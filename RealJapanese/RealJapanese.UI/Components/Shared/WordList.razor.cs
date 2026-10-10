using System.Globalization;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public class WordListBase<TItem> : ComponentBase where TItem : Word
{
    private readonly string _searchInputId = $"word-search-{Guid.NewGuid():N}";
    private readonly HashSet<string> _collapsedGroups = [];
    private readonly HashSet<string> _collapsedSearchGroups = [];
    private readonly Dictionary<string, string> _groupPanelIds = [];
    private HashSet<int> _selectedIds = [];
    private string _searchText = string.Empty;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public List<TItem> AllWords { get; set; } = [];
    [Parameter] public List<int> SelectedIds { get; set; } = [];
    [Parameter] public EventCallback<TItem> WordSelected { get; set; }
    [Parameter] public EventCallback<TItem> WordDeselected { get; set; }
    [Parameter] public EventCallback<List<TItem>> WordsSelected { get; set; }
    [Parameter] public EventCallback<List<TItem>> WordsDeselected { get; set; }

    protected string SearchText
    {
        get => _searchText;
        set
        {
            if (!string.Equals(_searchText.Trim(), value.Trim(), StringComparison.Ordinal))
                _collapsedSearchGroups.Clear();
            _searchText = value;
        }
    }

    protected string SearchInputId => _searchInputId;
    protected bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);
    protected sealed record WordGroup(string Key, string Label, List<TItem> Items, List<WordGroup> Lessons);

    protected List<WordGroup> VisibleGroups
    {
        get
        {
            _selectedIds = SelectedIds.ToHashSet();
            var words = AllWords.Where(word => !IsSearching || MatchesSearch(word))
                .SelectMany(word => Origins(word).Select(origin => (Word: word, Origin: ParseOrigin(origin))));

            return words.GroupBy(entry => (entry.Origin.Book, entry.Origin.IsGeneral))
                .OrderBy(group => group.Key.IsGeneral ? 1 : 0)
                .ThenBy(group => group.Key.Book, StringComparer.OrdinalIgnoreCase)
                .Select(book =>
                {
                    var key = $"{book.Key.IsGeneral}:{book.Key.Book.Length}:{book.Key.Book}";
                    var items = OrderItems(book.Select(entry => entry.Word));
                    List<WordGroup> lessons = book.Key.IsGeneral ? [] : book.GroupBy(entry => entry.Origin.Lesson)
                        .OrderBy(lesson => lesson.Key ?? int.MaxValue)
                        .Select(lesson => new WordGroup($"{key}:{lesson.Key}",
                            lesson.Key is int number ? $"Lesson {number}" : "Other words",
                            OrderItems(lesson.Select(entry => entry.Word)), [])).ToList();
                    return new WordGroup(key, book.Key.Book, items, lessons);
                }).ToList();
        }
    }

    private List<TItem> OrderItems(IEnumerable<TItem> words) => words.DistinctBy(word => word.Id)
        .OrderBy(word => IsSelected(word.Id) ? 0 : 1).ThenBy(word => word.Id).ToList();

    private static IEnumerable<string> Origins(TItem word) =>
        new[] { word.Origin }.Concat(word.AdditionalOrigins ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).DefaultIfEmpty("");

    private static (string Book, int? Lesson, bool IsGeneral) ParseOrigin(string? origin)
    {
        var value = origin?.Trim();
        if (string.IsNullOrEmpty(value) || value.Equals("self added", StringComparison.OrdinalIgnoreCase))
            return ("General", null, true);

        var separator = value.LastIndexOf('-');
        if (separator > 0 && int.TryParse(value[(separator + 1)..], NumberStyles.None,
                CultureInfo.InvariantCulture, out var lesson) && !string.IsNullOrWhiteSpace(value[..separator]))
            return (value[..separator].Trim(), lesson, false);

        return (value, null, false);
    }

    private bool MatchesSearch(TItem word) => Contains(word.English, SearchText)
        || Contains(word.Japanese, SearchText)
        || Contains(word.Kana, SearchText)
        || Origins(word).Any(origin => Contains(origin, SearchText));

    private static bool Contains(string? value, string search) =>
        value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true;

    protected bool IsSelected(int id) => _selectedIds.Contains(id);
    protected bool IsExpanded(WordGroup group) =>
        !(IsSearching ? _collapsedSearchGroups : _collapsedGroups).Contains(group.Key);

    protected void ToggleExpanded(WordGroup group)
    {
        var collapsed = IsSearching ? _collapsedSearchGroups : _collapsedGroups;
        if (!collapsed.Add(group.Key))
            collapsed.Remove(group.Key);
    }

    protected string PanelId(WordGroup group)
    {
        if (!_groupPanelIds.TryGetValue(group.Key, out var id))
            _groupPanelIds[group.Key] = id = $"{_searchInputId}-group-{_groupPanelIds.Count}";
        return id;
    }

    protected int SelectedCount(WordGroup group) => group.Items.Count(word => IsSelected(word.Id));

    protected string CheckedState(WordGroup group)
    {
        var count = SelectedCount(group);
        return count == 0 ? "false" : count == group.Items.Count ? "true" : "mixed";
    }

    protected string SelectionSymbol(WordGroup group) => CheckedState(group) switch
    {
        "true" => "☑",
        "mixed" => "−",
        _ => "☐"
    };

    protected string SelectionLabel(WordGroup group) =>
        $"{(CheckedState(group) == "true" ? "Deselect" : "Select")} all {group.Items.Count} {(IsSearching ? "matching" : "eligible")} words in {group.Label}";

    protected async Task OnGroupClicked(WordGroup group)
    {
        var deselect = CheckedState(group) == "true";
        var changed = group.Items.Where(word => IsSelected(word.Id) == deselect).DistinctBy(word => word.Id).ToList();
        var bulkCallback = deselect ? WordsDeselected : WordsSelected;
        var itemCallback = deselect ? WordDeselected : WordSelected;
        if (bulkCallback.HasDelegate)
            await bulkCallback.InvokeAsync(changed);
        else if (itemCallback.HasDelegate)
            foreach (var word in changed)
                await itemCallback.InvokeAsync(word);
    }

    protected async Task OnItemClicked(TItem item)
    {
        if (SelectedIds.Contains(item.Id))
        {
            if (WordDeselected.HasDelegate)
                await WordDeselected.InvokeAsync(item);
        }
        else if (WordSelected.HasDelegate)
            await WordSelected.InvokeAsync(item);
    }
}
