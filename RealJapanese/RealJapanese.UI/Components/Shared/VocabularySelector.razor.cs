using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Repositories.Bases;

namespace RealJapanese.Components.Shared;

public class VocabularySelectorBase<TItem> : WordComponentBase<TItem> where TItem : Word
{
    [Parameter, EditorRequired] public WordDataBase<TItem> Repository { get; set; } = null!;
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter, EditorRequired] public string RoutePrefix { get; set; } = "";
    protected override WordDataBase<TItem> WordData => Repository;

    protected List<PraticeSelectorBase.PageDescreption> PracticePages =>
    [
        new()
        {
            Title = "Learn the words",
            Description = "Practice the spelling of the words",
            Url = $"spelling?category={SelectedCategoryQueryValue}"
        },
        new()
        {
            Title = "Flash cards",
            Description = "Reveal the answer, then move to the next word",
            Url = $"flashcards?category={SelectedCategoryQueryValue}"
        }
    ];
}
