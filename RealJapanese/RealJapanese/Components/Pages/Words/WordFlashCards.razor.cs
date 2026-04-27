using DataLoaders.Exstensions;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Words;

public class WordFlashCardsBase : FlashCardPracticeBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }

    [Inject]
    public WordData WordData { get; set; } = null!;

    protected FlashPracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = WordData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .KanaToEnglishQuestions();

        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef?.FocusCardAsync() ?? Task.CompletedTask;
}
