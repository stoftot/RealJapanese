using Bunit;
using RealJapanese.Components.Pages.Nouns;

namespace RealJapanese.ComponentTests;

/// <summary>Checks fixed noun grammar prompts, revealed readings, and accepted romaji answers.</summary>
public sealed class NounGrammarComponentTests
{
    public static TheoryData<string, string> GrammarCases => new()
    {
        { "Noun - present affirmative - polite", "desu" },
        { "Noun - present negative - polite", "janaidesu" },
        { "Noun - past affirmative - polite", "deshita" },
        { "Noun - past negative - polite", "janakattadesu" },
        { "Noun - present affirmative - short", "da" },
        { "Noun - present negative - short", "janai" },
        { "Noun - past affirmative - short", "datta" },
        { "Noun - past negative - short", "janakatta" },
        { "Noun - te form", "de" }
    };

    [Fact]
    public void EmptySelections_StillProvideAllNineDistinctGrammarQuestions()
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<NounConjugate>();
        var prompts = new HashSet<string>();
        var total = int.Parse(cut.Find("small.text-muted").TextContent.Split('/')[1]);

        for (var index = 0; index < total; index++)
        {
            prompts.Add(CurrentQuestion(cut));
            Button(cut, "Next").Click();
        }

        Assert.Equal(9, total);
        Assert.Equal(9, prompts.Count);
        Assert.True(GrammarCases.Select(row => row.Data.Item1).ToHashSet().SetEquals(prompts));
    }

    [Fact]
    public void MaximumChunks_KeepEveryNounEndingAccessible()
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<NounConjugate>();
        Assert.Equal("9", cut.FindAll("input[type=number]")[0].GetAttribute("max"));
        cut.FindAll("input[type=number]")[0].Change("9");
        var prompts = new HashSet<string>();

        for (var chunk = 1; chunk <= 9; chunk++)
        {
            cut.FindAll("input[type=number]")[1].Change(chunk.ToString());
            Assert.Equal("1/1", cut.Find("small.text-muted").TextContent.Trim());
            prompts.Add(CurrentQuestion(cut));
        }

        Assert.Equal(9, prompts.Count);
    }

    [Theory]
    [MemberData(nameof(GrammarCases))]
    public void GrammarAnswer_IsRevealedAndAccepted(string prompt, string expectedAnswer)
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<NounConjugate>();
        NavigateToPrompt(cut, prompt);

        Button(cut, "Show answer (Enter)").Click();
        Assert.Contains($"Answers:</strong> {expectedAnswer}", cut.Markup);

        var progressBefore = cut.Find("small.text-muted").TextContent.Trim();
        cut.Find("input[placeholder]").Input(expectedAnswer);
        cut.WaitForAssertion(() => Assert.NotEqual(progressBefore, cut.Find("small.text-muted").TextContent.Trim()));
        Assert.DoesNotContain("alert-info", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("input[placeholder]").GetAttribute("value"));
    }

    private static void NavigateToPrompt(IRenderedComponent<NounConjugate> cut, string prompt)
    {
        var total = int.Parse(cut.Find("small.text-muted").TextContent.Split('/')[1]);
        for (var index = 0; index < total && CurrentQuestion(cut) != prompt; index++)
            Button(cut, "Next").Click();
        Assert.Equal(prompt, CurrentQuestion(cut));
    }

    private static string CurrentQuestion(IRenderedComponent<NounConjugate> cut) =>
        cut.Find("p.lead").TextContent.Trim();

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<NounConjugate> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
}
