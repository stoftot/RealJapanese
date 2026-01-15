using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Repositories.DTOs;

namespace RealJapanese.Components.Shared;

public abstract class PracticeBase : ComponentBase
{
    // Data each derived class sets in OnInitialized
    protected IEnumerable<QuestionAnswerDto> OrginalQuestions { get; set; }
    protected List<QuestionAnswerDto> Questions { get; set; } = [];
    protected int currentQuestionIndex = 0;

    // UI
    protected double UiScale { get; set; } = 1.0;

    private string _userInput = string.Empty;
    protected string UserInput
    {
        get => _userInput;
        set => OnUserInputChanged(value ?? string.Empty);  // delegate to overrides
    }

    protected bool showAnswer = false;
    protected ElementReference answerInputRef;

    protected string CurrentQuestion =>
        Questions.Count == 0 ? string.Empty : (Questions[currentQuestionIndex].Question ?? string.Empty);

    protected string CurrentAnswer =>
        Questions.Count == 0 ? string.Empty : (Questions[currentQuestionIndex].Answer ?? string.Empty);

    // ---- overridable hooks for the two behaviors ----
    protected virtual void OnUserInputChanged(string value)
    {
        // default behavior: just set; derived classes can add extras
        _userInput = value;
    }

    protected virtual void OnEnterPressed()
    {
        // default: advance only if correct
        if (IsCorrect()) _ = GoToNextQuestionAsync();
    }

    // ---- events from the UI ----
    protected void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            OnEnterPressed();
        }
    }

    protected void RevealAnswer() => showAnswer = true;

    protected bool IsCorrect() => Normalize(UserInput) == Normalize(CurrentAnswer);
    
    protected virtual Task FocusAnswerInputAsync() => Task.CompletedTask;

    protected async Task GoToNextQuestionAsync()
    {
        NextQuestion();               // reshuffles at end & wraps
        _userInput = string.Empty;    // clear input backing field
        showAnswer = false;           // hide previous answer
        await FocusAnswerInputAsync();
        StateHasChanged();
    }

    private void NextQuestion()
    {
        currentQuestionIndex++;
        if (currentQuestionIndex >= Questions.Count)
        {
            Questions = Questions.OrderBy(_ => Guid.NewGuid()).ToList();
            currentQuestionIndex = 0;
        }
    }

    // Normalize: trim + remove all whitespace + case-insensitive
    protected static string Normalize(string? s) =>
        new string((s ?? string.Empty).Trim().Where(c => !char.IsWhiteSpace(c)).ToArray())
            .ToLowerInvariant();   
}