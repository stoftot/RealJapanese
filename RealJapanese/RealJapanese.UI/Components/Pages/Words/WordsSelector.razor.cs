using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;

namespace RealJapanese.Components.Pages.Words;

public class WordsSelectorBase : ComponentBase
{
    [Inject] private RepositoryPaths Paths { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [SupplyParameterFromQuery(Name = "sources")] public string? Sources { get; set; }
    [SupplyParameterFromQuery(Name = "category")] public string? Category { get; set; }
    protected VocabularyCollection Collection { get; private set; } = null!;
    protected HashSet<string> EnabledSources { get; private set; } = [];
    protected string SourcesQuery => "&sources=" + Uri.EscapeDataString(Collection.SerializeMixerSources(EnabledSources));

    protected override void OnInitialized() => Collection = new(Paths);
    protected override void OnParametersSet() => EnabledSources = Collection.ParseMixerSources(Sources);

    protected void Toggle(string key)
    {
        if (!EnabledSources.Add(key)) EnabledSources.Remove(key);
        Sources = Collection.SerializeMixerSources(EnabledSources);
        Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("sources", Sources), replace: true);
    }
}
