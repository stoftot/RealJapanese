using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;

namespace RealJapanese.Components.Pages;

public partial class NumbersBase : PracticeBase
{
    [Inject] public NumbersQuestionGenerator NumbersQuestionGenerator { get; set; } = null!;

    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        Questions = NumbersQuestionGenerator.QuestionAnswers.OrderBy(_ => Guid.NewGuid()).ToList();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef?.FocusInput() ?? Task.CompletedTask;

    protected override void OnUserInputChanged(string value)
    {
        var v = value ?? string.Empty;

        if (v.EndsWith(" "))
        {
            showAnswer = true;
            v = v.TrimEnd();
        }

        base.OnUserInputChanged(v);

        if (IsCorrect())
            _ = GoToNextQuestionAsync();
    }

    protected override void OnEnterPressed()
    {
        if (IsCorrect())
            _ = GoToNextQuestionAsync();
    }
}