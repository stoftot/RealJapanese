using System.Collections.Concurrent;
using System.Text.Json;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Genki.Inference;
using Genki.Studio;
using RealJapanese.TestSupport;
using Repositories.Genki;

namespace RealJapanese.IntegrationTests;

/// <summary>Exercises the durable PC studio queue using private catalogs and deterministic model responses.</summary>
public sealed class GenkiStudioTests
{
    private const string SchemaId = "g01-01-noun-predicate";

    [Fact]
    public async Task Configuration_round_trips_model_and_batch_paths_and_rejects_overlapping_roots()
    {
        await using var studio = await StudioFixture.Create();
        var settings = studio.Coordinator.Snapshot().Settings;
        settings.ModelsDirectory = Path.Combine(studio.Workspace.Root, "custom models");
        settings.PublishPath = Path.Combine(studio.Workspace.Root, "export", "questions.jsonl");
        settings.MaxTokens = 1000;
        settings.AlternativeLimit = 0;
        await studio.Coordinator.SaveSettingsAsync(settings);

        var loaded = new StudioWorkspace(studio.ConfigurationFile, TestWorkspace.RepositoryRoot).Load();
        Assert.Equal(settings.ModelsDirectory, loaded.Models.ModelsDirectory);
        Assert.Equal(settings.PublishPath, loaded.PublishPath);
        Assert.Equal(settings.MaxTokens, loaded.Models.MaxTokens);
        Assert.Equal(settings.AlternativeLimit, loaded.Batch.AlternativeLimit);

        settings.StateRoot = Path.Combine(settings.DataRoot, "generated");
        await Assert.ThrowsAsync<InvalidDataException>(() => studio.Coordinator.SaveSettingsAsync(settings));
    }

    [Fact]
    public async Task Configuration_rejects_missing_data_or_state_root()
    {
        await using var studio = await StudioFixture.Create();
        var settings = studio.Coordinator.Snapshot().Settings;
        settings.DataRoot = " ";
        await Assert.ThrowsAsync<InvalidDataException>(() => studio.Coordinator.SaveSettingsAsync(settings));

        settings = studio.Coordinator.Snapshot().Settings;
        settings.StateRoot = "";
        await Assert.ThrowsAsync<InvalidDataException>(() => studio.Coordinator.SaveSettingsAsync(settings));
    }

    [Fact]
    public async Task Configuration_cannot_change_while_work_is_queued()
    {
        await using var studio = await StudioFixture.Create();
        var id = await studio.Coordinator.EnqueueAsync(Generate(limit: 1));

        var changed = studio.Coordinator.Snapshot().Settings;
        changed.MaxTokens++;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => studio.Coordinator.SaveSettingsAsync(changed));

        Assert.Contains("Pause or cancel queued work", error.Message);
        Assert.Equal("queued", studio.Job(id).Status);
    }

    [Fact]
    public async Task Persisted_jobs_pause_on_restart_and_inference_waits_for_explicit_resume()
    {
        await using var studio = await StudioFixture.Create(start: false);
        var config = studio.Configuration;
        var candidate = studio.Candidate(schemaId: SchemaId);
        var savedJob = new StudioJob
        {
            Id = "persisted-job", Request = Generate(limit: 1), Configuration = config,
            Status = "queued", Activity = "Waiting for the worker"
        };
        new BatchStore(config.StateRoot).Save("studio-jobs", savedJob.Id, savedJob);
        var interruptedJob = new StudioJob
        {
            Id = "interrupted-job", Request = Generate(limit: 1), Configuration = config,
            Status = "running", Activity = "Generating"
        };
        new BatchStore(config.StateRoot).Save("studio-jobs", interruptedJob.Id, interruptedJob);

        var testWorkspace = studio.Workspace;
        var configurationFile = studio.ConfigurationFile;
        var fake = studio.Fake;
        await studio.ReleaseCoordinator();
        await using var restarted = await StudioFixture.Open(testWorkspace, configurationFile, fake, start: true);
        Assert.Equal("paused", restarted.Job(savedJob.Id).Status);
        Assert.Equal("paused", restarted.Job(interruptedJob.Id).Status);
        await Task.Delay(150);
        Assert.Empty(studio.Fake.Calls);

        await restarted.Coordinator.ResumeAsync(savedJob.Id);
        await restarted.WaitForStatus(savedJob.Id, "paused");
        Assert.NotEmpty(studio.Fake.Calls);
        Assert.NotNull(new BatchStore(config.StateRoot).Read<QuestionState>("questions", candidate.Id));
    }

    [Fact]
    public async Task Bounded_coverage_scan_marks_partial_then_exact_without_calling_models()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var vocabulary = GenkiVocabulary.Load(studio.Configuration.DataRoot);
        var schema = studio.Catalog.Schemas.Single(s => s.Id == SchemaId);
        var twoNouns = vocabulary.Entries.Where(e => e.Ref.WordType == "noun").Take(2).Select(e => e.Ref).ToArray();
        var selected = new GenkiVocabulary(twoNouns.Select(vocabulary.Resolve));
        var expected = new CandidateEnumerator(studio.Catalog, selected, []).Enumerate(schema).LongCount();
        Assert.True(expected > 1);

        var partial = await studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "scan", SchemaIds = [SchemaId], Words = twoNouns, Limit = 1, Minutes = null
        });
        await studio.WaitForStatus(partial, "completed");
        await studio.Coordinator.RefreshAsync();
        var first = studio.Row().Coverage!;
        Assert.False(first.Complete);
        Assert.Equal(1, first.Enumerated);
        Assert.Equal(1, first.NotStarted);

        var exact = await studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "scan", SchemaIds = [SchemaId], Words = twoNouns, Exhaustive = true, Limit = null, Minutes = null
        });
        await studio.WaitForStatus(exact, "completed");
        await studio.Coordinator.RefreshAsync();
        var final = studio.Row().Coverage!;
        Assert.True(final.Complete);
        Assert.Equal(expected, final.Enumerated);
        Assert.Empty(studio.Fake.Calls);
    }

    [Fact]
    public async Task Generation_resume_reuses_completed_checkpoint()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var id = await studio.Coordinator.EnqueueAsync(Generate(limit: 1));
        await studio.WaitForStatus(id, "paused");
        var callsAfterFirstPass = studio.Fake.CallCount;
        Assert.Equal(1, studio.States().Count(s => s.Status == "completed"));

        await studio.Coordinator.ResumeAsync(id);
        await studio.WaitForStatus(id, "paused");

        Assert.True(studio.Job(id).Summary.Reused >= 1);
        Assert.True(studio.Fake.CallCount > callsAfterFirstPass); // A later unseen candidate was processed.
        Assert.Equal(2, studio.States().Count(s => s.Status == "completed"));
    }

    [Fact]
    public async Task Pause_and_cancel_keep_the_saved_candidate_checkpoint()
    {
        await using var paused = await StudioFixture.Create(start: true);
        paused.Fake.BlockStage = "candidate-validation";
        var pauseId = await paused.Coordinator.EnqueueAsync(Generate(limit: 1));
        await paused.Fake.WaitForCall("candidate-validation");
        await paused.Coordinator.PauseAsync(pauseId);
        await paused.WaitForStatus(pauseId, "paused");
        Assert.Contains(paused.States(), s => s.Status == "pending" && s.Stages.ContainsKey("A-candidate"));

        paused.Fake.BlockStage = null;
        await paused.Coordinator.ResumeAsync(pauseId);
        await paused.WaitForStatus(pauseId, "paused");

        await using var cancelled = await StudioFixture.Create(start: true);
        cancelled.Fake.BlockStage = "candidate-validation";
        var cancelId = await cancelled.Coordinator.EnqueueAsync(Generate(limit: 1));
        await cancelled.Fake.WaitForCall("candidate-validation");
        await cancelled.Coordinator.CancelAsync(cancelId);
        await cancelled.WaitForStatus(cancelId, "cancelled");
        Assert.Contains(cancelled.States(), s => s.Status == "pending" && s.Stages.ContainsKey("A-candidate"));
    }

    [Fact]
    public async Task Scoped_regeneration_archives_only_matching_scope_and_leaves_published_bank_unchanged()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var candidate = studio.Candidate(schemaId: SchemaId);
        var other = studio.Candidate(schemaId: SchemaId, skip: 1);
        var store = new BatchStore(studio.Configuration.StateRoot);
        store.Save("questions", candidate.Id, State(candidate, "pending"));
        store.Save("questions", other.Id, State(other, "pending"));
        Directory.CreateDirectory(Path.GetDirectoryName(studio.Configuration.PublishPath!)!);
        File.WriteAllText(studio.Configuration.PublishPath!, "existing published bank\n");
        var modelCalls = studio.Fake.CallCount;

        var id = await studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "generate", Mode = "regenerate", SchemaIds = [SchemaId], Words = candidate.RequiredWords.ToArray(),
            Limit = 1, Minutes = null, ConfirmRegeneration = true
        });
        await studio.WaitForStatus(id, "paused");

        var job = studio.Job(id);
        Assert.NotNull(job.ArchiveDirectory);
        Assert.Contains(Directory.EnumerateFiles(Path.Combine(job.ArchiveDirectory!, "questions")),
            path => BatchStore.ReadFile<QuestionState>(path)?.CandidateId == candidate.Id);
        Assert.NotNull(store.Read<QuestionState>("questions", other.Id));
        Assert.Equal("existing published bank\n", File.ReadAllText(studio.Configuration.PublishPath!));
        Assert.True(studio.Fake.CallCount > modelCalls);
    }

    [Fact]
    public async Task Retry_failed_resets_failed_attempt_but_reuses_successful_stages()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var candidate = studio.Candidate(schemaId: SchemaId);
        var config = studio.Configuration;
        var provenance = GenkiJson.Fingerprint(new
        {
            Version = QuestionPipeline.Version, models = studio.Fake.Provenance, config.Batch.AlternativeLimit
        });
        var state = State(candidate, "failed");
        state.ActiveStage = "C-context";
        state.Attempts["C-context"] = 3;
        state.Stages["B-candidate-validation"] = JsonSerializer.SerializeToElement(
            new Judgment { Outcome = "valid", Reason = "fixture", GrammarIds = ["g01-01"] }, GenkiJson.Options);
        state = CopyWithProvenance(state, provenance);
        new BatchStore(config.StateRoot).Save("questions", candidate.Id, state);
        var id = await studio.Coordinator.EnqueueAsync(Generate(limit: 1, mode: "retry-failed"));
        await studio.WaitForStatus(id, "paused");

        Assert.Equal(0, studio.Fake.Count("candidate-validation"));
        Assert.Equal(1, studio.Fake.Count("context"));
        var retried = new BatchStore(config.StateRoot).Read<QuestionState>("questions", candidate.Id)!;
        Assert.Contains("B-candidate-validation", retried.Stages.Keys);
        Assert.Equal(1, retried.Attempts["C-context"]);
    }

    [Fact]
    public async Task Failed_tag_request_recovers_and_finishes_completed_after_explicit_retry()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var word = GenkiVocabulary.Load(studio.Configuration.DataRoot).Entries[0].Ref;
        var tag = studio.Catalog.Registry.Tags.Keys.First();
        studio.Fake.FailStage = "semantic-tags";
        studio.Fake.FailuresRemaining = 1;
        var id = await studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "tag", Words = [word], TagIds = [tag], Limit = 1, Minutes = null
        });
        await studio.WaitForStatus(id, "failed");
        Assert.Equal(1, studio.Fake.Count("semantic-tags"));

        await studio.Coordinator.ResumeAsync(id);
        await studio.WaitForStatus(id, "completed");

        Assert.Equal(2, studio.Fake.Count("semantic-tags"));
        Assert.Equal("matches", new BatchStore(studio.Configuration.StateRoot)
            .Read<TaggingState>("tags", word.ToString())!.Evaluations[tag].Status);
    }

    [Fact]
    public async Task Tag_failure_then_success_within_attempt_budget_finishes_completed()
    {
        await using var studio = await StudioFixture.Create(start: true, maxAttempts: 2);
        var word = GenkiVocabulary.Load(studio.Configuration.DataRoot).Entries[0].Ref;
        var tag = studio.Catalog.Registry.Tags.Keys.First();
        studio.Fake.FailStage = "semantic-tags";
        studio.Fake.FailuresRemaining = 1;
        var id = await studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "tag", Words = [word], TagIds = [tag], Limit = 2, Minutes = null
        });

        await studio.WaitForStatus(id, "completed");

        Assert.Equal(2, studio.Fake.Count("semantic-tags"));
        var evaluation = new BatchStore(studio.Configuration.StateRoot)
            .Read<TaggingState>("tags", word.ToString())!.Evaluations[tag];
        Assert.Equal("matches", evaluation.Status);
        Assert.Equal(2, evaluation.Attempts);
    }

    [Fact]
    public async Task Cancelled_scoped_tag_regeneration_keeps_other_mapping_and_removes_selected_stale_positive()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var vocabulary = GenkiVocabulary.Load(studio.Configuration.DataRoot);
        var selectedWord = vocabulary.Entries[0];
        var otherWord = vocabulary.Entries[1];
        var tag = studio.Catalog.Registry.Tags.Keys.First();
        var tagFingerprint = TagFingerprint(studio.Catalog.Registry, tag);
        var store = new BatchStore(studio.Configuration.StateRoot);
        store.Save("tags", selectedWord.Ref.ToString(), TagState(selectedWord.Ref, selectedWord.Fingerprint, tag, tagFingerprint));
        store.Save("tags", otherWord.Ref.ToString(), TagState(otherWord.Ref, otherWord.Fingerprint, tag, tagFingerprint));
        BatchStore.WriteAtomic(Path.Combine(store.Root, "word-tags.json"), new[]
        {
            new WordTags { Word = selectedWord.Ref, WordFingerprint = selectedWord.Fingerprint, DirectTags = [tag] },
            new WordTags { Word = otherWord.Ref, WordFingerprint = otherWord.Fingerprint, DirectTags = [tag] }
        });
        studio.Fake.BlockStage = "semantic-tags";

        var id = await studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "tag", Mode = "regenerate", Words = [selectedWord.Ref], TagIds = [tag],
            Limit = 1, Minutes = null, ConfirmRegeneration = true
        });
        await studio.Fake.WaitForCall("semantic-tags");
        var beforeCancel = BatchStore.ReadFile<WordTags[]>(Path.Combine(store.Root, "word-tags.json"))!;
        Assert.Empty(beforeCancel.Single(x => x.Word == selectedWord.Ref).DirectTags);
        Assert.Contains(tag, beforeCancel.Single(x => x.Word == otherWord.Ref).DirectTags);

        await studio.Coordinator.CancelAsync(id);
        await studio.WaitForStatus(id, "cancelled");

        var afterCancel = BatchStore.ReadFile<WordTags[]>(Path.Combine(store.Root, "word-tags.json"))!;
        Assert.Empty(afterCancel.Single(x => x.Word == selectedWord.Ref).DirectTags);
        Assert.Contains(tag, afterCancel.Single(x => x.Word == otherWord.Ref).DirectTags);
        var selectedState = store.Read<TaggingState>("tags", selectedWord.Ref.ToString())!;
        Assert.Equal("pending", selectedState.Evaluations[tag].Status);
        Assert.Equal("matches", store.Read<TaggingState>("tags", otherWord.Ref.ToString())!.Evaluations[tag].Status);
        Assert.NotNull(studio.Job(id).ArchiveDirectory);
    }

    [Fact]
    public async Task Publication_requires_confirmation_validates_destination_and_exports_completed_only()
    {
        await using var studio = await StudioFixture.Create(start: true);
        var generationId = await studio.Coordinator.EnqueueAsync(Generate(limit: 1));
        await studio.WaitForStatus(generationId, "paused");
        var store = new BatchStore(studio.Configuration.StateRoot);
        var pending = studio.Candidate(schemaId: SchemaId, skip: 1);
        store.Save("questions", pending.Id, State(pending, "needs-review"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "publish", ConfirmPublication = false
        }));
        var settings = studio.Coordinator.Snapshot().Settings;
        settings.PublishPath = Path.Combine(settings.StateRoot, "questions.jsonl");
        await Assert.ThrowsAsync<InvalidDataException>(() => studio.Coordinator.SaveSettingsAsync(settings));

        await Assert.ThrowsAsync<InvalidOperationException>(() => studio.Coordinator.EnqueueAsync(new StudioRequest
        {
            Kind = "publish", ConfirmPublication = true,
            ExpectedPublishPath = Path.Combine(studio.Workspace.Root, "different-bank.jsonl")
        }));
        var publish = await studio.Coordinator.EnqueueAsync(new StudioRequest { Kind = "publish", ConfirmPublication = true });
        await studio.WaitForStatus(publish, "completed");
        var lines = File.ReadAllLines(studio.Configuration.PublishPath!);
        Assert.Single(lines);
        Assert.Contains("\"schemaId\":\"g01-01-noun-predicate\"", lines[0]);
    }

    private static StudioRequest Generate(int limit, string mode = "resume") => new()
    {
        Kind = "generate", Mode = mode, SchemaIds = [SchemaId], Limit = limit, Minutes = null
    };

    private static QuestionState State(Candidate candidate, string status) => new()
    {
        CandidateId = candidate.Id, InputFingerprint = candidate.InputFingerprint, SchemaId = candidate.Schema.Id,
        Provenance = "fixture", Status = status,
        Stages = new(StringComparer.Ordinal)
        {
            ["A-candidate"] = JsonSerializer.SerializeToElement(candidate, GenkiJson.Options)
        }
    };

    private static QuestionState CopyWithProvenance(QuestionState state, string provenance) => new()
    {
        CandidateId = state.CandidateId, InputFingerprint = state.InputFingerprint, SchemaId = state.SchemaId,
        Provenance = provenance, Status = state.Status, ActiveStage = state.ActiveStage, Reason = state.Reason,
        Stages = state.Stages, Attempts = state.Attempts, AlternativeExclusions = state.AlternativeExclusions
    };

    private static TaggingState TagState(WordRef word, string wordFingerprint, string tag, string tagFingerprint) => new()
    {
        Word = word, WordFingerprint = wordFingerprint,
        Evaluations = new(StringComparer.Ordinal)
        {
            [tag] = new TagEvaluation { Status = "matches", DefinitionFingerprint = tagFingerprint }
        }
    };

    private static string TagFingerprint(SemanticRegistry registry, string tag) =>
        GenkiJson.Fingerprint(registry.Expand([tag]).Order(StringComparer.Ordinal).Select(id => registry.Tags[id]));

    private sealed class StudioFixture : IAsyncDisposable
    {
        public TestWorkspace Workspace { get; }
        public string ConfigurationFile { get; }
        public GenkiCatalog Catalog { get; } = new();
        public FakeModelFactory Fake { get; }
        public StudioCoordinator Coordinator { get; }
        public GenkiConfiguration Configuration => new StudioWorkspace(ConfigurationFile, TestWorkspace.RepositoryRoot).Load();

        private StudioFixture(TestWorkspace workspace, string configurationFile, FakeModelFactory fake, StudioCoordinator coordinator)
        { Workspace = workspace; ConfigurationFile = configurationFile; Fake = fake; Coordinator = coordinator; }

        public static async Task<StudioFixture> Create(bool start = false, int maxAttempts = 1)
        {
            var workspace = new TestWorkspace();
            var configFile = Path.Combine(workspace.Root, ".tooling", "studio.json");
            var models = Path.Combine(workspace.Root, "models");
            Directory.CreateDirectory(models);
            File.WriteAllText(Path.Combine(models, "qwen.gguf"), "fixture");
            File.WriteAllText(Path.Combine(models, "translation.gguf"), "fixture");
            var config = new GenkiConfiguration
            {
                DataRoot = workspace.CatalogRoot,
                StateRoot = Path.Combine(workspace.Root, "generated"),
                PublishPath = Path.Combine(workspace.Root, "published", "questions.jsonl"),
                Models = new AiLibraryOptions
                {
                    ModelsDirectory = models, QwenModelFile = "qwen.gguf", TranslationModelFile = "translation.gguf",
                    MaxTokens = 1000, QwenContextSize = 4096, TranslationContextSize = 2048
                },
                Batch = new BatchOptions { AlternativeLimit = 0, MaxAttempts = maxAttempts }
            };
            var workspaceApi = new StudioWorkspace(configFile, TestWorkspace.RepositoryRoot);
            workspaceApi.Save(config);
            var fake = new FakeModelFactory();
            var coordinator = new StudioCoordinator(workspaceApi, fake);
            var fixture = new StudioFixture(workspace, configFile, fake, coordinator);
            if (start) await fixture.Start();
            return fixture;
        }

        public static async Task<StudioFixture> Open(TestWorkspace workspace, string configFile, FakeModelFactory fake, bool start)
        {
            var coordinator = new StudioCoordinator(new StudioWorkspace(configFile, TestWorkspace.RepositoryRoot), fake);
            var fixture = new StudioFixture(workspace, configFile, fake, coordinator);
            if (start) await fixture.Start();
            return fixture;
        }

        private async Task Start()
        {
            await Coordinator.StartAsync(CancellationToken.None);
            await Coordinator.RefreshAsync();
        }

        public Candidate Candidate(string schemaId, int skip = 0)
        {
            var vocabulary = GenkiVocabulary.Load(Configuration.DataRoot);
            return new CandidateEnumerator(Catalog, vocabulary, []).Enumerate(Catalog.Schemas.Single(s => s.Id == schemaId)).Skip(skip).First();
        }
        public StudioJobView Job(string id) => Coordinator.Snapshot().Jobs.Single(j => j.Id == id);
        public StudioSchemaRow Row() => Coordinator.Snapshot().Schemas.Single(s => s.Id == SchemaId);
        public IReadOnlyList<QuestionState> States() => new BatchStore(Configuration.StateRoot).ReadAll<QuestionState>("questions").ToArray();
        public async Task WaitForStatus(string id, string status)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                if (Job(id).Status == status) return;
                await Task.Delay(20);
            }
            Assert.Fail($"Job {id} did not reach status '{status}'. Current: {Job(id).Status}; activity: {Job(id).Activity}; error: {Job(id).Error}");
        }
        private bool coordinatorReleased;
        public async Task ReleaseCoordinator()
        {
            if (coordinatorReleased) return;
            coordinatorReleased = true;
            try { await Coordinator.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token); }
            finally { Coordinator.Dispose(); }
        }
        public async ValueTask DisposeAsync()
        {
            await ReleaseCoordinator();
            Workspace.Dispose();
        }
    }

    private sealed class FakeModelFactory : IStudioModelFactory
    {
        private readonly FakeSession session = new();
        public bool IsAvailable => true;
        public int CallCount => session.CallCount;
        public IReadOnlyList<string> Calls => session.Calls;
        public int Count(string stage) => session.Count(stage);
        public string Provenance => session.Provenance;
        public string? BlockStage { get => session.BlockStage; set => session.SetBlockStage(value); }
        public string? FailStage { get => session.FailStage; set => session.FailStage = value; }
        public int FailuresRemaining { get => session.FailuresRemaining; set => session.FailuresRemaining = value; }
        public Task WaitForCall(string stage) => session.WaitForCall(stage);
        public IGenkiModelSession Create(AiLibraryOptions options) => session;
    }

    private sealed class FakeSession : IGenkiModelSession
    {
        private readonly ConcurrentDictionary<string, int> counts = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<TaskCompletionSource> callWaiters = new();
        private TaskCompletionSource? release;
        public string Provenance => "genki-studio-test-model-v1";
        public string? BlockStage { get; private set; }
        public string? FailStage { get; set; }
        public int FailuresRemaining { get; set; }
        public IReadOnlyList<string> Calls => counts.SelectMany(kvp => Enumerable.Repeat(kvp.Key, kvp.Value)).ToArray();
        public int CallCount => counts.Values.Sum();
        public int Count(string stage) => counts.GetValueOrDefault(stage);

        public void SetBlockStage(string? stage)
        {
            BlockStage = stage;
            if (stage is null)
            {
                release?.TrySetResult();
                release = null;
            }
        }

        public async Task<string> QwenAsync(string stage, string systemPrompt, string inputJson, CancellationToken cancellationToken)
        {
            counts.AddOrUpdate(stage, 1, (_, count) => count + 1);
            while (callWaiters.TryDequeue(out var waiter)) waiter.TrySetResult();
            if (stage == FailStage && FailuresRemaining > 0) { FailuresRemaining--; throw new InvalidDataException("Fixture model failure"); }
            if (stage == BlockStage)
            {
                release ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                await release.Task.WaitAsync(cancellationToken);
            }
            var grammar = "\"grammarIds\":[\"g01-01\"]";
            return stage switch
            {
                "context" => "{\"setting\":null,\"judgment\":{\"outcome\":\"valid\",\"reason\":\"fixture\"," + grammar + "}}",
                "kana" => JsonSerializer.Serialize(new { outcome = "valid", kana = ExpectedReading(inputJson), reason = "fixture" }, GenkiJson.Options),
                "semantic-tags" => TagResponseJson(inputJson),
                _ => "{\"outcome\":\"valid\",\"reason\":\"fixture\"," + grammar + "}"
            };
        }

        public Task<string> TranslateAsync(string japanese, string? setting, string register, CancellationToken cancellationToken) => Task.FromResult("Fixture translation");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async Task WaitForCall(string stage)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                if (Count(stage) > 0) return;
                var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                callWaiters.Enqueue(waiter);
                if (Count(stage) > 0) return;
                await waiter.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            throw new TimeoutException($"Fake model was not called for {stage}.");
        }

        private static string ExpectedReading(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("expectedReading").GetString()!;
        }

        private static string TagResponseJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            var evaluations = document.RootElement.GetProperty("tags").EnumerateArray()
                .Select(item => new TagDecision(item.GetProperty("id").GetString()!, "matches", "fixture"))
                .ToArray();
            return JsonSerializer.Serialize(new TagResponse(false, "fixture", evaluations), GenkiJson.Options);
        }
    }
}
