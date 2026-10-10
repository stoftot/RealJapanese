using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Repositories.Bases;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;

public class VocabularyFlashCardsBase<TItem> : FlashCardPracticeBase where TItem : Word
{
    [Parameter, EditorRequired] public WordDataBase<TItem> Repository { get; set; } = null!;
    [Parameter] public string? Category { get; set; }
    protected FlashPracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = Repository.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .KanaToEnglishQuestions();
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync() => cardRef?.FocusCardAsync() ?? Task.CompletedTask;
}
