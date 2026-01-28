using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Kanji;

public class KanjiSingleMeaningBase : PracticeBase
{
    [SupplyParameterFromQuery(Name = "training")]
    public bool Training { get; set; } = false;
    
    [Inject]
    public KanjiData KanjiData{ get; set; } = null!;
    
    protected int splitDataInto = 1;
    
    protected List<string> GivenAnswers { get; set; } = [];
    protected List<string> MissingAnswers { get; set; } = [];

    private int selectedDataChunkIndex => selectedDataChunk-1;
    protected int selectedDataChunk = 1;

    protected int DataChunkSize => OrginalQuestions.Count() / splitDataInto;

    private string _oldUserInput = string.Empty;

    // ref to the shared UI so we can focus the input
    protected PracticeCardMultipelAnswers cardRef;
    protected override void OnInitialized()
    {
        WordDataBase<Word> data = KanjiData.Single;
        OrginalQuestions = (Training ? data.TrainingWords : data.VocabWords)
            .JapaneseToEnglishQuestions();
        
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
        GivenAnswers.Clear();
        MissingAnswers = Questions[currentQuestionIndex].Answer.Split(";").ToList();
    }
    
    protected override string Normalize(string? s) =>
        (s ?? string.Empty).Trim().ToLowerInvariant();   

    protected override bool IsCorrect()
    {
        var givenAnswer = Normalize(UserInput);

        if (MissingAnswers.Contains(givenAnswer))
        {
            GivenAnswers.Add(givenAnswer);
            MissingAnswers.Remove(givenAnswer);
            UserInput = string.Empty;
        }
        
        return MissingAnswers.Count == 0;
    }

    protected override void NextQuestion()
    {
        base.NextQuestion();
        GivenAnswers.Clear();
        MissingAnswers = Questions[currentQuestionIndex].Answer.Split(";").ToList();
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