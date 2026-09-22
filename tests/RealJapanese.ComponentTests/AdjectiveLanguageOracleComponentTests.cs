using Bunit;
using RealJapanese.Components.Pages.Adjectives;

namespace RealJapanese.ComponentTests;

/// <summary>Checks the literal adjective-ending prompts, revealed readings, and accepted UI answers against the language oracle.</summary>
public sealed class AdjectiveLanguageOracleComponentTests
{
    [Theory]
    [InlineData("い - present negative", "kunaidesu")]
    [InlineData("い - past affirmative", "kattadesu")]
    [InlineData("な - present affirmative", "nadesu")]
    [InlineData("いい - present negative", "yokunaidesu")]
    public void RepresentativeEndingIsRevealedAndAccepted(string prompt, string expectedAnswer)
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<AdjectiveConjugateBase>();
        NavigateToPrompt(cut, prompt);

        Button(cut, "Show answer (Enter)").Click();
        Assert.Contains($"Answers:</strong> {expectedAnswer}", cut.Markup);

        var progressBefore = cut.Find("small.text-muted").TextContent.Trim();
        cut.Find("input[placeholder]").Input(expectedAnswer);
        cut.WaitForAssertion(() => Assert.NotEqual(progressBefore, cut.Find("small.text-muted").TextContent.Trim()));
        Assert.DoesNotContain("alert-info", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
    }

    [Fact(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    public void IPresentAffirmativeEndingRetainsItsInitialI()
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<AdjectiveConjugateBase>();
        NavigateToPrompt(cut, "い - present affirmative");

        Button(cut, "Show answer (Enter)").Click();

        Assert.Contains("Answers:</strong> idesu", cut.Markup);
    }

    private static void NavigateToPrompt(IRenderedComponent<AdjectiveConjugateBase> cut, string prompt)
    {
        var total = int.Parse(cut.Find("small.text-muted").TextContent.Split('/')[1]);
        for (var index = 0; index < total && CurrentQuestion(cut) != prompt; index++)
            Button(cut, "Next").Click();
        Assert.Equal(prompt, CurrentQuestion(cut));
    }

    private static string CurrentQuestion(IRenderedComponent<AdjectiveConjugateBase> cut) =>
        cut.Find("p.lead").TextContent.Trim();

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<AdjectiveConjugateBase> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
}
