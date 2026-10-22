using System.Collections.Concurrent;
using AudioStudio;

namespace RealJapanese.Tests;

/// <summary>Exercises generation pause, cancellation and resume checkpoints with deterministic fake model batches.</summary>
public sealed class GenerationCheckpointTests
{
    [Fact]
    public async Task Pause_WaitsForInFlightGenerationThenDisposesModelsAndReleasesWork()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var backend = new FakeBackend(store);
        var gate = backend.BlockGeneration("bert", "Nouns/1");
        var service = new GenerationService(store, catalog, backend);
        var started = await Start(service, ["Nouns/1", "Nouns/2"], ["bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        service.Pause(started.Id);

        Assert.Equal("Pausing", store.GetRun(started.Id).Status);
        Assert.False(gate.Token.IsCancellationRequested);
        Assert.Equal(1, backend.Events.Count(e => e.StartsWith("generate ", StringComparison.Ordinal)));
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");

        Assert.Equal("Generated", store.GetRun(started.Id).Clips[0].Status);
        Assert.Equal(1, backend.VoiceDisposals);
        // Pause releases the shared work slot only after the in-flight request is saved and the model is disposed.
        Assert.Equal("work-released", await service.ReferenceOperation(_ => Task.FromResult("work-released"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Resume_ReopensModelsInOrderRunsOnlyRemainingRequestsThenChecksAllClips()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var firstBackend = new FakeBackend(store);
        var gate = firstBackend.BlockGeneration("bert", "Nouns/1");
        var firstService = new GenerationService(store, catalog, firstBackend);
        var started = await Start(firstService, ["Nouns/1", "Verbs/1"], ["fish", "bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        firstService.Pause(started.Id);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");
        Assert.Equal("Generated", store.GetRun(started.Id).Clips.Single(c => c.Engine == "bert" && c.Key == "Nouns/1").Status);
        Assert.Equal("Pending", store.GetRun(started.Id).Clips.Single(c => c.Engine == "bert" && c.Key == "Verbs/1").Status);

        var reopenedStore = CreateStore(temp.Path, catalog);
        var resumeBackend = new FakeBackend(reopenedStore);
        var resumeService = new GenerationService(reopenedStore, catalog, resumeBackend);
        await resumeService.ResumeAsync(started.Id);
        await WaitForStatus(reopenedStore, started.Id, "Completed");

        Assert.DoesNotContain(resumeBackend.Events, e => e == "generate bert Nouns/1");
        Assert.Equal(
            new[] { "open bert", "generate bert Verbs/1", "dispose bert", "open fish", "generate fish Nouns/1", "generate fish Verbs/1", "dispose fish" },
            resumeBackend.Events.Where(e => e.StartsWith("open ", StringComparison.Ordinal) && e != "open checks"
                || e.StartsWith("generate ", StringComparison.Ordinal) || e.StartsWith("dispose ", StringComparison.Ordinal) && e != "dispose checks"));
        Assert.Equal(4, resumeBackend.Events.Count(e => e.StartsWith("convert ", StringComparison.Ordinal)));
        Assert.Equal(16, resumeBackend.Events.Count(e => e.StartsWith("check ", StringComparison.Ordinal)));
        Assert.Equal("open checks", resumeBackend.Events.First(e => e == "open checks"));
        Assert.Equal("dispose checks", resumeBackend.Events.Last(e => e == "dispose checks"));
        Assert.All(reopenedStore.GetRun(started.Id).Clips, clip => Assert.Equal("Text matches", clip.Validation));
    }

    [Fact]
    public async Task PauseDuringFirstCheck_SavesItAndResumeChecksOnlyTheRemainingClip()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var firstBackend = new FakeBackend(store);
        var gate = firstBackend.BlockCheck("bert", "Nouns/1");
        var service = new GenerationService(store, catalog, firstBackend);
        var started = await Start(service, ["Nouns/1", "Nouns/2"], ["bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        service.Pause(started.Id);
        Assert.Equal("Pausing", store.GetRun(started.Id).Status);
        Assert.False(gate.Token.IsCancellationRequested);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");

        var paused = store.GetRun(started.Id);
        Assert.Equal("Text matches", paused.Clips.Single(c => c.Key == "Nouns/1").Validation);
        Assert.Equal("Pending", paused.Clips.Single(c => c.Key == "Nouns/2").Validation);
        Assert.All(paused.Clips, clip => Assert.NotNull(clip.File));

        var reopenedStore = CreateStore(temp.Path, catalog);
        var resumeBackend = new FakeBackend(reopenedStore);
        await new GenerationService(reopenedStore, catalog, resumeBackend).ResumeAsync(started.Id);
        await WaitForStatus(reopenedStore, started.Id, "Completed");

        Assert.DoesNotContain(resumeBackend.Events, e => e.StartsWith("open ", StringComparison.Ordinal) && e != "open checks");
        Assert.DoesNotContain(resumeBackend.Events, e => e.StartsWith("generate ", StringComparison.Ordinal));
        Assert.Equal("open checks", resumeBackend.Events.First());
        Assert.Equal("dispose checks", resumeBackend.Events.Last());
        Assert.Equal(7, resumeBackend.Events.Count(e => e.StartsWith("check ", StringComparison.Ordinal)));
        Assert.DoesNotContain(resumeBackend.Events, e => e.StartsWith("convert ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancelPausedRun_OpensNoModelAndCannotBeResumed()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var backend = new FakeBackend(store);
        var gate = backend.BlockGeneration("bert", "Nouns/1");
        var service = new GenerationService(store, catalog, backend);
        var started = await Start(service, ["Nouns/1", "Nouns/2"], ["bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        service.Pause(started.Id);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");
        var openedBeforeCancel = backend.VoiceOpens;

        service.Cancel(started.Id);

        Assert.Equal("Cancelled", store.GetRun(started.Id).Status);
        Assert.Equal(openedBeforeCancel, backend.VoiceOpens);
        var resumeBackend = new FakeBackend(store);
        var error = await Assert.ThrowsAsync<StudioException>(() => new GenerationService(store, catalog, resumeBackend).ResumeAsync(started.Id));
        Assert.Equal(409, error.Status);
        Assert.Empty(resumeBackend.Events);
    }

    [Fact]
    public async Task CancelActiveRun_SignalsRequestAndPreservesEarlierCompletedClip()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var backend = new FakeBackend(store);
        var gate = backend.BlockGeneration("bert", "Nouns/2");
        var service = new GenerationService(store, catalog, backend);
        var started = await Start(service, ["Nouns/1", "Nouns/2"], ["bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal("Generated", store.GetRun(started.Id).Clips.Single(c => c.Key == "Nouns/1").Status);

        service.Cancel(started.Id);
        await WaitForStatus(store, started.Id, "Cancelled");

        Assert.True(gate.Token.IsCancellationRequested);
        var run = store.GetRun(started.Id);
        var saved = run.Clips.Single(c => c.Key == "Nouns/1");
        Assert.Equal("Generated", saved.Status);
        Assert.True(File.Exists(StudioStore.SafePath(store.RunFolder(started.Id), saved.File!)));
        Assert.Null(run.Clips.Single(c => c.Key == "Nouns/2").File);
        Assert.Equal(1, backend.VoiceDisposals);
    }

    [Fact]
    public async Task SecondResumeWhileResumedWorkIsActive_IsRejected()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var initialBackend = new FakeBackend(store);
        var initialGate = initialBackend.BlockGeneration("bert", "Nouns/1");
        var initialService = new GenerationService(store, catalog, initialBackend);
        var started = await Start(initialService, ["Nouns/1", "Nouns/2"], ["bert"]);
        await initialGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        initialService.Pause(started.Id);
        initialGate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");

        var resumeBackend = new FakeBackend(store);
        var resumeGate = resumeBackend.BlockGeneration("bert", "Nouns/2");
        var resumeService = new GenerationService(store, catalog, resumeBackend);
        await resumeService.ResumeAsync(started.Id);
        await resumeGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<StudioException>(() => resumeService.ResumeAsync(started.Id));

        Assert.Equal(409, error.Status);
        resumeGate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Completed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resume_RejectsMissingOrChangedCompletedClip(bool tamper)
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var backend = new FakeBackend(store);
        var gate = backend.BlockGeneration("bert", "Nouns/1");
        var service = new GenerationService(store, catalog, backend);
        var started = await Start(service, ["Nouns/1", "Nouns/2"], ["bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        service.Pause(started.Id);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");
        var clip = store.GetRun(started.Id).Clips.Single(c => c.Key == "Nouns/1");
        var path = StudioStore.SafePath(store.RunFolder(started.Id), clip.File!);
        if (tamper) File.WriteAllText(path, "changed audio"); else File.Delete(path);

        var resumeBackend = new FakeBackend(store);
        var error = await Assert.ThrowsAsync<StudioException>(() => new GenerationService(store, catalog, resumeBackend).ResumeAsync(started.Id));

        Assert.Equal(409, error.Status);
        Assert.Empty(resumeBackend.Events);
    }

    [Fact]
    public async Task Store_RecoversPausingAsPausedAndRetainsPausedRunWithoutAllowingExport()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var backend = new FakeBackend(store);
        var gate = backend.BlockGeneration("bert", "Nouns/1");
        var service = new GenerationService(store, catalog, backend);
        var started = await Start(service, ["Nouns/1", "Nouns/2"], ["bert"]);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        service.Pause(started.Id);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, started.Id, "Paused");
        store.Select(started.Id, new() { ["Nouns/1"] = "bert" });
        store.UpdateRun(started.Id, run => run.Status = "Pausing");

        var recoveredStore = CreateStore(temp.Path, catalog);
        Assert.Equal("Paused", recoveredStore.GetRun(started.Id).Status);
        var afterRestart = CreateStore(temp.Path, catalog);
        Assert.Equal("Paused", afterRestart.GetRun(started.Id).Status);
        var error = Assert.Throws<StudioException>(() => afterRestart.Export(started.Id, acknowledgeUnselected: true));
        Assert.Equal(409, error.Status);
        Assert.Empty(afterRestart.Exports());
    }

    [Fact]
    public async Task FormatUpgrade_PauseAfterConversionResumesRemainingClipsAndChecksEachVariantWithoutOpeningVoice()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateTerminalRun(store, [catalog.Get("Nouns/1"), catalog.Get("Verbs/1")], ["bert"]);
        var firstBackend = new FakeBackend(store);
        var gate = firstBackend.BlockConversion("bert", "Nouns/1");
        var firstService = new GenerationService(store, catalog, firstBackend);

        await firstService.PrepareFormatsAsync(run.Id);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        firstService.Pause(run.Id);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, run.Id, "Paused");

        var paused = store.GetRun(run.Id);
        Assert.True(paused.FormatUpgrade);
        Assert.Equal(4, paused.Clips.Single(c => c.Key == "Nouns/1").Variants.Count);
        Assert.Empty(paused.Clips.Single(c => c.Key == "Verbs/1").Variants);
        Assert.DoesNotContain(firstBackend.Events, e => e.StartsWith("open bert", StringComparison.Ordinal));
        Assert.DoesNotContain(firstBackend.Events, e => e.StartsWith("check ", StringComparison.Ordinal));

        var reopened = CreateStore(temp.Path, catalog);
        var resumeBackend = new FakeBackend(reopened);
        resumeBackend.ThrowOnPrepare = true;
        await new GenerationService(reopened, catalog, resumeBackend).ResumeAsync(run.Id);
        await WaitForStatus(reopened, run.Id, "Completed");

        Assert.DoesNotContain(resumeBackend.Events, e => e.StartsWith("open bert", StringComparison.Ordinal));
        Assert.Equal(new[] { "convert bert Verbs/1" }, resumeBackend.Events.Where(e => e.StartsWith("convert ", StringComparison.Ordinal)));
        Assert.Equal(6, resumeBackend.Events.Count(e => e.StartsWith("check ", StringComparison.Ordinal)));
        Assert.False(reopened.GetRun(run.Id).FormatUpgrade);
        Assert.All(reopened.GetRun(run.Id).Clips, clip => Assert.Equal(4, clip.Variants.Count));
    }

    [Fact]
    public async Task FormatUpgrade_PauseDuringVariantCheckResumesOnlyUncheckedFormats()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateTerminalRun(store, [catalog.Get("Nouns/1")], ["bert"]);
        PopulateVariants(store, run.Id, "bert", "Nouns/1", validationsPending: true);
        var firstBackend = new FakeBackend(store);
        var gate = firstBackend.BlockCheck("bert", "Nouns/1");
        var firstService = new GenerationService(store, catalog, firstBackend);

        await firstService.PrepareFormatsAsync(run.Id);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        firstService.Pause(run.Id);
        gate.Release.TrySetResult(true);
        await WaitForStatus(store, run.Id, "Paused");

        Assert.Equal(2, store.GetRun(run.Id).Clips.Single().Variants.Values.Count(v => v.Validation != "Pending"));
        var reopened = CreateStore(temp.Path, catalog);
        var resumeBackend = new FakeBackend(reopened);
        await new GenerationService(reopened, catalog, resumeBackend).ResumeAsync(run.Id);
        await WaitForStatus(reopened, run.Id, "Completed");

        Assert.Equal(2, resumeBackend.Events.Count(e => e.StartsWith("check ", StringComparison.Ordinal)));
        Assert.DoesNotContain(resumeBackend.Events, e => e.StartsWith("convert ", StringComparison.Ordinal));
        Assert.DoesNotContain(resumeBackend.Events, e => e.StartsWith("open bert", StringComparison.Ordinal));
        Assert.All(reopened.GetRun(run.Id).Clips.Single().Variants.Values, v => Assert.Equal("Text matches", v.Validation));
    }

    private static async Task<StudioRun> Start(GenerationService service, string[] wordKeys, string[] engines) =>
        await service.StartAsync(new(wordKeys, engines, null));

    private static async Task WaitForStatus(StudioStore store, string id, string expected)
    {
        var limit = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < limit)
        {
            if (store.GetRun(id).Status == expected) return;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.Equal(expected, store.GetRun(id).Status);
    }

    private static WordCatalog CreateCatalog(string dataRoot)
    {
        foreach (string dataset in WordCatalog.Datasets)
        {
            string folder = Path.Combine(dataRoot, dataset);
            Directory.CreateDirectory(folder);
            string json = dataset switch
            {
                "Nouns" => "[{\"id\":1,\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"},{\"id\":2,\"japanese\":\"犬\",\"kana\":\"いぬ\",\"english\":\"dog\"}]",
                "Verbs" => "[{\"id\":1,\"japanese\":\"見る\",\"kana\":\"みる\",\"english\":\"to see\"}]",
                _ => "[]"
            };
            File.WriteAllText(Path.Combine(folder, dataset + ".json"), json);
        }
        return new WordCatalog(dataRoot);
    }

    private static StudioStore CreateStore(string root, WordCatalog catalog) =>
        new(Path.Combine(root, "work"), Path.Combine(root, "exports"), catalog);

    private static StudioRun CreateTerminalRun(StudioStore store, List<CatalogWord> words, string[] engines)
    {
        string id = Guid.NewGuid().ToString("N");
        string runFolder = store.RunFolder(id);
        var clips = new List<StudioClip>();
        foreach (var word in words)
        foreach (string engine in engines)
        {
            string relative = Path.Combine(engine, word.Dataset + "-" + word.Id + ".wav");
            string rawRelative = Path.Combine(engine, "raw", word.Dataset + "-" + word.Id + ".wav");
            string path = StudioStore.SafePath(runFolder, relative);
            string rawPath = StudioStore.SafePath(runFolder, rawRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);
            File.WriteAllText(path, $"audio:{engine}:{word.Key}");
            File.WriteAllText(rawPath, $"raw:{engine}:{word.Key}");
            clips.Add(new StudioClip
            {
                Key = word.Key, Engine = engine, Status = "Completed", File = relative, RawFile = rawRelative,
                Sha256 = StudioStore.Hash(path), Seconds = 1, Validation = "Text matches", Transcript = word.Kana
            });
        }
        var run = new StudioRun
        {
            Id = id, Status = "Completed", Phase = "Complete", Words = words, Engines = engines,
            OutputQuality = "OriginalWav", Clips = clips
        };
        store.AddRun(run);
        return run;
    }

    private sealed class FakeBackend(StudioStore store) : IGenerationBackend
    {
        private readonly StudioStore runStore = store;
        private readonly ConcurrentQueue<string> events = new();
        private readonly ConcurrentDictionary<(string Engine, string Key), RequestGate> generationGates = new();
        private readonly ConcurrentDictionary<(string Engine, string Key), RequestGate> checkGates = new();
        private readonly ConcurrentDictionary<(string Engine, string Key), RequestGate> conversionGates = new();
        private int voiceOpens;
        private int voiceDisposals;
        public string[] Events => events.ToArray();
        public int VoiceOpens => Volatile.Read(ref voiceOpens);
        public int VoiceDisposals => Volatile.Read(ref voiceDisposals);
        public bool ThrowOnPrepare { get; set; }
        public void Prepare(StudioRun run)
        {
            if (ThrowOnPrepare) throw new InvalidOperationException("TTS preparation is not expected for a format-only upgrade.");
        }
        public RequestGate BlockGeneration(string engine, string key) => generationGates.GetOrAdd((engine, key), _ => new());
        public RequestGate BlockCheck(string engine, string key) => checkGates.GetOrAdd((engine, key), _ => new());
        public RequestGate BlockConversion(string engine, string key) => conversionGates.GetOrAdd((engine, key), _ => new());
        public async Task<Dictionary<string, AudioVariant>> ConvertFormatsAsync(StudioRun run, StudioClip clip, CancellationToken token)
        {
            events.Enqueue($"convert {clip.Engine} {clip.Key}");
            if (conversionGates.TryGetValue((clip.Engine, clip.Key), out var gate))
            {
                gate.Token = token;
                gate.Entered.TrySetResult(true);
                await gate.Release.Task.WaitAsync(token);
            }
            return PopulateVariants(runStore, run.Id, clip.Engine, clip.Key, validationsPending: true);
        }
        public IVoiceBatch OpenVoice(StudioRun run, string engine)
        {
            Interlocked.Increment(ref voiceOpens);
            events.Enqueue("open " + engine);
            return new VoiceBatch(this, run, engine);
        }
        public ICheckBatch OpenChecks(StudioRun run)
        {
            events.Enqueue("open checks");
            return new CheckBatch(this, run);
        }
        private sealed class VoiceBatch(FakeBackend owner, StudioRun run, string engine) : IVoiceBatch
        {
            public async Task<GeneratedAudio> GenerateAsync(CatalogWord word, CancellationToken token)
            {
                owner.events.Enqueue($"generate {engine} {word.Key}");
                if (owner.generationGates.TryGetValue((engine, word.Key), out var gate))
                {
                    gate.Token = token;
                    gate.Entered.TrySetResult(true);
                    await gate.Release.Task.WaitAsync(token);
                }
                string relative = Path.Combine(engine, word.Dataset + "-" + word.Id + ".wav");
                string rawRelative = Path.Combine(engine, "raw", word.Dataset + "-" + word.Id + ".wav");
                string path = StudioStore.SafePath(owner.runStore.RunFolder(run.Id), relative);
                string rawPath = StudioStore.SafePath(owner.runStore.RunFolder(run.Id), rawRelative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);
                File.WriteAllText(path, $"audio:{engine}:{word.Key}");
                File.WriteAllText(rawPath, $"raw:{engine}:{word.Key}");
                return new(relative, rawRelative, 1, StudioStore.Hash(path), "fake profile");
            }
            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref owner.voiceDisposals);
                owner.events.Enqueue("dispose " + engine);
                return ValueTask.CompletedTask;
            }
        }
        private sealed class CheckBatch(FakeBackend owner, StudioRun run) : ICheckBatch
        {
            public async Task<CheckedAudio> CheckAsync(StudioClip clip, CancellationToken token)
            {
                owner.events.Enqueue($"check {clip.Engine} {clip.Key} {Path.GetFileName(clip.File)}");
                if (owner.checkGates.TryGetValue((clip.Engine, clip.Key), out var gate))
                {
                    gate.Token = token;
                    gate.Entered.TrySetResult(true);
                    await gate.Release.Task.WaitAsync(token);
                }
                var word = run.Words.Single(w => w.Key == clip.Key);
                return new(word.Kana, null, "fake transcription");
            }
            public ValueTask DisposeAsync()
            {
                owner.events.Enqueue("dispose checks");
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class RequestGate
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; set; }
    }

    private static Dictionary<string, AudioVariant> PopulateVariants(StudioStore store, string runId, string engine, string key, bool validationsPending)
    {
        var run = store.GetRun(runId);
        var clip = run.Clips.Single(c => c.Key == key && c.Engine == engine);
        var variants = ClipFormats.Available(run, clip);
        foreach (string quality in ClipFormats.Qualities.Where(q => !variants.ContainsKey(q)))
        {
            string relative = Path.Combine("formats", engine, key.Replace('/', '-'), quality + ClipFormats.Extension(quality));
            string path = StudioStore.SafePath(store.RunFolder(runId), relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"format:{engine}:{key}:{quality}");
            variants[quality] = new AudioVariant
            {
                File = relative,
                Sha256 = StudioStore.Hash(path),
                Seconds = 1,
                Validation = validationsPending ? "Pending" : "Text matches"
            };
        }
        if (!validationsPending)
            foreach (var variant in variants.Values) variant.Validation = "Text matches";
        return variants;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
