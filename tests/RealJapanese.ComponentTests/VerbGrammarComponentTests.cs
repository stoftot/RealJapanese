using Bunit;
using RealJapanese.Components.Pages.Verbs;

namespace RealJapanese.ComponentTests;

/// <summary>Checks that verb grammar practice initializes and can reveal answers without a model exception.</summary>
public sealed class VerbGrammarComponentTests
{
    [Fact]
    public void GrammarPracticeLoadsAndRevealsAnAnswer()
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<ConjugationsAndForms>();

        Assert.NotEmpty(cut.Find("p.lead").TextContent.Trim());
        Assert.NotEqual("0/0", cut.Find("small.text-muted").TextContent.Trim());
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Show answer (Enter)").Click();
        Assert.Contains("Answers:</strong>", cut.Markup);
    }
}
