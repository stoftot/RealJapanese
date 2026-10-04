using DataLoaders.Models;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Repositories.Genki;

namespace RealJapanese.Tests;

/// <summary>Checks curriculum coverage, schema boundaries, small Cartesian products and stored-answer eligibility.</summary>
public sealed class GenkiTests
{
    [Fact]
    public void Embedded_curriculum_has_complete_expected_point_coverage()
    {
        var catalog = new GenkiCatalog();
        var expectedCounts = new[] { 3, 8, 8, 8, 6, 7, 6, 10, 8, 9, 7, 6 };

        Assert.Equal(12, catalog.Lessons.Count);
        Assert.Equal(expectedCounts, catalog.Lessons.OrderBy(x => x.Number)
            .Select(x => x.GrammarPoints.Count).ToArray());
        Assert.Equal(expectedCounts.Sum(), catalog.Lessons.Sum(x => x.GrammarPoints.Count));
        foreach (var point in catalog.Lessons.SelectMany(x => x.GrammarPoints))
        {
            Assert.False(string.IsNullOrWhiteSpace(point.SourcePages));
            Assert.False(string.IsNullOrWhiteSpace(point.Meaning));
            Assert.False(string.IsNullOrWhiteSpace(point.Formation));
            Assert.NotEmpty(point.Notes);
            Assert.All(point.Notes, note => Assert.False(string.IsNullOrWhiteSpace(note)));
            Assert.NotEmpty(catalog.ForPoint(point.Id));
            Assert.All(catalog.ForPoint(point.Id), schema => Assert.Equal(point.Id, schema.GrammarPointId));
        }
    }

    [Fact]
    public void Catalog_rejects_unknown_segment_forms_and_future_prerequisites()
    {
        var valid = GenkiTestData.NounPredicateSchema();
        var unknownForm = valid with
        {
            Segments = [valid.Segments[0] with { Form = "made-up-form" }, .. valid.Segments.Skip(1)]
        };
        Assert.Throws<InvalidDataException>(() => GenkiTestData.Catalog(unknownForm));

        var first = GenkiTestData.NounPredicateSchema("g01-01") with { PrerequisiteGrammarIds = ["g01-02"] };
        var later = GenkiTestData.NounPredicateSchema("g01-02");
        Assert.Throws<InvalidDataException>(() => GenkiTestData.Catalog([first, later]));
        first = first with { PrerequisiteGrammarIds = [] };
        var pair = GenkiTestData.Catalog([first, later]);
        var firstPoint = pair.Lessons.Single().GrammarPoints[0] with { Prerequisites = ["g01-02"] };
        var lesson = pair.Lessons.Single() with
        {
            GrammarPoints = [firstPoint, .. pair.Lessons.Single().GrammarPoints.Skip(1)]
        };

        Assert.Throws<InvalidDataException>(() => new GenkiCatalog([lesson], [first, later], [],
            pair.Materials.ToArray()));
    }

    [Fact]
    public void Word_reference_keeps_shared_numeric_ids_distinct_by_collection()
    {
        var vocabulary = new GenkiVocabulary(
        [
            GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" }),
            GenkiTestData.Entry("verb", new Verb { Id = 1, Japanese = "行く", Kana = "いく", English = "to go", Type = "u" })
        ]);

        Assert.Equal(2, vocabulary.Entries.Count);
        Assert.Equal("大学", vocabulary.Resolve(new("noun", "1")).Word.Japanese);
        Assert.Equal("行く", vocabulary.Resolve(new("verb", "1")).Word.Japanese);
        Assert.NotEqual(new WordRef("noun", "1"), new WordRef("verb", "1"));
    }

    [Fact]
    public void Tag_hierarchy_supports_multiple_parents_and_filter_semantics()
    {
        var registry = new SemanticRegistry(GenkiTestData.Tags(
            ("physical", []), ("edible", []), ("food", ["physical", "edible"]), ("missing-result", [])));

        Assert.Contains("physical", registry.Expand(["food"]));
        Assert.Contains("edible", registry.Expand(["food"]));
        Assert.DoesNotContain("food", registry.Expand(["physical"]));
        Assert.True(registry.Matches(new() { AllOf = ["physical", "edible"] }, ["food"]));
        Assert.True(registry.Matches(new() { AnyOf = ["food", "edible"] }, ["food"]));
        Assert.False(registry.Matches(new() { AnyOf = ["missing-result", "physical"] }, ["edible"]));
        Assert.False(registry.Matches(new() { NoneOf = ["physical"] }, ["food"]));
        Assert.True(registry.Matches(new(), []));
        Assert.Throws<InvalidDataException>(() => registry.Matches(new() { AllOf = ["not-registered"] }, []));
    }

    [Fact]
    public void Tag_registry_rejects_missing_parents_and_cycles()
    {
        Assert.Throws<InvalidDataException>(() => new SemanticRegistry(GenkiTestData.Tags(("food", ["missing"]))));
        Assert.Throws<InvalidDataException>(() => new SemanticRegistry(GenkiTestData.Tags(
            ("first", ["second"]), ("second", ["first"]))));
    }

    [Fact]
    public void Exhaustive_enumerator_covers_named_two_by_two_bindings_with_stable_ids()
    {
        var schema = GenkiTestData.NounPredicateSchema();
        var catalog = GenkiTestData.Catalog(schema);
        var words = new[]
        {
            GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" }),
            GenkiTestData.Entry("noun", new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" })
        };

        var original = new CandidateEnumerator(catalog, new GenkiVocabulary(words), [])
            .Enumerate(schema).ToArray();
        var reordered = new CandidateEnumerator(catalog, new GenkiVocabulary(words.Reverse()), [])
            .Enumerate(schema).ToArray();

        Assert.Equal(4, original.Length);
        Assert.Equal(4, original.Select(x => x.Id).Distinct().Count());
        Assert.Equal(original.Select(x => x.Id).Order().ToArray(), reordered.Select(x => x.Id).Order().ToArray());
        Assert.Contains(original, x => x.Bindings["topic"] == new WordRef("noun", "1") &&
            x.Bindings["predicate"] == new WordRef("noun", "2"));
    }

    [Fact]
    public void Repeated_slot_reuses_one_binding_and_keeps_fixed_word_provenance()
    {
        var schema = GenkiTestData.NounPredicateSchema() with
        {
            Segments =
            [
                new() { Kind = "fixed", Word = new("noun", "1") },
                new() { Kind = "literal", Text = "は" },
                new() { Kind = "slot", Name = "topic" },
                new() { Kind = "literal", Text = "です。" }
            ],
            Slots = new Dictionary<string, GenkiSlot> { ["topic"] = new() { WordTypes = ["noun"] } }
        };
        var catalog = GenkiTestData.Catalog(schema);
        var vocabulary = new GenkiVocabulary(
        [
            GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" }),
            GenkiTestData.Entry("noun", new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" })
        ]);
        var candidates = new CandidateEnumerator(catalog, vocabulary, []).Enumerate(schema).ToArray();

        Assert.Equal(2, candidates.Length);
        var fixedCandidate = Assert.Single(candidates, c => c.Bindings["topic"] == new WordRef("noun", "2"));
        Assert.Contains(fixedCandidate.RequiredWords, w => w == new WordRef("noun", "1"));
        Assert.Contains(fixedCandidate.Segments, x => x.Kind == "fixed" && x.Word == new WordRef("noun", "1"));
        Assert.Equal("大学は学校です。", fixedCandidate.Japanese);
    }

    [Fact]
    public void Optional_groups_and_each_occurrence_form_choices_render_independently()
    {
        var optionalSchema = GenkiTestData.NounPredicateSchema() with
        {
            Segments =
            [
                new() { Kind = "optional", Children = [new() { Kind = "slot", Name = "topic" }, new() { Kind = "literal", Text = "は" }] },
                new() { Kind = "slot", Name = "predicate" },
                new() { Kind = "literal", Text = "です。" }
            ]
        };
        var optionalCatalog = GenkiTestData.Catalog(optionalSchema);
        var nouns = new GenkiVocabulary(
        [
            GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" }),
            GenkiTestData.Entry("noun", new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" })
        ]);
        var optionalCandidates = new CandidateEnumerator(optionalCatalog, nouns, []).Enumerate(optionalSchema).ToArray();
        Assert.Equal(6, optionalCandidates.Length);
        Assert.Contains(optionalCandidates, x => x.Japanese == "学校です。");
        Assert.Contains(optionalCandidates, x => x.Japanese == "大学は学校です。");
        Assert.Equal(6, optionalCandidates.Select(x => x.Id).Distinct().Count());

        var verbSchema = GenkiTestData.VerbFormSchema();
        var verbCatalog = GenkiTestData.Catalog(verbSchema);
        var verbs = new GenkiVocabulary(
        [GenkiTestData.Entry("verb", new Verb { Id = 1, Japanese = "行く", Kana = "いく", English = "to go", Type = "u" })]);
        var forms = new CandidateEnumerator(verbCatalog, verbs, []).Enumerate(verbSchema).ToArray();

        Assert.Equal(4, forms.Length);
        Assert.Contains(forms, x => x.Japanese == "行く行きます" && x.ExpectedKana == "いくいきます");
        Assert.Equal(4, forms.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void New_word_or_tag_assignment_adds_only_new_eligible_candidates()
    {
        var tag = GenkiTestData.Tags(("food", []));
        var schema = GenkiTestData.NounPredicateSchema("g01-01", new() { AllOf = ["food"] });
        var catalog = GenkiTestData.Catalog(schema, tag);
        var first = GenkiTestData.Entry("noun", new Word { Id = 1, Japanese = "大学", Kana = "だいがく", English = "university" });
        var second = GenkiTestData.Entry("noun", new Word { Id = 2, Japanese = "学校", Kana = "がっこう", English = "school" });
        var initialVocabulary = new GenkiVocabulary([first, second]);
        WordTags Assignment(VocabularyEntry entry) => new()
        {
            Word = entry.Ref, WordFingerprint = entry.Fingerprint, DirectTags = ["food"]
        };
        var before = new CandidateEnumerator(catalog, initialVocabulary, [Assignment(first)]).Enumerate(schema).ToArray();
        var afterTag = new CandidateEnumerator(catalog, initialVocabulary, [Assignment(first), Assignment(second)]).Enumerate(schema).ToArray();
        var afterWordVocabulary = new GenkiVocabulary([.. initialVocabulary.Entries,
            GenkiTestData.Entry("noun", new Word { Id = 3, Japanese = "図書館", Kana = "としょかん", English = "library" })]);
        var afterWord = new CandidateEnumerator(catalog, afterWordVocabulary,
            afterWordVocabulary.Entries.Select(Assignment)).Enumerate(schema).ToArray();

        Assert.Single(before);
        Assert.Equal(4, afterTag.Length);
        Assert.Equal(9, afterWord.Length);
        Assert.Single(before.Select(x => x.Id).Intersect(afterTag.Select(x => x.Id)));
        Assert.All(afterTag.Where(x => !before.Any(old => old.Id == x.Id)), x => Assert.NotNull(x.InputFingerprint));
    }

    [Fact]
    public void Practice_accepts_one_known_answer_and_compares_approved_answers_conservatively()
    {
        var question = new GenkiQuestion
        {
            Id = "question-fixture", SchemaId = "g01-01-noun-predicate", GrammarPointId = "g01-01",
            English = "A university.", Register = "polite", RequiredGrammar = ["g01-01"],
            Answers =
            [
                new() { Japanese = "大学は大学です。", Kana = "だいがくはだいがくです。", RequiredWords = [new("noun", "1")] },
                new() { Japanese = "学校は学校です。", Kana = "がっこうはがっこうです。", RequiredWords = [new("noun", "2")] }
            ]
        };
        var known = new HashSet<WordRef> { new("noun", "1") };
        var exercise = new GenkiExercise { Question = question, DisplayAnswers = [question.Answers[0]] };

        Assert.True(GenkiPracticeService.Eligible(question, known, new HashSet<string> { "g01-01" }));
        Assert.True(exercise.MatchesModel("  大学は大学です ! "));
        Assert.True(exercise.MatchesModel("だいがくはだいがくです。"));
        Assert.True(exercise.MatchesModel("学校は学校です"));
        Assert.False(exercise.MatchesModel("大学 大学です"));
        Assert.False(exercise.MatchesModel("大学は大学でした。"));
    }
}

internal static class GenkiTestData
{
    public static VocabularyEntry Entry(string wordType, Word word) => new(new(wordType, word.Id.ToString()), word);

    public static SemanticTag[] Tags(params (string Id, string[] Parents)[] tags) => tags.Select(x => new SemanticTag
    {
        Id = x.Id, Name = x.Id, Description = $"Test classification {x.Id}.", Parents = x.Parents
    }).ToArray();

    public static GenkiSchema NounPredicateSchema(string grammarPointId = "g01-01", TagFilter? filter = null) => new()
    {
        Id = grammarPointId + "-noun-predicate", GrammarPointId = grammarPointId, Register = "polite",
        Tense = "nonpast", Polarity = "affirmative",
        Segments =
        [
            new() { Kind = "slot", Name = "topic" }, new() { Kind = "literal", Text = "は" },
            new() { Kind = "slot", Name = "predicate" }, new() { Kind = "literal", Text = "です。" }
        ],
        Slots = new Dictionary<string, GenkiSlot>
        {
            ["topic"] = new() { WordTypes = ["noun"], Tags = filter ?? new() },
            ["predicate"] = new() { WordTypes = ["noun"], Tags = filter ?? new() }
        },
        ValidationRules = ["Test-only schema fixture."]
    };

    public static GenkiSchema VerbFormSchema() => new()
    {
        Id = "g03-01-verb-form-fixture", GrammarPointId = "g03-01", Register = "polite",
        Tense = "nonpast", Polarity = "affirmative",
        Segments =
        [
            new() { Kind = "slot", Name = "verb", FormChoices = ["base", "polite"] },
            new() { Kind = "slot", Name = "verb", FormChoices = ["base", "polite"] }
        ],
        Slots = new Dictionary<string, GenkiSlot> { ["verb"] = new() { WordTypes = ["verb"] } },
        ValidationRules = ["Test-only schema fixture."]
    };

    public static GenkiCatalog Catalog(GenkiSchema schema, SemanticTag[]? tags = null) => Catalog([schema], tags);

    public static GenkiCatalog Catalog(GenkiSchema[] schemas, SemanticTag[]? tags = null)
    {
        var pointIds = schemas.Select(x => x.GrammarPointId).Distinct().Order(StringComparer.Ordinal).ToArray();
        var points = pointIds.Select(id => new GenkiGrammarPoint
        {
            Id = id, Title = "test grammar point", SourcePages = "fixture", Meaning = "Fixture meaning.", Formation = "Fixture pattern."
        }).ToArray();
        var lessonNumber = int.Parse(pointIds[0].AsSpan(1, 2));
        var lesson = new GenkiLesson
        {
            Number = lessonNumber, Title = "Test fixture lesson", SourcePages = "fixture", GrammarPoints = points
        };
        var materials = schemas.SelectMany(schema => Flatten(schema.Segments)
            .Where(segment => segment.Kind == "literal")
            .Select(segment => new GrammarMaterial(segment.Text!, schema.GrammarPointId))).Distinct().ToArray();
        return new GenkiCatalog([lesson], schemas, tags ?? [], materials);
    }

    private static IEnumerable<GenkiSegment> Flatten(IEnumerable<GenkiSegment> segments)
    {
        foreach (var segment in segments)
        {
            yield return segment;
            if (segment.Kind == "optional")
                foreach (var child in Flatten(segment.Children)) yield return child;
        }
    }
}
