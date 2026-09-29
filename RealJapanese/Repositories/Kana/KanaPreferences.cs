namespace Repositories.Kana;

/// <summary>Device-local kana choices, independent of vocabulary progress and sync.</summary>
public sealed class KanaPreferences
{
    public int Version { get; set; } = 1;
    public HashSet<string> Selected { get; set; } = ["h-あ", "h-い", "h-う", "h-え", "h-お"];
    public HashSet<string> ReviewCards { get; set; } = [];
    public HashSet<int> Fonts { get; set; } = [0];
    public KanaScript Script { get; set; }
    public KanaGroup Group { get; set; }
    public bool AutoSubmit { get; set; } = true;
    public bool HighScores { get; set; } = true;
    public bool RandomOrder { get; set; } = true;
    public bool Review { get; set; } = true;
    public bool Scoring { get; set; } = true;
    public Dictionary<string, KanaBest> Best { get; set; } = [];

    public void Sanitize()
    {
        var valid = KanaCatalog.Characters.Select(c => c.Id).ToHashSet();
        Selected = (Selected ?? []).Where(valid.Contains).ToHashSet();
        ReviewCards = (ReviewCards ?? []).Where(valid.Contains).ToHashSet();
        Fonts = (Fonts ?? []).Where(f => f is >= 0 and < 9).ToHashSet();
        if (Fonts.Count == 0) Fonts.Add(0);
        if (!Enum.IsDefined(Script)) Script = KanaScript.Hiragana;
        if (!Enum.IsDefined(Group) || Script == KanaScript.Hiragana && Group == KanaGroup.Extended)
            Group = KanaGroup.Single;
        Best = (Best ?? []).Where(p => p.Value is not null && p.Value.Percent is >= 0 and <= 100
                && double.IsFinite(p.Value.Seconds) && p.Value.Seconds >= 0)
            .Take(100).ToDictionary(p => p.Key, p => p.Value);
    }
}

public sealed record KanaBest(int Percent, double Seconds);
