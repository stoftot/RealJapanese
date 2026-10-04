using System.Text.Json;
using DataLoaders.Models;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Repositories.Genki;

namespace RealJapanese.Tests;

public sealed class GenkiPipelineTests
{
    [Fact]
    public async Task Tagger_retains_negative_coverage_and_reuses_it_after_restart()
    {
        using var temp = new TemporaryDirectory();
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var registry = new SemanticRegistry(GenkiTestData.Tags(("place", [])));
        var store = new BatchStore(temp.Path);
        var models = new FakeModels { TagOutcome = "does-not-match" };
        var tagger = new SemanticTagger(vocabulary, registry, store, models);

        var first = await tagger.RunAsync(new() { MaxAttempts = 1 });
        var callsAfterFirstRun = models.TotalQwenCalls;
        var state = store.Read<TaggingState>("tags", "noun:1");
        var resumed = await new SemanticTagger(vocabulary, registry, store, models)
            .RunAsync(new() { MaxAttempts = 1 });

        Assert.Equal(1, first.Completed);
        Assert.Equal("does-not-match", state!.Evaluations["place"].Status);
        Assert.Equal(0, models.TotalQwenCalls - callsAfterFirstRun);
        Assert.True(resumed.Reused > 0);
        Assert.Empty(new SemanticTagger(vocabulary, registry, store, models).ExportMapping().Single().DirectTags);
    }

    [Fact]
    public async Task Tagger_evaluates_new_words_and_new_tags_without_repeating_completed_pairs()
    {
        using var temp = new TemporaryDirectory();
        var firstWord = GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" });
        var vocabulary = new GenkiVocabulary([firstWord]);
        var store = new BatchStore(temp.Path);
        var models = new FakeModels { TagOutcome = "does-not-match" };
        var firstRegistry = new SemanticRegistry(GenkiTestData.Tags(("place", [])));
        await new SemanticTagger(vocabulary, firstRegistry, store, models)
            .RunAsync(new() { MaxAttempts = 1, TagGroupSize = 1 });
        var callsBeforeExpansion = models.TotalQwenCalls;

        var secondWord = GenkiTestData.Entry("noun",
            new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" });
        var expandedVocabulary = new GenkiVocabulary([firstWord, secondWord]);
        var expandedRegistry = new SemanticRegistry(GenkiTestData.Tags(("place", []), ("building", [])));
        var summary = await new SemanticTagger(expandedVocabulary, expandedRegistry, store, models)
            .RunAsync(new() { MaxAttempts = 1, TagGroupSize = 1 });

        Assert.Equal(3, summary.Completed);
        Assert.Equal(3, models.TotalQwenCalls - callsBeforeExpansion);
        Assert.Equal("does-not-match", store.Read<TaggingState>("tags", "noun:1")!.Evaluations["place"].Status);
        Assert.Contains("building", store.Read<TaggingState>("tags", "noun:1")!.Evaluations.Keys);
        Assert.Contains("place", store.Read<TaggingState>("tags", "noun:2")!.Evaluations.Keys);
        Assert.Contains("building", store.Read<TaggingState>("tags", "noun:2")!.Evaluations.Keys);
    }

    [Fact]
    public async Task Malformed_tag_group_does_not_create_negative_coverage_for_any_tag()
    {
        using var temp = new TemporaryDirectory();
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var registry = new SemanticRegistry(GenkiTestData.Tags(("first", []), ("second", [])));
        var store = new BatchStore(temp.Path);
        var summary = await new SemanticTagger(vocabulary, registry, store,
                new FakeModels { MalformTagResponses = true })
            .RunAsync(new() { MaxAttempts = 1, TagGroupSize = 1 });
        var state = store.Read<TaggingState>("tags", "noun:1")!;

        Assert.Equal(2, summary.Failed);
        Assert.Equal(2, state.Evaluations.Count);
        Assert.All(state.Evaluations.Values, e => Assert.Equal("failed", e.Status));
        Assert.DoesNotContain(state.Evaluations.Values, e => e.Status == "does-not-match");
        Assert.Empty(new SemanticTagger(vocabulary, registry, store, new FakeModels()).ExportMapping().Single().DirectTags);
    }

    [Fact]
    public async Task Tagger_time_budget_pauses_without_marking_unfinished_pair_negative()
    {
        using var temp = new TemporaryDirectory();
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var registry = new SemanticRegistry(GenkiTestData.Tags(("place", [])));
        var store = new BatchStore(temp.Path);
        var limited = await new SemanticTagger(vocabulary, registry, store,
                new FakeModels { DelayForeverAtStage = "semantic-tags" })
            .RunAsync(new() { MaxAttempts = 2, TimeBudget = TimeSpan.FromMilliseconds(150) });
        var checkpoint = store.Read<TaggingState>("tags", "noun:1")!;
        var resumed = await new SemanticTagger(vocabulary, registry, store, new FakeModels())
            .RunAsync(new() { MaxAttempts = 2 });

        Assert.True(limited.Paused);
        Assert.Equal("pending", checkpoint.Evaluations["place"].Status);
        Assert.Equal(0, checkpoint.Evaluations["place"].Attempts);
        Assert.Equal(1, resumed.Completed);
        Assert.Equal("does-not-match", store.Read<TaggingState>("tags", "noun:1")!.Evaluations["place"].Status);
    }

    [Fact]
    public async Task Child_match_with_negative_parent_is_reviewed_and_excluded_from_mapping()
    {
        using var temp = new TemporaryDirectory();
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var registry = new SemanticRegistry(GenkiTestData.Tags(("physical", []), ("building", ["physical"])));
        var store = new BatchStore(temp.Path);
        var models = new FakeModels { TagDecisions = new Dictionary<string, string>
        {
            ["physical"] = "does-not-match", ["building"] = "matches"
        } };

        var summary = await new SemanticTagger(vocabulary, registry, store, models)
            .RunAsync(new() { MaxAttempts = 1 });
        var state = store.Read<TaggingState>("tags", "noun:1")!;
        var mapping = new SemanticTagger(vocabulary, registry, store, models).ExportMapping();

        Assert.Equal(1, summary.NeedsReview);
        Assert.True(state.NeedsReview);
        Assert.Contains("physical", state.ReviewReason);
        Assert.Empty(mapping);
    }

    [Fact]
    public async Task New_tag_assignment_resumes_bank_and_processes_only_new_candidates()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema("g01-01", new() { AllOf = ["food"] });
        var tags = GenkiTestData.Tags(("food", []));
        var catalog = GenkiTestData.Catalog(schema, tags);
        var first = GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" });
        var second = GenkiTestData.Entry("noun",
            new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" });
        var vocabulary = new GenkiVocabulary([first, second]);
        WordTags Assignment(VocabularyEntry entry) => new()
        {
            Word = entry.Ref, WordFingerprint = entry.Fingerprint, DirectTags = ["food"]
        };
        var store = new BatchStore(temp.Path);
        var models = new FakeModels();
        var firstBatch = new QuestionPipeline(catalog, vocabulary,
            new CandidateEnumerator(catalog, vocabulary, [Assignment(first)]), store, models);
        var firstSummary = await firstBatch.RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 0 });
        var firstQuestionCalls = models.TotalQwenCalls;
        var firstTranslationCalls = models.TranslationCalls;

        var expandedBatch = new QuestionPipeline(catalog, vocabulary,
            new CandidateEnumerator(catalog, vocabulary, [Assignment(first), Assignment(second)]), store, models);
        var expandedSummary = await expandedBatch.RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 0 });

        Assert.Equal(1, firstSummary.Completed);
        Assert.Equal(3, expandedSummary.Completed);
        Assert.Equal(1, expandedSummary.Reused);
        Assert.Equal(12, models.TotalQwenCalls - firstQuestionCalls);
        Assert.Equal(3, models.TranslationCalls - firstTranslationCalls);
        Assert.Equal(4, store.ReadAll<QuestionState>("questions").Count(s => s.Status == "completed"));
    }

    [Fact]
    public async Task Alternatives_with_unknown_words_or_semantic_drift_are_not_published_and_fixed_words_remain_required()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema() with
        {
            Segments =
            [
                new() { Kind = "fixed", Word = new("noun", "1") }, new() { Kind = "literal", Text = "は" },
                new() { Kind = "slot", Name = "predicate" }, new() { Kind = "literal", Text = "です。" }
            ],
            Slots = new Dictionary<string, GenkiSlot>
            {
                ["predicate"] = new() { WordTypes = ["noun"], Tags = new() { AllOf = ["target"] } }
            }
        };
        var catalog = GenkiTestData.Catalog(schema, GenkiTestData.Tags(("target", [])));
        var vocabulary = new GenkiVocabulary(
        [
            GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" }),
            GenkiTestData.Entry("noun", new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" })
        ]);
        var store = new BatchStore(temp.Path);
        var selected = vocabulary.Resolve(new("noun", "2"));
        var assignment = new WordTags { Word = selected.Ref, WordFingerprint = selected.Fingerprint, DirectTags = ["target"] };
        var models = new FakeModels
        {
            Alternatives =
            [
                new() { Japanese = "未知語は大学です。", GrammarIds = ["g01-01"], Segments =
                    [new() { Kind = "fixed", Word = new("noun", "999") }, new() { Kind = "literal", Text = "は" }, new() { Kind = "fixed", Word = new("noun", "1") }, new() { Kind = "literal", Text = "です。" }] },
                new() { Japanese = "学校は大学です。", GrammarIds = ["g01-01"], Segments =
                    [new() { Kind = "fixed", Word = new("noun", "2") }, new() { Kind = "literal", Text = "は" }, new() { Kind = "fixed", Word = new("noun", "1") }, new() { Kind = "literal", Text = "です。" }] }
            ],
            EquivalenceOutcome = "invalid"
        };
        var summary = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, [assignment]), store, models)
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 2 });
        var completed = Assert.Single(store.ReadAll<QuestionState>("questions").Where(s => s.Status == "completed"));
        var bank = Path.Combine(temp.Path, "questions.jsonl");
        store.ExportBank(bank);
        var published = File.ReadAllLines(bank).Select(line => JsonSerializer.Deserialize<GenkiQuestion>(line, GenkiJson.Options)!).ToArray();

        Assert.Equal(1, summary.Completed);
        Assert.Contains(new WordRef("noun", "1"), completed.Question!.Answers[0].RequiredWords);
        Assert.Contains(completed.AlternativeExclusions.Values, reason => reason.Contains("Unknown vocabulary", StringComparison.Ordinal));
        Assert.Single(published);
        Assert.Single(published[0].Answers);
    }

    [Fact]
    public async Task Null_alternative_proposal_fails_retryably_without_caching_or_publishing()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        var first = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store,
                new FakeModels { MalformAlternativesResponse = true })
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 1 });
        var checkpoint = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var bank = Path.Combine(temp.Path, "questions.jsonl");
        store.ExportBank(bank);

        Assert.Equal(1, first.Failed);
        Assert.Equal("failed", checkpoint.Status);
        Assert.Equal("E-alternatives", checkpoint.ActiveStage);
        Assert.False(checkpoint.Stages.ContainsKey("E-alternatives"));
        Assert.Empty(File.ReadAllLines(bank));

        var retry = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, new FakeModels())
            .RunAsync([schema], new() { MaxAttempts = 2, AlternativeLimit = 1 });
        store.ExportBank(bank);
        Assert.Equal(1, retry.Completed);
        Assert.Single(File.ReadAllLines(bank));
    }

    [Theory]
    [InlineData("children")]
    [InlineData("formChoices")]
    public async Task Null_nested_alternative_fields_fail_retryably_without_caching(string nullField)
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        var raw = $"{{\"alternatives\":[{{\"japanese\":\"大学は大学です。\",\"grammarIds\":[\"g01-01\"],\"segments\":[{{\"kind\":\"literal\",\"text\":\"は\",\"{nullField}\":null}}]}}]}}";

        var summary = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store,
                new FakeModels { RawAlternativesResponse = raw })
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 1 });
        var checkpoint = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var bank = Path.Combine(temp.Path, "questions.jsonl");
        store.ExportBank(bank);

        Assert.Equal(1, summary.Failed);
        Assert.Equal("failed", checkpoint.Status);
        Assert.Equal("E-alternatives", checkpoint.ActiveStage);
        Assert.False(checkpoint.Stages.ContainsKey("E-alternatives"));
        Assert.Empty(File.ReadAllLines(bank));
    }

    [Fact]
    public async Task Identical_japanese_with_different_word_dependencies_keeps_both_answers()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary(
        [
            GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" }),
            GenkiTestData.Entry("noun", new Word { Id = 2, Japanese = "大学", Kana = "だいがく", English = "university" })
        ]);
        var enumerator = new CandidateEnumerator(catalog, vocabulary, []);
        var target = Assert.Single(enumerator.Enumerate(schema).Where(candidate =>
            candidate.Bindings["topic"] == new WordRef("noun", "1") &&
            candidate.Bindings["predicate"] == new WordRef("noun", "1")));
        var sameSurfaceAlternative = new AlternativeProposal
        {
            Japanese = "大学は大学です。", GrammarIds = ["g01-01"], Segments =
            [
                new() { Kind = "fixed", Word = new("noun", "2") }, new() { Kind = "literal", Text = "は" },
                new() { Kind = "fixed", Word = new("noun", "2") }, new() { Kind = "literal", Text = "です。" }
            ]
        };
        var store = new BatchStore(temp.Path);
        var result = await new QuestionPipeline(catalog, vocabulary, enumerator, store,
                new FakeModels { Alternatives = [sameSurfaceAlternative] })
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 1 });
        var state = store.Read<QuestionState>("questions", target.Id)!;
        var question = state.Question!;

        Assert.Equal(4, result.Completed);
        Assert.Equal(2, question.Answers.Count);
        Assert.Equal(question.Answers[0].Japanese, question.Answers[1].Japanese);
        Assert.Equal(new WordRef("noun", "1"), Assert.Single(question.Answers[0].RequiredWords));
        Assert.Equal(new WordRef("noun", "2"), Assert.Single(question.Answers[1].RequiredWords));
        Assert.True(GenkiPracticeService.Eligible(question, new HashSet<WordRef> { new("noun", "1") }, new HashSet<string> { "g01-01" }));
        Assert.True(GenkiPracticeService.Eligible(question, new HashSet<WordRef> { new("noun", "2") }, new HashSet<string> { "g01-01" }));
    }

    [Theory]
    [InlineData("context")]
    [InlineData("translation-validation")]
    public async Task Invalid_context_or_translation_judgment_requires_review(string stage)
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        var models = stage == "context"
            ? new FakeModels { ContextOutcome = "invalid" }
            : new FakeModels { TranslationValidationOutcome = "invalid" };
        var summary = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, models)
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 0 });
        var state = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var bank = Path.Combine(temp.Path, "questions.jsonl");
        store.ExportBank(bank);

        Assert.Equal(1, summary.NeedsReview);
        Assert.Equal("needs-review", state.Status);
        Assert.Null(state.Question);
        Assert.Empty(File.ReadAllLines(bank));
    }

    [Fact]
    public async Task Time_budget_pauses_and_restart_reuses_prior_stages()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        var limitedModels = new FakeModels { DelayForeverAtStage = "context" };
        var limited = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, limitedModels)
            .RunAsync([schema], new() { MaxAttempts = 2, AlternativeLimit = 0, TimeBudget = TimeSpan.FromMilliseconds(150) });
        var checkpoint = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var resumedModels = new FakeModels();
        var resumed = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, resumedModels)
            .RunAsync([schema], new() { MaxAttempts = 2, AlternativeLimit = 0 });

        Assert.True(limited.Paused);
        Assert.Contains("B-candidate-validation", checkpoint.Stages.Keys);
        Assert.Equal("pending", checkpoint.Status);
        Assert.Equal(0, resumedModels.QwenCalls.GetValueOrDefault("candidate-validation"));
        Assert.Equal(1, resumed.Completed);
    }

    [Fact]
    public async Task Cancellation_and_restart_reuse_successful_stages_without_retranslating()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        using var cancellation = new CancellationTokenSource();
        var interruptedModels = new FakeModels { CancelAtStage = "translation-validation", Cancellation = cancellation };
        var interruptedPipeline = new QuestionPipeline(catalog, vocabulary,
            new CandidateEnumerator(catalog, vocabulary, []), store, interruptedModels);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interruptedPipeline.RunAsync(
            [schema], new() { MaxAttempts = 2, AlternativeLimit = 0 }, cancellation.Token));
        var checkpoint = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var resumedModels = new FakeModels();
        var resumed = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, resumedModels)
            .RunAsync([schema], new() { MaxAttempts = 2, AlternativeLimit = 0 });

        Assert.Contains("D-translation", checkpoint.Stages.Keys);
        Assert.Equal(1, interruptedModels.TranslationCalls);
        Assert.Equal(0, resumedModels.TranslationCalls);
        Assert.Equal(0, resumedModels.QwenCalls.GetValueOrDefault("candidate-validation"));
        Assert.Equal(0, resumedModels.QwenCalls.GetValueOrDefault("context"));
        Assert.Equal(1, resumed.Completed);
    }

    [Fact]
    public async Task Transport_failure_is_retryable_and_never_published_as_linguistic_rejection()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        var failing = new FakeModels { ThrowAtStage = "candidate-validation" };
        var first = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, failing)
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 0 });
        var checkpoint = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var bank = Path.Combine(temp.Path, "questions.jsonl");
        store.ExportBank(bank);

        Assert.Equal(1, first.Failed);
        Assert.Equal("failed", checkpoint.Status);
        Assert.DoesNotContain("rejected", checkpoint.Status, StringComparison.Ordinal);
        Assert.Equal("B-candidate-validation", checkpoint.ActiveStage);
        Assert.Empty(File.ReadAllLines(bank));

        var retry = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store, new FakeModels())
            .RunAsync([schema], new() { MaxAttempts = 2, AlternativeLimit = 0 });
        store.ExportBank(bank);
        Assert.Equal(1, retry.Completed);
        Assert.Single(File.ReadAllLines(bank));
    }

    [Fact]
    public async Task Uncertain_kana_leaves_candidate_for_review_and_out_of_published_bank()
    {
        using var temp = new TemporaryDirectory();
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary([GenkiTestData.Entry("noun",
            new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" })]);
        var store = new BatchStore(temp.Path);
        var summary = await new QuestionPipeline(catalog, vocabulary,
                new CandidateEnumerator(catalog, vocabulary, []), store,
                new FakeModels { KanaOutcome = "uncertain" })
            .RunAsync([schema], new() { MaxAttempts = 1, AlternativeLimit = 0 });
        var state = Assert.Single(store.ReadAll<QuestionState>("questions"));
        var bank = Path.Combine(temp.Path, "questions.jsonl");
        store.ExportBank(bank);

        Assert.Equal(1, summary.NeedsReview);
        Assert.Equal("needs-review", state.Status);
        Assert.Null(state.Question);
        Assert.Empty(File.ReadAllLines(bank));
    }
}

internal sealed class FakeModels : IGenkiModels
{
    public Dictionary<string, int> QwenCalls { get; } = new(StringComparer.Ordinal);
    public int TotalQwenCalls => QwenCalls.Values.Sum();
    public int TranslationCalls { get; private set; }
    public string Provenance => "controlled-test-models-v1";
    public string TagOutcome { get; init; } = "does-not-match";
    public bool MalformTagResponses { get; init; }
    public bool MalformAlternativesResponse { get; init; }
    public string? RawAlternativesResponse { get; init; }
    public IReadOnlyDictionary<string, string>? TagDecisions { get; init; }
    public IReadOnlyList<AlternativeProposal> Alternatives { get; init; } = [];
    public string EquivalenceOutcome { get; init; } = "valid";
    public string ContextOutcome { get; init; } = "valid";
    public string TranslationValidationOutcome { get; init; } = "valid";
    public string KanaOutcome { get; init; } = "valid";
    public string? ThrowAtStage { get; init; }
    public string? CancelAtStage { get; init; }
    public CancellationTokenSource? Cancellation { get; init; }
    public string? DelayForeverAtStage { get; init; }

    public Task<string> QwenAsync(string stage, string systemPrompt, string inputJson, CancellationToken cancellationToken)
    {
        QwenCalls[stage] = QwenCalls.GetValueOrDefault(stage) + 1;
        if (stage == DelayForeverAtStage) return DelayForever(cancellationToken);
        if (stage == CancelAtStage && Cancellation is not null)
        {
            Cancellation.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
        if (stage == ThrowAtStage) throw new IOException("controlled model transport failure");
        if (stage == "semantic-tags")
        {
            if (MalformTagResponses) return Task.FromResult("{\"ambiguous\":false,\"reason\":\"\",\"evaluations\":[]}");
            using var document = JsonDocument.Parse(inputJson);
            var decisions = document.RootElement.GetProperty("tags").EnumerateArray().Select(tag =>
            {
                var id = tag.GetProperty("id").GetString()!;
                var outcome = TagDecisions is not null && TagDecisions.TryGetValue(id, out var selected) ? selected : TagOutcome;
                return new TagDecision(id, outcome, "controlled fixture decision");
            }).ToArray();
            return Task.FromResult(JsonSerializer.Serialize(new TagResponse(false, "", decisions), GenkiJson.Options));
        }
        if (stage is "candidate-validation" or "alternative-validation")
            return Task.FromResult(JudgmentJson("valid"));
        if (stage == "translation-validation") return Task.FromResult(JudgmentJson(TranslationValidationOutcome));
        if (stage == "equivalence") return Task.FromResult(JudgmentJson(EquivalenceOutcome));
        if (stage == "context")
            return Task.FromResult(JsonSerializer.Serialize(new ContextResponse
            {
                Setting = null, Judgment = new() { Outcome = ContextOutcome, Reason = "controlled context judgment", GrammarIds = ["g01-01"] }
            }, GenkiJson.Options));
        if (stage == "alternatives" && RawAlternativesResponse is { } rawAlternatives)
            return Task.FromResult(rawAlternatives);
        if (stage == "alternatives" && MalformAlternativesResponse)
            return Task.FromResult("{\"alternatives\":[null]}");
        if (stage == "alternatives")
            return Task.FromResult(JsonSerializer.Serialize(new AlternativesResponse { Alternatives = Alternatives }, GenkiJson.Options));
        if (stage == "kana")
        {
            using var document = JsonDocument.Parse(inputJson);
            var kana = document.RootElement.GetProperty("expectedReading").GetString()!;
            return Task.FromResult(JsonSerializer.Serialize(new KanaResponse
            {
                Outcome = KanaOutcome, Kana = kana, Reason = "controlled reading anchor"
            }, GenkiJson.Options));
        }
        throw new InvalidOperationException($"Unexpected test model stage '{stage}'.");
    }

    public Task<string> TranslateAsync(string japanese, string? setting, string register, CancellationToken cancellationToken)
    {
        TranslationCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("A university.");
    }

    private static string JudgmentJson(string outcome) => JsonSerializer.Serialize(new Judgment
    {
        Outcome = outcome, Reason = $"controlled {outcome} decision", GrammarIds = ["g01-01"]
    }, GenkiJson.Options);

    private static async Task<string> DelayForever(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return string.Empty;
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "genki-test-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
