using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Repositories;
using Repositories.DTOs;

namespace RealJapanese.Components.Pages;

public class GreetingsBase : ComponentBase
{
    [Inject] public GreetingsData GreetingsData { get; set; } = null!;

    public List<QuestionAnswerDto> Questions = [];
    public int currentQuestionIndex = 0;

    protected double UiScale { get; set; } = 1.0;
    
    // --- UI state ---
    private string _userInput = string.Empty;

    protected string UserInput
    {
        get => _userInput;
        set => _userInput = value;

        // var v = value ?? string.Empty;
        //
        // if (v.EndsWith(" "))
        // {
        //     showAnswer = true;
        //     v = v.TrimEnd();
        // }
        //
        // _userInput = v;
        // CheckAnswerAndAdvanceIfMatch();
    }
    private string OldUserInput { get; set; } = string.Empty;
    protected bool showAnswer = false;
    protected ElementReference answerInputRef;

    protected override void OnInitialized()
    {
        Questions = GreetingsData.QuestionAnswers.OrderBy(_ => Guid.NewGuid()).ToList();
    }

    // Convenience getters
    protected string CurrentQuestion =>
        Questions.Count == 0 ? string.Empty : Questions[currentQuestionIndex].Question;

    protected string CurrentAnswer =>
        Questions.Count == 0 ? string.Empty : Questions[currentQuestionIndex].Answer;

    // Optional: allow Enter to advance if already correct
    protected void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            if (showAnswer && OldUserInput == UserInput)
            {
                GoToNextQuestion();
                return;
            }
            
            if(!CheckAnswerAndAdvanceIfMatch())
                RevealAnswer();
            OldUserInput = _userInput;
        }
    }

    protected void RevealAnswer() => showAnswer = true;

    // === Advancing ===
    protected bool CheckAnswerAndAdvanceIfMatch()
    {
        if (Normalize(UserInput) != Normalize(CurrentAnswer))return false;
        
        GoToNextQuestion();
        return true;
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