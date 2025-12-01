using DataLoaders.Models;
using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public class WordListBase : ComponentBase
{
    [Parameter] public string Title { get; set; } = string.Empty;

    // All words that *can* be shown/selected
    [Parameter] public List<Word> AllWords { get; set; } = [];

    // IDs of the words that are currently selected in this list
    [Parameter] public List<int> SelectedIds { get; set; } = [];
    
    [Parameter] public EventCallback<Word> WordSelected { get; set; }
    
    [Parameter] public EventCallback<Word> WordDeselected { get; set; }

    // Selected words at the top, in the order of SelectedIds;
    // unselected words below, sorted by Japanese.
    protected IEnumerable<Word> OrderedItems =>
        AllWords
            .OrderBy(w => IsSelected(w.Id) ? 0 : 1)
            .ThenBy(w => w.Id);

    private bool IsSelected(int id) => SelectedIds.Contains(id);

    protected async Task OnItemClicked(Word item)
    {
        var wasSelected = SelectedIds.Contains(item.Id);

        if (wasSelected)
        {
            // toggle off
            SelectedIds.Remove(item.Id);
            if (WordDeselected.HasDelegate)
                await WordDeselected.InvokeAsync(item);
        }
        else
        {
            // newly selected → insert at front so it appears at the very top
            SelectedIds.Add(item.Id);
            if (WordSelected.HasDelegate)
                await WordSelected.InvokeAsync(item);
        }

        

        StateHasChanged();
    }
}