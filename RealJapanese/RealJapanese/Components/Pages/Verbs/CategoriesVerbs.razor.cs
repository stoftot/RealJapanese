using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Verbs;

public class CategoriesVerbsBase : SingleAnwserBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }
    
    [Inject]
    public VerbData VerbData { get; set; } = null!;

    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = VerbData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .KanaToTypeQuestion();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}
