using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Repositories.Bases;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;

public class VocabularySpellingBase<TItem> : SingleAnwserBase where TItem : Word
{
    [Parameter, EditorRequired] public WordDataBase<TItem> Repository { get; set; } = null!;
    [Parameter] public string? Category { get; set; }
    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = Repository.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .EnglishToRomajiQuestions();
        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync() => cardRef?.ClearAndFocusInputAsync() ?? Task.CompletedTask;
}
