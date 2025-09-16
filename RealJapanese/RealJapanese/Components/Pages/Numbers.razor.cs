using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web; // KeyboardEventArgs + FocusAsync
using Repositories;
using Repositories.DTOs;

namespace RealJapanese.Components.Pages;

public class NumbersBase : ComponentBase
{
    [Inject] public NumbersData NumbersData { get; set; } = null!;

    public List<QuestionAnswerDto> Questions = [];
    public int currentQuestionIndex = 0;

    // --- UI state ---
    private string _userInput = string.Empty;

    protected string UserInput
    {
        get => _userInput;
        set
        {
            var v = value ?? string.Empty;
            
            if (v.EndsWith(" "))
            {
                showAnswer = true;
                v = v.TrimEnd();
            }

            _userInput = v;
            CheckAnswerAndAdvanceIfMatch();
        }
    }
    protected bool showAnswer = false;
    protected ElementReference answerInputRef;

    protected override void OnInitialized()
    {
        Questions = NumbersData.QuestionAnswers.OrderBy(_ => Guid.NewGuid()).ToList();
    }

    // Convenience getters
    protected string CurrentQuestion =>
        Questions.Count == 0 ? string.Empty : (Questions[currentQuestionIndex].Question ?? string.Empty);

    protected string CurrentAnswer =>
        Questions.Count == 0 ? string.Empty : (Questions[currentQuestionIndex].Answer ?? string.Empty);

    // Optional: allow Enter to advance if already correct
    protected void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            ForceNextIfCorrect();
        }
    }

    protected void RevealAnswer() => showAnswer = true;

    // === Advancing ===
    protected void CheckAnswerAndAdvanceIfMatch()
    {
        if (Normalize(UserInput) == Normalize(CurrentAnswer))
        {
            GoToNextQuestion();
        }
    }

    protected void ForceNextIfCorrect()
    {
        if (Normalize(UserInput) == Normalize(CurrentAnswer))
        {
            GoToNextQuestion();
        }
    }

    protected async void GoToNextQuestion()
    {
        NextQuestion();               // reshuffles at end & wraps
        UserInput = string.Empty;     // clear input
        showAnswer = false;           // hide previous answer
        await answerInputRef.FocusAsync();
        StateHasChanged();
    }
    
    private void NextQuestion()
    {
        currentQuestionIndex++;
        if (currentQuestionIndex >= NumbersData.QuestionAnswers.Count)
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