using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;

namespace RealJapanese.Components.Pages.Words;

public class MixedWordPracticeBase : ComponentBase
{
    [Inject] private RepositoryPaths Paths { get; set; } = null!;
    [SupplyParameterFromQuery(Name = "sources")] public string? Sources { get; set; }
    [SupplyParameterFromQuery(Name = "category")] public string? Category { get; set; }
    private VocabularyCollection collection = null!;
    protected List<VocabularyEntry> PracticeWords { get; private set; } = [];
    protected (string?, string) PracticeKey => (Category, collection.SerializeMixerSources(collection.ParseMixerSources(Sources)));
    protected string BackUrl => $"words?category={Category}&sources={Uri.EscapeDataString(PracticeKey.Item2)}";

    protected override void OnInitialized() => collection = new(Paths);
    protected override void OnParametersSet() => PracticeWords = collection.SavedWords(collection.ParseMixerSources(Sources),
        WordPracticeCategoryExtensions.ParseQueryValue(Category));

    protected bool RecordPracticeAnswer(Word word, bool correct) => collection.RecordPracticeAnswer(word, correct);
}
