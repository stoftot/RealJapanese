using Bunit;
using RealJapanese.Components.Pages.Words;
using RealJapanese.Components.Shared;

namespace RealJapanese.ComponentTests;

/// <summary>Verifies flashcard control transitions, retry selection, and JavaScript shortcut lifetime wiring.</summary>
public sealed class FlashcardComponentTests
{
    [Fact]
    public void ForgotAndWrongAnswersAreRetriedWhileCorrectCardIsNot()
    {
        using var test = new ComponentTestContext();
        var words = test.Words.Words.GroupBy(word => word.Kana).Select(group => group.First()).Take(3).ToList();
        foreach (var word in words) test.Words.AddToVocab(word);
        test.UseCategory("known");
        var cut = test.Context.Render<WordFlashCards>();
        var retried = new HashSet<string>(StringComparer.Ordinal);

        var forgot = CurrentQuestion(cut);
        Button(cut, "Forgot").Click();
        Button(cut, "Next question").Click();

        var wrong = CurrentQuestion(cut);
        Button(cut, "Show answer").Click();
        Button(cut, "Gave wrong answer").Click();

        Button(cut, "Show answer").Click();
        Button(cut, "Next question").Click();

        for (var index = 0; index < 2; index++)
        {
            retried.Add(CurrentQuestion(cut));
            Button(cut, "Show answer").Click();
            Button(cut, "Next question").Click();
        }

        Assert.Equal(new[] { forgot, wrong }.Order(), retried.Order());
    }

    [Fact]
    public async Task CardRegistersOneGlobalShortcutHandlerAndRoutesItsCallbacks()
    {
        using var test = new ComponentTestContext();
        test.Context.JSInterop.Setup<string>("blazorHelpers.registerFlashCardShortcuts").SetResult("shortcut-1");
        var primary = 0;
        var forgot = 0;
        var wrong = 0;
        var cut = test.Context.Render<FlashPracticeCard>(parameters => parameters
            .Add(component => component.Question, "question")
            .Add(component => component.Answer, "answer")
            .Add(component => component.PrimaryActionRequested, () => primary++)
            .Add(component => component.ForgotRequested, () => forgot++)
            .Add(component => component.GaveWrongAnswerRequested, () => wrong++));

        cut.WaitForAssertion(() => Assert.Single(test.Context.JSInterop.Invocations,
            invocation => invocation.Identifier == "blazorHelpers.registerFlashCardShortcuts"));

        await cut.Instance.HandleGlobalSpaceAsync();
        await cut.Instance.HandleGlobalBackspaceAsync();
        cut.Render(parameters => parameters
            .Add(component => component.Question, "question")
            .Add(component => component.Answer, "answer")
            .Add(component => component.ShowAnswer, true)
            .Add(component => component.PrimaryActionRequested, () => primary++)
            .Add(component => component.ForgotRequested, () => forgot++)
            .Add(component => component.GaveWrongAnswerRequested, () => wrong++));
        await cut.Instance.HandleGlobalBackspaceAsync();

        Assert.Single(test.Context.JSInterop.Invocations,
            invocation => invocation.Identifier == "blazorHelpers.registerFlashCardShortcuts");
        Assert.Equal(1, primary);
        Assert.Equal(1, forgot);
        Assert.Equal(1, wrong);
    }

    [Fact(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    public void RetryQueuedInOneFlashcardChunkDoesNotLeakIntoAnotherChunk()
    {
        using var test = new ComponentTestContext();
        var words = test.Words.Words.GroupBy(word => word.Kana).Select(group => group.First()).Take(4).ToList();
        foreach (var word in words) test.Words.AddToVocab(word);
        test.UseCategory("known");
        var cut = test.Context.Render<WordFlashCards>();
        cut.FindAll("input[type=number]")[0].Change("2");
        var oldChunkQuestion = CurrentQuestion(cut);
        Button(cut, "Forgot").Click();

        cut.FindAll("input[type=number]")[1].Change("2");
        var total = int.Parse(cut.Find("small.text-muted").TextContent.Split('/')[1]);
        for (var index = 0; index < total; index++)
        {
            Button(cut, "Show answer").Click();
            Button(cut, "Next question").Click();
        }

        // A retry from the abandoned chunk must not replace the selected chunk's next complete round.
        Assert.NotEqual(oldChunkQuestion, CurrentQuestion(cut));
    }

    private static string CurrentQuestion(IRenderedComponent<WordFlashCards> cut) =>
        cut.Find("p.flash-card-question").TextContent.Trim();

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<WordFlashCards> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
}
