using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Verbs;

public class VerbSpellingBase : MultipleAnswerBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }
    
    [Inject]
    public VerbData VerbData { get; set; } = null!;

    protected PracticeCardMultipelAnswers cardRef = null!;

    protected override void OnInitialized()
    {
        OrderedAnswers = true;
        OrginalQuestions = VerbData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .EnglishToRomajiAndTypeQuestion();
        
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}
