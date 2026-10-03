using DataLoaders.Models;
using DataLoaders.Models.Genki;
using Repositories.Genki;

namespace RealJapanese.Tests;

/// <summary>Protects textbook coverage, cumulative gating, safe substitution and model-answer comparison.</summary>
public sealed class GenkiTests
{
    [Theory]
    [InlineData("行く", "te", "行って", 6)]
    [InlineData("読む", "te", "読んで", 6)]
    [InlineData("読む", "past", "読んだ", 9)]
    [InlineData("食べる", "past", "食べた", 9)]
    [InlineData("ある", "negative", "ない", 8)]
    [InlineData("いい", "politeNegative", "よくないです", 5)]
    [InlineData("いい", "te", "よくて", 7)]
    [InlineData("静か", "te", "静かで", 7)]
    public void Reviewed_forms_follow_independent_language_oracles(string lemma, string form, string expected, int firstLesson)
    {
        var forms = new GenkiCatalog().Vocabulary.Where(w => w.Japanese == lemma && w.Forms.ContainsKey(form))
            .Select(w => w.Forms[form]).ToArray();
        Assert.NotEmpty(forms);
        Assert.All(forms, actual =>
        {
            Assert.Equal(expected, actual.Japanese);
            Assert.True(actual.IntroducedLesson >= firstLesson);
        });
    }

    [Fact]
    public void Every_numbered_textbook_section_has_recap_and_production_patterns()
    {
        var catalog = new GenkiCatalog();
        // Independent inventory from the supplied third-edition contents, printed pp. 6–10.
        int[] sectionCounts = [3, 7, 8, 8, 6, 7, 6, 8, 7, 7, 4, 6];
        Assert.Equal(Enumerable.Range(1, 12), catalog.Lessons.Select(x => x.Number));
        foreach (var lesson in catalog.Lessons)
        {
            for (var section = 1; section <= sectionCounts[lesson.Number - 1]; section++)
            {
                var point = Assert.Single(lesson.GrammarPoints, x => x.Id == $"g{lesson.Number:00}-{section:00}");
                Assert.NotEmpty(point.Meaning);
                Assert.NotEmpty(point.Formation);
                Assert.NotEmpty(point.Examples);
                Assert.Contains(point.Schemas, x => x.Kind == "translation");
            }
        }
    }

    [Fact]
    public void Every_pattern_generates_at_its_own_lesson_and_with_expanded_vocabulary()
    {
        var catalog = new GenkiCatalog();
        var generator = new GenkiGenerator(catalog);
        foreach (var lesson in catalog.Lessons)
        foreach (var point in lesson.GrammarPoints)
        foreach (var schema in point.Schemas)
        {
            foreach (var slot in schema.Slots)
                Assert.True(catalog.Vocabulary.Count(w => GenkiGenerator.IsEligible(w, slot, lesson.Number, lesson.Number)) >= 2,
                    $"{schema.Id}/{slot.Name} needs at least two eligible words.");
            var outputs = new HashSet<string>();
            foreach (var vocabularyLesson in new[] { lesson.Number, 12 }.Distinct())
            for (var seed = 0; seed < 32; seed++)
            {
                var exercise = generator.Generate(lesson.Number, point.Id, schema.Id, vocabularyLesson, new Random(seed));
                Assert.NotEmpty(exercise.QuestionAnswer.Question);
                Assert.NotEmpty(exercise.QuestionAnswer.Answer);
                Assert.DoesNotContain("{", exercise.QuestionAnswer.Question + exercise.QuestionAnswer.Answer + exercise.KanaAnswer + exercise.Context);
                Assert.True(exercise.MatchesModel(exercise.KanaAnswer));
                foreach (var slot in schema.Slots)
                    Assert.True(GenkiGenerator.IsEligible(exercise.Words[slot.Name], slot, lesson.Number, vocabularyLesson));
                foreach (var relation in schema.Relations)
                {
                    var left = exercise.Words[relation.Left]; var right = exercise.Words[relation.Right];
                    if (relation.Kind == "distinct") Assert.NotEqual(left.Japanese, right.Japanese);
                    else Assert.Contains(right.Tags, left.Accepts.Contains);
                }
                outputs.Add(exercise.QuestionAnswer.Answer);
            }
            Assert.True(outputs.Count > 1, $"{schema.Id} produces only one fixed sentence.");
        }
    }

    [Fact]
    public void Shipped_later_vocabulary_expands_patterns_in_every_earlier_lesson()
    {
        var catalog = new GenkiCatalog();
        foreach (var lesson in catalog.Lessons.Where(l => l.Number < 12))
        {
            var grows = lesson.GrammarPoints.SelectMany(p => p.Schemas).SelectMany(s => s.Slots).Any(slot =>
            {
                var original = catalog.Vocabulary.Where(w => GenkiGenerator.IsEligible(w, slot, lesson.Number, lesson.Number))
                    .Select(w => w.Japanese).ToHashSet();
                return catalog.Vocabulary.Any(w => GenkiGenerator.IsEligible(w, slot, lesson.Number, 12) &&
                    !original.Contains(w.Japanese));
            });
            Assert.True(grows, $"Lesson {lesson.Number}'s vocabulary ceiling must add new usable words.");
        }
    }

    [Fact]
    public void Vocabulary_growth_reuses_old_patterns_without_unlocking_future_forms()
    {
        var catalog = new GenkiCatalog();
        var generator = new GenkiGenerator(catalog);
        var lesson = catalog.Lessons[0]; var point = lesson.GrammarPoints[0]; var schema = point.Schemas[0];
        var slot = schema.Slots[0];
        var existing = catalog.Vocabulary.First(w => GenkiGenerator.IsEligible(w, slot, 1, 1));
        var added = GenkiLexeme.FromWord(new Word { Japanese = "新語", Kana = "しんご", English = "new vocabulary", Id = 999 },
            existing with { Key = "test-added", IntroducedLesson = 12 });
        Assert.False(GenkiGenerator.IsEligible(added, slot, 1, 1));
        Assert.True(GenkiGenerator.IsEligible(added, slot, 1, 12));
        Assert.Contains(Enumerable.Range(0, 100).Select(seed =>
            generator.Generate(1, point.Id, schema.Id, 12, new Random(seed), [added])),
            e => e.Words.Values.Any(w => w.Key == added.Key));
        var future = added with { Forms = new Dictionary<string, GenkiForm>
        { ["futureForm"] = new() { Japanese = "読んだ", Kana = "よんだ", English = "read", IntroducedLesson = 9 } } };
        Assert.False(GenkiGenerator.IsEligible(future, slot with { Forms = ["futureForm"] }, 1, 12));
        Assert.False(GenkiGenerator.IsEligible(added with { Tags = [] }, slot, 1, 12));
    }

    [Fact]
    public void Imported_dictionary_words_keep_reviewed_English_in_generated_requests()
    {
        var catalog = new GenkiCatalog();
        var lesson = catalog.FindLesson(6)!;
        var point = lesson.GrammarPoints.Single(p => p.Id == "g06-02");
        var schema = point.Schemas.Single(s => s.Id == "g06-02-request");
        var metadata = catalog.Vocabulary.Single(w => w.Key == "l10-pencil") with
        {
            Key = "test-imported-pencil", English = "a pencil",
            Tags = ["l06-object", "l06-tool", "test-imported-pencil"]
        };
        var imported = GenkiLexeme.FromWord(new Word
            { Id = 999, Japanese = "鉛筆", Kana = "えんぴつ", English = "pencil" }, metadata);
        // Restrict only the object pool so this request must exercise the imported word.
        var request = schema with { Slots = schema.Slots.Select(s => s.Name == "object"
            ? s with { Tags = ["test-imported-pencil"] } : s).ToArray() };
        var changedPoint = point with { Schemas = point.Schemas.Select(s => s == schema ? request : s).ToArray() };
        var changedLesson = lesson with
            { GrammarPoints = lesson.GrammarPoints.Select(p => p == point ? changedPoint : p).ToArray() };
        var generator = new GenkiGenerator(new GenkiCatalog(catalog.Lessons
            .Select(l => l == lesson ? changedLesson : l).ToArray()));

        var exercise = generator.Generate(6, point.Id, schema.Id, 10, new Random(1), [imported]);

        Assert.Equal(999, exercise.Words["object"].Id);
        Assert.Equal($"Please {exercise.Words["action"].English} a pencil.", exercise.QuestionAnswer.Question);
        Assert.StartsWith("鉛筆を", exercise.QuestionAnswer.Answer);
    }

    [Fact]
    public void Invalid_dependencies_and_templates_fail_closed()
    {
        var catalog = new GenkiCatalog();
        var first = catalog.Lessons[0]; var point = first.GrammarPoints[0];
        GenkiLesson Changed(GenkiGrammarPoint changed) => first with
        { GrammarPoints = first.GrammarPoints.Select(p => p == point ? changed : p).ToArray() };
        Assert.Throws<InvalidDataException>(() => new GenkiCatalog(catalog.Lessons.Select(l => l == first
            ? Changed(point with { Prerequisites = ["g12-01"] }) : l).ToArray()));
        Assert.Throws<InvalidDataException>(() => new GenkiCatalog(catalog.Lessons.Select(l => l == first
            ? Changed(point with { Schemas = [point.Schemas[0] with { Japanese = "{unknown}" }] }) : l).ToArray()));
    }

    [Fact]
    public void Model_matching_ignores_spacing_and_punctuation_but_preserves_grammar()
    {
        var catalog = new GenkiCatalog(); var point = catalog.Lessons[0].GrammarPoints[0];
        var exercise = new GenkiGenerator(catalog).Generate(1, point.Id, point.Schemas[0].Id, 1, new Random(1));
        Assert.True(exercise.MatchesModel("  " + exercise.KanaAnswer.TrimEnd('。') + " ! "));
        Assert.False(exercise.MatchesModel(""));
        Assert.False(exercise.MatchesModel(exercise.KanaAnswer.Replace("です", "じゃないです")));
    }
}
