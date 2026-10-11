using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;

public abstract class PracticeBase : ComponentBase
{
    // Data each derived class sets in OnInitialized
    private List<QuestionAnswerDto> _originalQuestions = [];
    protected IEnumerable<QuestionAnswerDto> OrginalQuestions
    {
        get => _originalQuestions;
        set => _originalQuestions = value.ToList();
    }
    protected IList<QuestionAnswerDto> Questions { get; set; } = [];
    protected int currentQuestionIndex = 0;
    private readonly List<QuestionAnswerDto> _retryQuestions = [];
    private QuestionAnswerDto? _currentRetryQuestion;
    private readonly HashSet<QuestionAnswerDto> _movedQuestions = [];

    // UI
    protected double UiScale { get; set; } = 1.0;

    private string _userInput = string.Empty;
    protected string UserInput
    {
        get => _userInput;
        set => OnUserInputChanged(value ?? string.Empty);  // delegate to overrides
    }

    protected bool showAnswer = false;
    protected bool RepeatQuestionsWithRevealedAnswers { get; set; } = true;
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

    protected void ClearUserInput() => _userInput = string.Empty;

    protected virtual void OnEnterPressed()
    {
        // default: advance only if correct
        if (IsCorrect()) _ = CompleteQuestionAsync();
    }

    // ---- events from the UI ----
    protected void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            OnEnterPressed();
        }
    }

    protected void RevealAnswer()
    {
        ShowAnswer();
        if (Questions.Count > 0
            && !ReferenceEquals(_currentRetryQuestion, Questions[currentQuestionIndex]))
        {
            Questions[currentQuestionIndex].RecordAnswer?.Invoke(false);
            if (RepeatQuestionsWithRevealedAnswers)
                _retryQuestions.Add(Questions[currentQuestionIndex]);
            _currentRetryQuestion = Questions[currentQuestionIndex];
        }
    }
    
    protected void ShowAnswer() => showAnswer = true;
    protected void HideAnswer() => showAnswer = false;
    
    protected virtual bool IsCorrect() => Normalize(UserInput) == Normalize(CurrentAnswer);
    
    protected virtual Task FocusAnswerInputAsync() => Task.CompletedTask;

    protected abstract void UpdateQuestions();

    protected Task CompleteQuestionAsync()
    {
        // Copying a revealed answer only finishes the failed attempt; its retry must succeed unaided.
        if (Questions.Count > 0 && !ReferenceEquals(_currentRetryQuestion, Questions[currentQuestionIndex]))
        {
            var question = Questions[currentQuestionIndex];
            if (question.RecordAnswer?.Invoke(true) == true)
                _movedQuestions.Add(question);
        }
        return GoToNextQuestionAsync();
    }

    protected async Task GoToNextQuestionAsync()
    {
        NextQuestion();               // reshuffles at end & wraps
        _currentRetryQuestion = null;
        _userInput = string.Empty;    // clear input backing field
        showAnswer = false;           // hide previous answer
        await FocusAnswerInputAsync();
        StateHasChanged();
    }

    protected virtual void NextQuestion()
    {
        if (Questions.Count == 0)
            return;

        currentQuestionIndex++;
        if (currentQuestionIndex >= Questions.Count)
        {
            if (_retryQuestions.Count > 0)
                Questions = _retryQuestions.ToList();
            else
                UpdateQuestions();
            
            Questions.Shuffle();

            _retryQuestions.Clear();
            currentQuestionIndex = 0;
        }
    }

    // Keep the active round and retries stable. Repartition only when starting a full round
    // or explicitly selecting a different chunk, and only change the pool after a move.
    protected void RefreshQuestionPool()
    {
        if (_movedQuestions.Count == 0) return;
        _originalQuestions.RemoveAll(_movedQuestions.Contains);
        _movedQuestions.Clear();
    }

    protected void ResetRoundState()
    {
        _retryQuestions.Clear();
        _currentRetryQuestion = null;
        currentQuestionIndex = 0;
        _userInput = string.Empty;
        showAnswer = false;
    }

    // Normalize: trim + remove all whitespace + case-insensitive
    protected virtual string Normalize(string? s) =>
        new string((s ?? string.Empty).Trim().Where(c => !char.IsWhiteSpace(c)).ToArray())
            .ToLowerInvariant();   
}
