using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;

public abstract class FlashCardPracticeBase : PracticeBase
{
    protected int splitDataInto = 1;
    protected int selectedDataChunk = 1;
    private int selectedDataChunkIndex => selectedDataChunk - 1;

    protected override void UpdateQuestions()
    {
        Questions = OrginalQuestions
            .GetChunk(splitDataInto, selectedDataChunkIndex);
        RefreshQuestions();
    }

    protected virtual void RefreshQuestions()
    {
        Questions.Shuffle();

        currentQuestionIndex = 0;
        OnQuestionsUpdated();
        HideAnswer();
    }

    protected virtual void OnQuestionsUpdated()
    {
    }

    protected async Task HandlePrimaryActionAsync()
    {
        if (showAnswer)
        {
            await GoToNextQuestionAsync();
            return;
        }

        ShowAnswer();
        StateHasChanged();
    }

    protected void HandleForgotAction()
    {
        RevealAnswer();
        StateHasChanged();
    }

    public void OnSplitDataIntoChanged(int newValue)
    {
        splitDataInto = newValue;
        selectedDataChunk = 1;
        UpdateQuestions();
    }

    public void OnSelectedDataChunkIndexChanged(int newValue)
    {
        selectedDataChunk = newValue;
        UpdateQuestions();
    }
}
