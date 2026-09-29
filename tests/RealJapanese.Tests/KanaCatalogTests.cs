using Repositories.Kana;

namespace RealJapanese.Tests;

/// <summary>Checks the character inventory, chart positions and independently specified kana readings.</summary>
public sealed class KanaCatalogTests
{
    [Fact]
    public void Characters_ContainsExpectedStableCatalogCounts()
    {
        Assert.Equal(244, KanaCatalog.Characters.Count);
        Assert.Equal(244, KanaCatalog.Characters.Select(character => character.Id).Distinct().Count());

        foreach (var script in Enum.GetValues<KanaScript>())
        {
            Assert.Equal(71, KanaCatalog.Characters.Count(character =>
                character.Script == script && character.Group == KanaGroup.Single));
            Assert.Equal(36, KanaCatalog.Characters.Count(character =>
                character.Script == script && character.Group == KanaGroup.Double));
        }

        Assert.Equal(30, KanaCatalog.Characters.Count(character => character.Group == KanaGroup.Extended));
        Assert.All(KanaCatalog.Characters.Where(character => character.Group == KanaGroup.Extended),
            character => Assert.Equal(KanaScript.Katakana, character.Script));
    }

    [Theory]
    [InlineData("h-し", "shi", "si")]
    [InlineData("k-チ", "chi", "ti")]
    [InlineData("h-つ", "tsu", "tu")]
    [InlineData("k-フ", "fu", "hu")]
    [InlineData("h-ぢ", "ji", "di")]
    [InlineData("k-ヅ", "zu", "du")]
    [InlineData("h-ん", "n", "nn")]
    [InlineData("k-ヲ", "wo", "o")]
    public void IrregularSingles_ExposeExpectedPrimaryReadingsAndAliases(string id, string romaji, string alias)
    {
        var character = Assert.Single(KanaCatalog.Characters, character => character.Id == id);

        Assert.Equal(romaji, character.Romaji);
        Assert.Contains(alias, character.Aliases);
        Assert.True(character.Accepts(alias));
    }

    [Fact]
    public void SingleKana_UsesNFinalRowAndKeepsZuDistinctFromJiAlias()
    {
        var n = Assert.Single(KanaCatalog.Characters, character => character.Id == "h-ん");
        var zu = Assert.Single(KanaCatalog.Characters, character => character.Id == "h-ず");

        Assert.Equal(4, n.Row);
        Assert.Equal(10, n.Column);
        Assert.False(zu.Accepts("zi"));
    }

    [Theory]
    [InlineData("h-しゃ", "sha", "sya")]
    [InlineData("k-チャ", "cha", "tya")]
    [InlineData("h-じゃ", "ja", "jya")]
    [InlineData("k-ヂュ", "ju", "dyu")]
    [InlineData("h-ぴょ", "pyo", "pyo")]
    public void DoubleKana_ExposeExpectedReadingsAndAliases(string id, string romaji, string accepted)
    {
        var character = Assert.Single(KanaCatalog.Characters, character => character.Id == id);

        Assert.Equal(KanaGroup.Double, character.Group);
        Assert.Equal(romaji, character.Romaji);
        Assert.True(character.Accepts(accepted));
    }

    [Theory]
    [InlineData("k-イェ", "ye")]
    [InlineData("k-ヴ", "vu")]
    [InlineData("k-シェ", "she")]
    [InlineData("k-トゥ", "tu")]
    [InlineData("k-ツィ", "tsi")]
    [InlineData("k-ファ", "fa")]
    [InlineData("k-ヴュ", "vyu")]
    [InlineData("k-デュ", "dyu")]
    [InlineData("k-フュ", "fyu")]
    public void ExtendedKatakana_ContainsExpectedReadings(string id, string romaji)
    {
        var character = Assert.Single(KanaCatalog.Characters, character => character.Id == id);

        Assert.Equal(KanaScript.Katakana, character.Script);
        Assert.Equal(KanaGroup.Extended, character.Group);
        Assert.Equal(romaji, character.Romaji);
    }

    [Theory]
    [InlineData("k-イェ", 0, 3)]
    [InlineData("k-ウィ", 1, 1)]
    [InlineData("k-ヴャ", 10, 0)]
    [InlineData("k-ヴュ", 10, 2)]
    [InlineData("k-ヴョ", 10, 4)]
    public void Characters_UsesVowelRowsForExtendedColumns(string id, int column, int row)
    {
        var character = Assert.Single(KanaCatalog.Characters, character => character.Id == id);

        Assert.Equal(column, character.Column);
        Assert.Equal(row, character.Row);
    }

    [Theory]
    [InlineData(" Shi ", true)]
    [InlineData("SI", true)]
    [InlineData("し", false)]
    [InlineData("shі", false)] // The final character is Cyrillic, not ASCII i.
    [InlineData("sha!", false)]
    [InlineData("chi", false)]
    public void Accepts_MatchesOnlyTrimmedCaseInsensitiveAsciiRomaji(string answer, bool expected)
    {
        var character = Assert.Single(KanaCatalog.Characters, character => character.Id == "h-し");

        Assert.Equal(expected, character.Accepts(answer));
    }
}
