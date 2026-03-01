using DataLoaders.Exstensions;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Verbs;

public class VerbSpellingBase : SingleAnwserBase
{
    [SupplyParameterFromQuery(Name = "training")]
    public bool Training { get; set; } = false;
    
    [Inject]
    public VerbData VerbData { get; set; } = null!;

    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = (Training ? VerbData.TrainingWords : VerbData.VocabWords)
            .EnglishToRomajiQuestions();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}