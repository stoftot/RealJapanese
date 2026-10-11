using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;
public abstract class SingleAnwserBase : PracticeBase
{
    protected int splitDataInto = 1;
    protected int selectedDataChunk = 1;
    private int selectedDataChunkIndex => selectedDataChunk - 1;
    private string _oldUserInput = string.Empty;

    protected override void UpdateQuestions()
    {
        RefreshQuestionPool();
        Questions = OrginalQuestions
            .GetChunk(splitDataInto, selectedDataChunkIndex);
        RefreshQuestions();
    }
    
    protected virtual void RefreshQuestions()
    {
        Questions.Shuffle();

        ResetRoundState();
        _oldUserInput = string.Empty;
        OnQuestionsUpdated();
    }

    protected virtual void OnQuestionsUpdated()
    {
    }

    protected override void OnUserInputChanged(string value)
    {
        base.OnUserInputChanged(value);

        if (Questions.Count > 0 && IsCorrect())
            _ = CompleteQuestionAsync();
    }

    protected override void OnEnterPressed()
    {
        if (Questions.Count == 0)
            return;

        if (showAnswer && _oldUserInput == UserInput)
        {
            _ = GoToNextQuestionAsync();
        }
        else
        {
            if (IsCorrect())
                _ = CompleteQuestionAsync();
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
