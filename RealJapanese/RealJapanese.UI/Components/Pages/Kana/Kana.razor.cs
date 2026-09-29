using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Repositories.Kana;

namespace RealJapanese.Components.Pages.Kana;

public class KanaBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JS { get; set; } = null!;
    protected static readonly string[] Tabs = ["Hiragana", "Katakana", "Study", "Settings"];
    protected static readonly string[] FontNames = ["Noto Sans JP", "Noto Serif JP", "Zen Kurenaido", "Kaisei Tokumin", "Kiwi Maru", "Stick", "Shippori Antique B1", "Kaisei Opti", "Klee One"];
    protected KanaPreferences Preferences { get; private set; } = new();
    protected string View { get; private set; } = "Hiragana";
    protected bool Ready { get; private set; }
    protected string? StorageMessage { get; private set; }
    protected KanaSession? Session { get; private set; }
    protected bool ReviewMode { get; private set; }
    protected string Answer { get; private set; } = "";
    protected bool Wrong { get; private set; }
    protected int CurrentFont { get; private set; }
    protected ElementReference AnswerElement;
    private IJSObjectReference? storage;
    private bool focusAnswer;
    private string setKey = "";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    protected KanaBest? Best => Preferences.Best.GetValueOrDefault(setKey);
    protected IEnumerable<KanaGroup> Groups => Preferences.Script == KanaScript.Hiragana
        ? [KanaGroup.Single, KanaGroup.Double] : [KanaGroup.Single, KanaGroup.Double, KanaGroup.Extended];
    protected IEnumerable<KanaCharacter> GroupCards(KanaGroup group) => KanaCatalog.Characters.Where(c => c.Script == Preferences.Script && c.Group == group);
    protected int SelectedCount(KanaGroup group) => GroupCards(group).Count(IsSelected);
    protected bool IsSelected(KanaCharacter character) => Preferences.Selected.Contains(character.Id);
    protected List<KanaCharacter[]> Columns => GroupCards(Preferences.Group).GroupBy(c => c.Column).OrderBy(g => g.Key).Select(g => g.ToArray()).ToList();
    protected int RowCount => Preferences.Group == KanaGroup.Double ? 3 : 5;
    protected static string FontFamily(int index) => $"'Kana {FontNames[index]}', sans-serif";
    protected static string Pressed(bool selected) => selected ? "true" : "false";
    protected static string FormatTime(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                storage = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/RealJapanese.UI/js/kana-storage.js");
                var saved = await storage.InvokeAsync<StorageResult>("load");
                if (!saved.Available) StorageMessage = "Storage is unavailable. Your choices will last for this visit only.";
                if (saved.Json is { } json)
                {
                    try
                    {
                        var preferences = JsonSerializer.Deserialize<KanaPreferences>(json, JsonOptions);
                        if (preferences is { Version: 1 }) { preferences.Sanitize(); Preferences = preferences; }
                        else StorageMessage = "Your saved kana choices could not be read. Default choices are in use.";
                    }
                    catch (JsonException) { StorageMessage = "Your saved kana choices could not be read. Default choices are in use."; }
                }
            }
            catch (JSException) { StorageMessage = "Storage is unavailable. Your choices will last for this visit only."; }
            View = Preferences.Script.ToString();
            Ready = true;
            StateHasChanged();
        }
        if (focusAnswer && View == "Study" && Session is { Finished: false })
        {
            focusAnswer = false;
            await AnswerElement.FocusAsync();
        }
    }

    protected async Task Save()
    {
        if (storage is null) return;
        try
        {
            var saved = await storage.InvokeAsync<bool>("save", JsonSerializer.Serialize(Preferences));
            StorageMessage = saved ? null : "Your choices could not be saved. They will last for this visit only.";
        }
        catch (JSException) { StorageMessage = "Your choices could not be saved. They will last for this visit only."; }
    }

    protected async Task ChangeTab(string tab)
    {
        View = tab;
        if (tab == "Study") Start(false);
        else if (Enum.TryParse<KanaScript>(tab, out var script))
        {
            Preferences.Script = script;
            if (script == KanaScript.Hiragana && Preferences.Group == KanaGroup.Extended) Preferences.Group = KanaGroup.Single;
            await Save();
        }
    }

    protected async Task ChangeGroup(KanaGroup group) { Preferences.Group = group; await Save(); }
    protected async Task ToggleCharacter(KanaCharacter character)
    {
        if (!Preferences.Selected.Add(character.Id)) Preferences.Selected.Remove(character.Id);
        await Save();
    }
    protected async Task ToggleColumn(KanaCharacter[] column)
    {
        var remove = column.All(IsSelected);
        foreach (var c in column) { if (remove) Preferences.Selected.Remove(c.Id); else Preferences.Selected.Add(c.Id); }
        await Save();
    }
    protected async Task SelectGroup(bool selected)
    {
        foreach (var c in GroupCards(Preferences.Group)) { if (selected) Preferences.Selected.Add(c.Id); else Preferences.Selected.Remove(c.Id); }
        await Save();
    }
    protected async Task ToggleFont(int font)
    {
        if (Preferences.Fonts.Contains(font)) { if (Preferences.Fonts.Count > 1) Preferences.Fonts.Remove(font); }
        else Preferences.Fonts.Add(font);
        await Save();
    }
    protected void Start(bool review)
    {
        ReviewMode = review;
        var ids = review ? Preferences.ReviewCards : Preferences.Selected;
        var cards = KanaCatalog.Characters.Where(c => ids.Contains(c.Id)).ToArray();
        Session = new KanaSession(cards, Preferences.RandomOrder);
        // High scores compare like-for-like sets, including the selected fonts and answer mode.
        var signature = string.Join(',', cards.Select(c => c.Id).Order()) + ";" + string.Join(',', Preferences.Fonts.Order())
            + $";{Preferences.AutoSubmit};{Preferences.RandomOrder}";
        setKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        ResetAnswer();
    }
    private void ResetAnswer()
    {
        Answer = "";
        Wrong = false;
        var fonts = Preferences.Fonts.ToArray();
        CurrentFont = fonts[Random.Shared.Next(fonts.Length)];
        focusAnswer = true;
    }
    protected async Task Input(string value)
    {
        Answer = value;
        Wrong = false;
        if (Preferences.AutoSubmit && Session?.Current?.Accepts(Answer) == true) await CheckAnswer();
    }
    protected async Task Submit()
    {
        if (Session is null || Session.Finished) return;
        if (Session.Revealed) { Session.Skip(); await AfterAdvance(); }
        else if (Wrong) await Reveal();
        else if (string.IsNullOrWhiteSpace(Answer)) await Reveal();
        else await CheckAnswer();
    }
    private async Task CheckAnswer()
    {
        if (Session?.Current is not { } card) return;
        var missed = Session.Missed;
        if (Session.Answer(Answer))
        {
            if (Preferences.Review && !missed) Preferences.ReviewCards.Remove(card.Id);
            await AfterAdvance();
        }
        else
        {
            Wrong = true;
            if (Preferences.Review) Preferences.ReviewCards.Add(card.Id);
            focusAnswer = true;
            await Save();
        }
    }
    protected async Task Reveal()
    {
        if (Session?.Current is not { } card) return;
        Session.Reveal();
        Wrong = false;
        if (Preferences.Review) Preferences.ReviewCards.Add(card.Id);
        focusAnswer = true;
        await Save();
    }
    private async Task AfterAdvance()
    {
        ResetAnswer();
        if (Session is { Finished: true } && Preferences.HighScores && !ReviewMode)
        {
            var previous = Best;
            if (previous is null && Preferences.Best.Count >= 100) Preferences.Best.Remove(Preferences.Best.Keys.First());
            Preferences.Best[setKey] = new KanaBest(Math.Max(previous?.Percent ?? 0, Session.Percent),
                Math.Min(previous?.Seconds ?? double.MaxValue, Session.Seconds));
        }
        await Save();
    }
    protected async Task ToggleReview()
    {
        if (Session?.Current is not { } card) return;
        if (!Preferences.ReviewCards.Add(card.Id)) Preferences.ReviewCards.Remove(card.Id);
        await Save();
    }
    protected async Task ClearReview()
    {
        Preferences.ReviewCards.Clear();
        if (ReviewMode) Start(true);
        await Save();
    }
    public async ValueTask DisposeAsync()
    {
        if (storage is null) return;
        try { await storage.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
    public sealed record StorageResult(string? Json, bool Available);
}
