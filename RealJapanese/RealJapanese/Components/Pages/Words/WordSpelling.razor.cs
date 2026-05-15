using DataLoaders.Exstensions;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Words;

public class WordSpellingBase : SingleAnwserBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }
    
    [Inject]
    public WordData WordData { get; set; } = null!;

    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = WordData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .EnglishToRomajiQuestions();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}
