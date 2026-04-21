using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Kanji;

public class KanjiSingleMeaningBase : MultipleAnswerBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }
    
    [Inject]
    public KanjiData KanjiData{ get; set; } = null!;

    // ref to the shared UI so we can focus the input
    protected PracticeCardMultipelAnswers cardRef = null!;

    protected override void OnInitialized()
    {
        WordDataBase<Word> data = KanjiData.Single;
        OrginalQuestions = data.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .JapaneseToEnglishQuestions();
        
        UpdateQuestions();
    }
    
    protected override string Normalize(string? s) =>
        (s ?? string.Empty).Trim().ToLowerInvariant();   

    protected override Task FocusAnswerInputAsync()
        => cardRef.ClearAndFocusInputAsync();
}
