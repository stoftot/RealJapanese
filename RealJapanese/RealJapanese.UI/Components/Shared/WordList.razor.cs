using DataLoaders.Models;
using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public class WordListBase<TItem> : ComponentBase where TItem : Word
{
    private readonly string _searchInputId = $"word-search-{Guid.NewGuid():N}";

    [Parameter] public string Title { get; set; } = string.Empty;

    // All words that *can* be shown/selected
    [Parameter] public List<TItem> AllWords { get; set; } = [];

    // IDs of the words that are currently selected in this list
    [Parameter] public List<int> SelectedIds { get; set; } = [];
    
    [Parameter] public EventCallback<TItem> WordSelected { get; set; }
    
    [Parameter] public EventCallback<TItem> WordDeselected { get; set; }

    protected string SearchText { get; set; } = string.Empty;
    protected string SearchInputId => _searchInputId;

    // Selected words at the top, in the order of SelectedIds;
    // unselected words below, sorted by Japanese.
    protected IEnumerable<TItem> OrderedItems =>
        AllWords
            .OrderBy(w => IsSelected(w.Id) ? 0 : 1)
            .ThenBy(w => w.Id);

    protected IEnumerable<TItem> FilteredItems => string.IsNullOrWhiteSpace(SearchText)
        ? OrderedItems
        : OrderedItems.Where(MatchesSearch);

    private bool MatchesSearch(TItem word) =>
        Contains(word.English, SearchText)
        || Contains(word.Japanese, SearchText)
        || Contains(word.Kana, SearchText);

    private static bool Contains(string? value, string search) =>
        value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true;

    private bool IsSelected(int id) => SelectedIds.Contains(id);

    protected async Task OnItemClicked(TItem item)
    {
        var wasSelected = SelectedIds.Contains(item.Id);

        if (wasSelected)
        {
            if (WordDeselected.HasDelegate)
                await WordDeselected.InvokeAsync(item);
        }
        else
        {
            if (WordSelected.HasDelegate)
                await WordSelected.InvokeAsync(item);
        }

        StateHasChanged();
    }
}
