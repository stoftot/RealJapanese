using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models;
using RealJapanese.Components.Pages.Words;
using Repositories.Exstensions;

namespace RealJapanese.ComponentTests;

/// <summary>Verifies normalized single-answer input and the reveal-to-retry round transition.</summary>
public sealed class SingleAnswerPracticeComponentTests
{
    [Fact]
    public void CorrectAnswerIgnoresCaseAndWhitespaceAndClearsForTheNextQuestion()
    {
        using var test = new ComponentTestContext();
        var words = SeedKnownWords(test, 2);
        var answers = words.ToDictionary(word => word.English, word => word.Kana.ToRomaji());
        var cut = Render(test);
        var firstQuestion = CurrentQuestion(cut);
        var normalizedVariant = string.Join(" ", answers[firstQuestion].ToUpperInvariant().ToCharArray());

        AnswerInput(cut).Input($"  {normalizedVariant}  ");

        cut.WaitForAssertion(() => Assert.NotEqual(firstQuestion, CurrentQuestion(cut)));
        Assert.Equal(string.Empty, AnswerInput(cut).GetAttribute("value"));
        Assert.Contains("2/2", cut.Markup);
    }

    [Fact]
    public void RevealedQuestionAppearsOnceInRetryThenTheFullRoundResumes()
    {
        using var test = new ComponentTestContext();
        var words = SeedKnownWords(test, 2);
        var answers = words.ToDictionary(word => word.English, word => word.Kana.ToRomaji());
        var cut = Render(test);
        var revealedQuestion = CurrentQuestion(cut);

        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Show answer (Enter)").Click();
        Assert.Contains("Answers:", cut.Markup);
        Button(cut, "Next").Click();

        var remainingOriginalQuestion = CurrentQuestion(cut);
        Assert.NotEqual(revealedQuestion, remainingOriginalQuestion);
        AnswerInput(cut).Input(answers[remainingOriginalQuestion]);
        cut.WaitForAssertion(() => Assert.Equal(revealedQuestion, CurrentQuestion(cut)));
        Assert.Contains("1/1", cut.Markup);

        AnswerInput(cut).Input(answers[revealedQuestion]);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(CurrentQuestion(cut), answers.Keys);
            Assert.Contains("/2", cut.Markup);
        });
    }

    [Fact]
    public void WrongEnterRevealsAndSecondUnchangedEnterAdvances()
    {
        using var test = new ComponentTestContext();
        SeedKnownWords(test, 2);
        var cut = Render(test);
        var firstQuestion = CurrentQuestion(cut);

        AnswerInput(cut).Input("definitely wrong");
        AnswerInput(cut).KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        Assert.Contains("Answers:", cut.Markup);

        AnswerInput(cut).KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        cut.WaitForAssertion(() => Assert.NotEqual(firstQuestion, CurrentQuestion(cut)));
    }

    private static IRenderedComponent<WordSpelling> Render(ComponentTestContext test)
    {
        test.UseCategory("known");
        return test.Context.Render<WordSpelling>();
    }

    private static List<Word> SeedKnownWords(ComponentTestContext test, int count)
    {
        var words = test.Words.Words
            .GroupBy(word => word.English)
            .Select(group => group.First())
            .Take(count)
            .ToList();
        foreach (var word in words) test.Words.AddToVocab(word);
        return words;
    }

    private static string CurrentQuestion(IRenderedComponent<WordSpelling> cut) =>
        cut.Find("p.lead").TextContent.Trim();

    private static IElement AnswerInput(IRenderedComponent<WordSpelling> cut) =>
        cut.Find("input[placeholder]");

    private static IElement Button(IRenderedComponent<WordSpelling> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
}
