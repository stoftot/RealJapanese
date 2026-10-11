using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Words;
using Repositories.Exstensions;
using System.Text.Json;

namespace RealJapanese.ComponentTests;

/// <summary>Exercises chunk boundaries and retry transitions with small training catalogs.</summary>
public sealed class RetryChunkStressComponentTests
{
    [Fact]
    public void TypedPracticePartitionsTrainingWordsAcrossEverySplitFromOneToTen()
    {
        using var test = new ComponentTestContext();
        var words = SeedTrainingWords(test, 5);
        var answers = words.ToDictionary(word => word.English, word => word.Kana.ToRomaji(), StringComparer.Ordinal);
        var cut = RenderSpelling(test);

        for (var split = 1; split <= 10; split++)
        {
            SetSplit(cut, split);
            var observed = new HashSet<string>(StringComparer.Ordinal);
            for (var chunk = 1; chunk <= split; chunk++)
            {
                SetChunk(cut, chunk);
                var expected = ExpectedChunk(words.Select(word => word.English).ToList(), split, chunk - 1);
                if (expected.Count == 0)
                {
                    Assert.Contains("Nothing to practise yet", cut.Markup);
                    continue;
                }

                Assert.Equal(expected.Count, Total(cut));
                var missedQuestion = CurrentQuestion(cut);
                Click(cut, "Show answer (Enter)");
                Click(cut, "Show answer (Enter)");
                Click(cut, "Next");
                for (var index = 1; index < expected.Count; index++) Click(cut, "Next");

                Assert.Equal(missedQuestion, CurrentQuestion(cut));
                Assert.Equal(1, Total(cut));
                Click(cut, "Show answer (Enter)");
                Click(cut, "Show answer (Enter)");
                Click(cut, "Next");
                Assert.Equal(missedQuestion, CurrentQuestion(cut));
                Assert.Equal(1, Total(cut));
                cut.Find("input[placeholder]").Input(answers[missedQuestion]);
                cut.WaitForAssertion(() => Assert.Equal(expected.Count, Total(cut)));

                var chunkQuestions = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < expected.Count; index++)
                {
                    Assert.True(chunkQuestions.Add(CurrentQuestion(cut)));
                    Click(cut, "Next");
                }
                Assert.Equal(expected.Order(), chunkQuestions.Order());
                foreach (var question in chunkQuestions)
                    Assert.True(observed.Add(question), $"Question '{question}' appeared in multiple chunks.");
            }
            Assert.Equal(words.Select(word => word.English).Order(), observed.Order());
        }
    }

    [Fact]
    public void TypedRetryIsUniqueCanRepeatAfterAnotherMissAndRestoresFullChunk()
    {
        using var test = new ComponentTestContext();
        var words = SeedTrainingWords(test, 3);
        var answers = words.ToDictionary(word => word.English, word => word.Kana.ToRomaji(), StringComparer.Ordinal);
        var cut = RenderSpelling(test);
        var initialTotal = Total(cut);
        var missedQuestion = CurrentQuestion(cut);

        Click(cut, "Show answer (Enter)");
        Click(cut, "Show answer (Enter)");
        Click(cut, "Next");
        for (var index = 1; index < initialTotal; index++) Click(cut, "Next");

        Assert.Equal(missedQuestion, CurrentQuestion(cut));
        Assert.Equal(1, Total(cut));
        Click(cut, "Show answer (Enter)");
        Click(cut, "Show answer (Enter)");
        Click(cut, "Next");

        Assert.Equal(missedQuestion, CurrentQuestion(cut));
        Assert.Equal(1, Total(cut));
        cut.Find("input[placeholder]").Input(answers[missedQuestion]);
        cut.WaitForAssertion(() => Assert.Equal(initialTotal, Total(cut)));

        var fullRound = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < initialTotal; index++)
        {
            Assert.True(fullRound.Add(CurrentQuestion(cut)));
            Click(cut, "Next");
        }
        Assert.Equal(words.Select(word => word.English).Order(), fullRound.Order());
    }

    [Fact]
    public void ChangingChunkAndSplitDropsQueuedRetryAndStaleAnswerState()
    {
        using var test = new ComponentTestContext();
        var words = SeedTrainingWords(test, 5);
        var cut = RenderSpelling(test);
        SetSplit(cut, 2);
        Click(cut, "Show answer (Enter)");
        SetChunk(cut, 2);

        Assert.DoesNotContain("Answers:", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
        Assert.Equal(ExpectedChunk(words.Select(word => word.English).ToList(), 2, 1).Count, Total(cut));

        Click(cut, "Show answer (Enter)");
        SetSplit(cut, 10);
        Assert.Equal("1", NumberInputs(cut)[1].GetAttribute("value"));
        Assert.DoesNotContain("Answers:", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
        Assert.Equal(ExpectedChunk(words.Select(word => word.English).ToList(), 10, 0).Count, Total(cut));

        SetChunk(cut, 10);
        Assert.Contains("Nothing to practise yet", cut.Markup);
        SetSplit(cut, 1);
        Assert.Equal("1", NumberInputs(cut)[1].GetAttribute("value"));
        Assert.Equal(words.Count, Total(cut));
        Assert.Contains(CurrentQuestion(cut), words.Select(word => word.English));
    }

    [Fact]
    public void FlashcardRetriesStayInsideSelectedChunkAndKeepItsOriginalSize()
    {
        using var test = new ComponentTestContext();
        var words = SeedTrainingWords(test, 5);
        var cut = RenderFlashcards(test);
        SetSplit(cut, 2);
        var oldChunkQuestion = CurrentCardQuestion(cut);
        Click(cut, "Forgot");
        SetChunk(cut, 2);

        Assert.Equal(ExpectedChunk(words.Select(word => word.Kana).ToList(), 2, 1).Count, Total(cut));
        var selectedChunkQuestions = new HashSet<string>(StringComparer.Ordinal);
        var selectedTotal = Total(cut);
        for (var index = 0; index < selectedTotal; index++)
        {
            Assert.True(selectedChunkQuestions.Add(CurrentCardQuestion(cut)));
            Click(cut, "Show answer");
            Click(cut, "Next question");
        }

        Assert.DoesNotContain(oldChunkQuestion, selectedChunkQuestions);
        Assert.Equal(selectedTotal, Total(cut));
        Assert.Contains(CurrentCardQuestion(cut), selectedChunkQuestions);
    }

    [Fact]
    public void PartialKanjiAnswerIsClearedWhenChangingChunks()
    {
        using var test = new ComponentTestContext(workspace => File.WriteAllText(
            Path.Combine(workspace.CatalogRoot, "Kanji", "Singel", "Singel.json"),
            JsonSerializer.Serialize(new Word[]
            {
                new() { Id = 20, Japanese = "円", Kana = "えん", English = "yen;circle" },
                new() { Id = 21, Japanese = "時", Kana = "とき", English = "time" },
                new() { Id = 22, Japanese = "水", Kana = "みず", English = "water" },
                new() { Id = 23, Japanese = "山", Kana = "やま", English = "mountain" }
            })));
        var words = test.Kanji.Single.Words.ToList();
        var target = words[0];
        Assert.Equal(4, words.Count);
        foreach (var word in words) test.Kanji.Single.AddToTraining(word);
        test.UseCategory("training");
        var cut = test.Context.Render<KanjiSingleMeaning>();
        SetSplit(cut, 2);
        for (var index = 0; index < Total(cut) && CurrentQuestion(cut) != target.Japanese; index++) Click(cut, "Next");
        Assert.Equal(target.Japanese, CurrentQuestion(cut));

        var oneAnswer = target.English.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        cut.Find("input[placeholder]").Input(oneAnswer);
        Assert.Contains("Answers:", cut.Markup);
        SetChunk(cut, 2);

        Assert.DoesNotContain("Answers:", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
    }

    [Fact]
    public void CombinedKanjiRetryDoesNotCrossChunkBoundary()
    {
        using var test = new ComponentTestContext();
        var words = test.Kanji.Combined.Words
            .GroupBy(word => word.Japanese).Select(group => group.First()).Take(5).ToList();
        foreach (var word in words) test.Kanji.Combined.AddToTraining(word);
        test.UseCategory("training");
        var cut = test.Context.Render<KanjiCombinedMeaning>();
        SetSplit(cut, 2);
        var abandonedChunkQuestion = CurrentQuestion(cut);
        Click(cut, "Show answer (Enter)");
        SetChunk(cut, 2);

        var selectedChunkTotal = Total(cut);
        for (var index = 0; index < selectedChunkTotal; index++) Click(cut, "Next");

        Assert.NotEqual(abandonedChunkQuestion, CurrentQuestion(cut));
    }

    private static List<Word> SeedTrainingWords(ComponentTestContext test, int count)
    {
        var words = test.Adverbs.Words.GroupBy(word => word.English).Select(group => group.First()).Take(count).ToList();
        foreach (var word in words) test.Adverbs.AddToTraining(word);
        return words.Cast<Word>().ToList();
    }

    private static IRenderedComponent<WordSpelling> RenderSpelling(ComponentTestContext test)
    {
        test.UseCategory("training");
        return test.Context.Render<WordSpelling>();
    }

    private static IRenderedComponent<WordFlashCards> RenderFlashcards(ComponentTestContext test)
    {
        test.UseCategory("training");
        return test.Context.Render<WordFlashCards>();
    }

    private static List<string> ExpectedChunk(IReadOnlyList<string> values, int count, int index)
    {
        // A fixed five-item oracle avoids deriving expectations with GetChunk's arithmetic.
        int[][] sizes = [
            [5], [3, 2], [2, 2, 1], [2, 1, 1, 1], [1, 1, 1, 1, 1],
            [1, 1, 1, 1, 1, 0], [1, 1, 1, 1, 1, 0, 0], [1, 1, 1, 1, 1, 0, 0, 0],
            [1, 1, 1, 1, 1, 0, 0, 0, 0], [1, 1, 1, 1, 1, 0, 0, 0, 0, 0]
        ];
        Assert.Equal(5, values.Count);
        var chunks = sizes[count - 1];
        return values.Skip(chunks.Take(index).Sum()).Take(chunks[index]).ToList();
    }

    private static void SetSplit(IRenderedComponent<IComponent> cut, int count) => NumberInputs(cut)[0].Change(count.ToString());
    private static void SetChunk(IRenderedComponent<IComponent> cut, int index) => NumberInputs(cut)[1].Change(index.ToString());
    private static AngleSharp.Dom.IElement[] NumberInputs(IRenderedComponent<IComponent> cut) => cut.FindAll("input[type=number]").ToArray();
    private static int Total(IRenderedComponent<IComponent> cut) => int.Parse(cut.Find("small.text-muted").TextContent.Split('/')[1]);
    private static string CurrentQuestion(IRenderedComponent<IComponent> cut) => cut.Find("p.lead").TextContent.Trim();
    private static string CurrentCardQuestion(IRenderedComponent<WordFlashCards> cut) => cut.Find("p.flash-card-question").TextContent.Trim();
    private static void Click(IRenderedComponent<IComponent> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label).Click();
}
