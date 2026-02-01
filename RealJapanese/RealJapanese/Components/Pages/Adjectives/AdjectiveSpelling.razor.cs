using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Adjectives;

public class AdjectiveSpellingBase : PracticeBase
{
    [SupplyParameterFromQuery(Name = "training")]
    public bool Training { get; set; } = false;
    
    [Inject]
    public AdjectiveData AdjectiveData { get; set; } = null!;

    protected int splitDataInto = 1;

    private int selectedDataChunkIndex => selectedDataChunk-1;
    protected int selectedDataChunk = 1;

    protected int DataChunkSize => OrginalQuestions.Count() / splitDataInto;

    private string _oldUserInput = string.Empty;

    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;
    protected override void OnInitialized()
    {
        OrginalQuestions = (Training ? AdjectiveData.TrainingWords : AdjectiveData.VocabWords)
            .EnglishToRomajiQuestions();
        
        UpdateQuestions();
    }
    
    private void UpdateQuestions()
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