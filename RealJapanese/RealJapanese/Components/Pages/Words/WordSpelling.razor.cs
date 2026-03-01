using DataLoaders.Exstensions;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Words;

public class WordSpellingBase : SingleAnwserBase
{
    [SupplyParameterFromQuery(Name = "training")]
    public bool Training { get; set; } = false;
    
    [Inject]
    public WordData WordData { get; set; } = null!;

    // ref to the shared UI so we can focus the input
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = (Training ? WordData.TrainingWords : WordData.VocabWords)
            .EnglishToRomajiQuestions();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}