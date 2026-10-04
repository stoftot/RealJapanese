using DataLoaders.Exstensions;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Nouns;

public class NounFlashCardsBase : FlashCardPracticeBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }

    [Inject]
    public NounData NounData { get; set; } = null!;

    protected FlashPracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = NounData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .KanaToEnglishQuestions();

        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef?.FocusCardAsync() ?? Task.CompletedTask;
}
