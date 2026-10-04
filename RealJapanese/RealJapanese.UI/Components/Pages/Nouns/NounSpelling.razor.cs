using DataLoaders.Exstensions;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Pages.Nouns;

public class NounSpellingBase : SingleAnwserBase
{
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }

    [Inject]
    public NounData NounData { get; set; } = null!;

    protected PracticeCard? cardRef;

    protected override void OnInitialized()
    {
        OrginalQuestions = NounData.GetWords(WordPracticeCategoryExtensions.ParseQueryValue(Category))
            .EnglishToRomajiQuestions();

        UpdateQuestions();
    }

    protected override Task FocusAnswerInputAsync()
        => cardRef?.ClearAndFocusInputAsync() ?? Task.CompletedTask;
}
