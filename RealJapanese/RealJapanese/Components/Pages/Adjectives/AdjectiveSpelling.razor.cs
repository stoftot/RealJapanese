using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Adjectives;

public class AdjectiveSpellingBase : MultipleAnswerBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }
    
    [Inject]
    public AdjectiveData AdjectiveData { get; set; } = null!;

    protected PracticeCardMultipelAnswers cardRef = null!;

    protected override void OnInitialized()
    {
        OrginalQuestions = AdjectiveData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .EnglishToRomajiAndTypeQuestion();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}
