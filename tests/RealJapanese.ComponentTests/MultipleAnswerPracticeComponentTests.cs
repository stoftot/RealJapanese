using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Pages.Adjectives;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Verbs;
using Repositories.Exstensions;

namespace RealJapanese.ComponentTests;

/// <summary>Exercises unordered kanji meanings and ordered verb/adjective spelling-and-type contracts.</summary>
public sealed class MultipleAnswerPracticeComponentTests
{
    [Fact]
    public void KanjiMeaningsIgnoreDuplicateCreditAndAdvanceExactlyOnceOnTheLastDistinctAnswer()
    {
        using var test = new ComponentTestContext();
        var target = test.Kanji.Single.Words.First(word =>
            word.English.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length == 2);
        var otherWords = test.Kanji.Single.Words.Where(word => word.Id != target.Id)
            .GroupBy(word => word.Japanese).Select(group => group.First()).Take(2);
        foreach (var word in otherWords.Prepend(target)) test.Kanji.Single.AddToVocab(word);
        var answers = target.English.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        test.UseCategory("known");
        var cut = test.Context.Render<KanjiSingleMeaning>();
        NavigateToQuestion(cut, target.Japanese);

        cut.Find("input[placeholder]").Input($"  {answers[1].ToUpperInvariant()}  ");
        var progressBeforeDuplicate = Progress(cut);
        Assert.Equal(answers[1], GivenAnswers(cut), ignoreCase: true);

        cut.Find("input[placeholder]").Input(answers[1]);
        Assert.Equal(target.Japanese, CurrentQuestion(cut));
        Assert.Equal(progressBeforeDuplicate, Progress(cut));
        Assert.Equal(answers[1], GivenAnswers(cut), ignoreCase: true);

        var (current, total) = ParseProgress(progressBeforeDuplicate);
        var expectedProgress = current == total ? $"1/{total}" : $"{current + 1}/{total}";
        cut.Find("input[placeholder]").Input(answers[0]);
        cut.WaitForAssertion(() => Assert.Equal(expectedProgress, Progress(cut)));
        Assert.DoesNotContain(cut.FindAll("strong"), element => element.TextContent.Trim() == "Answers:");
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
    }

    [Theory]
    [InlineData("verb")]
    [InlineData("adjective")]
    public void SpellingRequiresRomajiBeforeTheShortType(string catalog)
    {
        using var test = new ComponentTestContext();
        var (cut, items) = RenderSpelling(test, catalog);
        var question = CurrentQuestion(cut);
        var item = items.Single(candidate => candidate.English == question);
        var progressBefore = Progress(cut);

        cut.Find("input[placeholder]").Input(item.TypeShortForm());
        Assert.Equal(question, CurrentQuestion(cut));
        Assert.Equal(progressBefore, Progress(cut));
        Assert.DoesNotContain(cut.FindAll("strong"), element => element.TextContent.Trim() == "Answers:");

        cut.Find("input[placeholder]").Input(item.Kana.ToRomaji());
        Assert.Equal(item.Kana.ToRomaji(), GivenAnswers(cut));

        var (current, total) = ParseProgress(progressBefore);
        var expectedProgress = current == total ? $"1/{total}" : $"{current + 1}/{total}";
        cut.Find("input[placeholder]").Input(item.TypeShortForm());
        cut.WaitForAssertion(() => Assert.Equal(expectedProgress, Progress(cut)));
        Assert.DoesNotContain(cut.FindAll("strong"), element => element.TextContent.Trim() == "Answers:");
    }

    private static (IRenderedComponent<IComponent> Cut, IReadOnlyList<Conjugatabel> Items) RenderSpelling(
        ComponentTestContext test, string catalog)
    {
        test.UseCategory("known");
        if (catalog == "verb")
        {
            var items = test.Verbs.Words.GroupBy(item => item.English).Select(group => group.First()).Take(3).ToList();
            foreach (var item in items) test.Verbs.AddToVocab(item);
            return (test.Context.Render<VerbSpelling>(), items);
        }

        var adjectives = test.Adjectives.Words.GroupBy(item => item.English).Select(group => group.First()).Take(3).ToList();
        foreach (var item in adjectives) test.Adjectives.AddToVocab(item);
        return (test.Context.Render<AdjectiveSpelling>(), adjectives);
    }

    private static void NavigateToQuestion(IRenderedComponent<KanjiSingleMeaning> cut, string question)
    {
        var total = ParseProgress(Progress(cut)).Total;
        for (var index = 0; index < total && CurrentQuestion(cut) != question; index++)
            cut.FindAll("button").Single(button => button.TextContent.Trim() == "Next").Click();
        Assert.Equal(question, CurrentQuestion(cut));
    }

    private static string GivenAnswers(IRenderedComponent<IComponent> cut) =>
        cut.FindAll("strong").Single(element => element.TextContent.Trim() == "Answers:")
            .NextElementSibling!.TextContent.Trim();

    private static string CurrentQuestion(IRenderedComponent<IComponent> cut) => cut.Find("p.lead").TextContent.Trim();

    private static string Progress(IRenderedComponent<IComponent> cut) => cut.Find("small.text-muted").TextContent.Trim();

    private static (int Current, int Total) ParseProgress(string progress)
    {
        var parts = progress.Split('/');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }
}
