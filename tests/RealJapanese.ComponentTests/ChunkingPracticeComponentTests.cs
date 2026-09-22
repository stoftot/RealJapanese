using Bunit;
using DataLoaders.Models;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Words;

namespace RealJapanese.ComponentTests;

/// <summary>Checks chunk coverage, empty chunks, reset behavior, and the documented retry-isolation defect.</summary>
public sealed class ChunkingPracticeComponentTests
{
    [Fact]
    public void UnevenAndOverprovisionedChunksCoverEveryQuestionExactlyOnce()
    {
        using var test = new ComponentTestContext();
        var words = SeedKnownWords(test, 5);
        var expected = words.Select(word => word.English).ToHashSet(StringComparer.Ordinal);
        var cut = Render(test);

        SetChunkCount(cut, 2);
        var twoChunkQuestions = CollectAcrossChunks(cut, 2);
        Assert.Equal(expected.Order(), twoChunkQuestions.Order());

        SetChunkCount(cut, 10);
        var tenChunkQuestions = CollectAcrossChunks(cut, 10);
        Assert.Equal(expected.Order(), tenChunkQuestions.Order());
        Assert.Contains("Nothing to practise yet", cut.Markup);
    }

    [Fact]
    public void ChangingChunkClearsInputAnswerAndRestartsProgress()
    {
        using var test = new ComponentTestContext();
        SeedKnownWords(test, 4);
        var cut = Render(test);
        SetChunkCount(cut, 2);

        cut.Find("input[placeholder]").Input("wrong");
        FindButton(cut, "Show answer (Enter)").Click();
        Assert.Contains("Answers:", cut.Markup);

        SetSelectedChunk(cut, 2);

        Assert.DoesNotContain("Answers:", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
        Assert.Contains("1/2", cut.Markup);
    }

    [Fact]
    public void CombinedKanjiUnevenAndOverprovisionedChunksCoverEveryQuestionExactlyOnce()
    {
        using var test = new ComponentTestContext();
        var words = test.Kanji.Combined.Words.GroupBy(word => word.Japanese).Select(group => group.First()).Take(5).ToList();
        foreach (var word in words) test.Kanji.Combined.AddToVocab(word);
        var expected = words.Select(word => word.Japanese).ToHashSet(StringComparer.Ordinal);
        test.UseCategory("known");
        var cut = test.Context.Render<KanjiCombinedMeaning>();

        SetChunkCount(cut, 2);
        Assert.Equal(expected.Order(), CollectAcrossKanjiChunks(cut, 2).Order());

        SetChunkCount(cut, 10);
        Assert.Contains("Nothing to practise yet", cut.Markup);
        Assert.Equal(expected.Order(), CollectAcrossKanjiChunks(cut, 10).Order());
    }

    [Fact(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    public void RetryQueuedInOneChunkDoesNotLeakIntoAnotherChunk()
    {
        using var test = new ComponentTestContext();
        SeedKnownWords(test, 4);
        var cut = Render(test);
        SetChunkCount(cut, 2);
        var oldChunkQuestion = CurrentQuestion(cut);
        FindButton(cut, "Show answer (Enter)").Click();

        SetSelectedChunk(cut, 2);
        var total = CurrentTotal(cut);
        for (var index = 0; index < total; index++) FindButton(cut, "Next").Click();

        // This assertion defines the intended isolation and currently exposes the shared retry queue leak.
        Assert.NotEqual(oldChunkQuestion, CurrentQuestion(cut));
    }

    private static HashSet<string> CollectAcrossChunks(IRenderedComponent<WordSpelling> cut, int chunkCount)
    {
        var questions = new HashSet<string>(StringComparer.Ordinal);
        for (var chunk = 1; chunk <= chunkCount; chunk++)
        {
            SetSelectedChunk(cut, chunk);
            if (cut.Markup.Contains("Nothing to practise yet", StringComparison.Ordinal)) continue;
            var total = CurrentTotal(cut);
            for (var index = 0; index < total; index++)
            {
                Assert.True(questions.Add(CurrentQuestion(cut)), "A question appeared in more than one chunk.");
                FindButton(cut, "Next").Click();
            }
        }
        return questions;
    }

    private static HashSet<string> CollectAcrossKanjiChunks(IRenderedComponent<KanjiCombinedMeaning> cut, int chunkCount)
    {
        var questions = new HashSet<string>(StringComparer.Ordinal);
        for (var chunk = 1; chunk <= chunkCount; chunk++)
        {
            SetSelectedChunk(cut, chunk);
            if (cut.Markup.Contains("Nothing to practise yet", StringComparison.Ordinal)) continue;
            var total = CurrentTotal(cut);
            for (var index = 0; index < total; index++)
            {
                Assert.True(questions.Add(CurrentQuestion(cut)), "A combined-kanji question appeared in more than one chunk.");
                FindButton(cut, "Next").Click();
            }
        }
        return questions;
    }

    private static List<Word> SeedKnownWords(ComponentTestContext test, int count)
    {
        var words = test.Words.Words.GroupBy(word => word.English).Select(group => group.First()).Take(count).ToList();
        foreach (var word in words) test.Words.AddToVocab(word);
        return words;
    }

    private static IRenderedComponent<WordSpelling> Render(ComponentTestContext test)
    {
        test.UseCategory("known");
        return test.Context.Render<WordSpelling>();
    }

    private static void SetChunkCount(IRenderedComponent<WordSpelling> cut, int value) =>
        cut.FindAll("input[type=number]")[0].Change(value.ToString());

    private static void SetSelectedChunk(IRenderedComponent<WordSpelling> cut, int value) =>
        cut.FindAll("input[type=number]")[1].Change(value.ToString());

    private static void SetChunkCount(IRenderedComponent<KanjiCombinedMeaning> cut, int value) =>
        cut.FindAll("input[type=number]")[0].Change(value.ToString());

    private static void SetSelectedChunk(IRenderedComponent<KanjiCombinedMeaning> cut, int value) =>
        cut.FindAll("input[type=number]")[1].Change(value.ToString());

    private static int CurrentTotal(IRenderedComponent<WordSpelling> cut) =>
        int.Parse(cut.FindAll("small.text-muted").Single().TextContent.Split('/')[1]);

    private static int CurrentTotal(IRenderedComponent<KanjiCombinedMeaning> cut) =>
        int.Parse(cut.FindAll("small.text-muted").Single().TextContent.Split('/')[1]);

    private static string CurrentQuestion(IRenderedComponent<WordSpelling> cut) => cut.Find("p.lead").TextContent.Trim();

    private static string CurrentQuestion(IRenderedComponent<KanjiCombinedMeaning> cut) => cut.Find("p.lead").TextContent.Trim();

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<WordSpelling> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<KanjiCombinedMeaning> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
}
