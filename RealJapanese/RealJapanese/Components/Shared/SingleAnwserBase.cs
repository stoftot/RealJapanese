using System;
using System.Linq;
using System.Threading.Tasks;

namespace RealJapanese.Components.Shared;
public abstract class SingleAnwserBase : PracticeBase
{
    protected int splitDataInto = 1;
    protected int selectedDataChunk = 1;
    private int selectedDataChunkIndex => selectedDataChunk - 1;
    protected int selectedDataChunkPublic => selectedDataChunk; // if derived code referenced the field name, keep access via property
    protected int DataChunkSize => OrginalQuestions.Count() / splitDataInto;
    private string _oldUserInput = string.Empty;

    protected void UpdateQuestions()
    {
        Questions = OrginalQuestions
            .Skip(selectedDataChunkIndex * DataChunkSize)
            .Take(selectedDataChunk == splitDataInto ? int.MaxValue : DataChunkSize)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        currentQuestionIndex = 0;
        UserInput = string.Empty;
        showAnswer = false;
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
