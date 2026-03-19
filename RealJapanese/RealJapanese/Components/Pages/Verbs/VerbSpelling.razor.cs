using DataLoaders.Exstensions;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Verbs;

public class VerbSpellingBase : MultipleAnswerBase
{
    [SupplyParameterFromQuery(Name = "training")]
    public bool Training { get; set; } = false;
    
    [Inject]
    public VerbData VerbData { get; set; } = null!;

    // ref to the shared UI so we can focus the input
    protected PracticeCardMultipelAnswers cardRef;

    protected override void OnInitialized()
    {
        // OrginalQuestions = (Training ? VerbData.TrainingWords : VerbData.VocabWords)
        //     .EnglishToRomajiQuestions();
        
        OrginalQuestions = (Training ? VerbData.TrainingWords : VerbData.VocabWords)
            .EnglishToRomajiAndTypeQuestion();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}