using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;

namespace RealJapanese.Components.Pages.Verbs;

public class VerbSpellingBase : PracticeBase
{
    [Inject] public VerbData VerbData { get; set; } = null!;
    private string _oldUserInput = string.Empty;

    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        Questions = VerbData.DictionaryVerbs.OrderBy(_ => Guid.NewGuid()).ToList();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.FocusInput();

    protected override void OnUserInputChanged(string value)
    {
        base.OnUserInputChanged(value);

        if (IsCorrect())
            _ = GoToNextQuestionAsync();
    }

    protected override void OnEnterPressed()
    {
        if (showAnswer && _oldUserInput == UserInput)
        {
            _ = GoToNextQuestionAsync();
        }
        else
        {
            if (IsCorrect())
                _ = GoToNextQuestionAsync();
            else
                RevealAnswer();

            _oldUserInput = UserInput;
        }
    }
}