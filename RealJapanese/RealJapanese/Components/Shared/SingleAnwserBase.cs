using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;
public abstract class SingleAnwserBase : PracticeBase
{
    protected int splitDataInto = 1;
    protected int selectedDataChunk = 1;
    private int selectedDataChunkIndex => selectedDataChunk - 1;
    private string _oldUserInput = string.Empty;

    protected void UpdateQuestions()
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
        UserInput = string.Empty;
        showAnswer = false;
    }

    protected virtual void OnQuestionsUpdated()
    {
    }

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
