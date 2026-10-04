using DataLoaders.Models;
using DataLoaders.Models.Genki;

namespace RealJapanese.Tests;

/// <summary>Checks schema-form resolution against literal Japanese and kana expectations.</summary>
public sealed class GenkiFormsTests
{
    [Theory]
    [InlineData("買う", "かう", "u", "polite", "買います", "かいます")]
    [InlineData("買う", "かう", "u", "politeNegative", "買いません", "かいません")]
    [InlineData("食べる", "たべる", "ru", "politePast", "食べました", "たべました")]
    [InlineData("する", "する", "irregular", "stem", "し", "し")]
    [InlineData("来る", "くる", "irregular", "short", "来る", "くる")]
    [InlineData("行く", "いく", "u", "te", "行って", "いって")]
    [InlineData("読む", "よむ", "u", "negativeStem", "読ま", "よま")]
    [InlineData("食べる", "たべる", "ru", "tai", "食べたい", "たべたい")]
    [InlineData("食べる", "たべる", "ru", "taiNegative", "食べたくない", "たべたくない")]
    [InlineData("食べる", "たべる", "ru", "taiPast", "食べたかった", "たべたかった")]
    [InlineData("食べる", "たべる", "ru", "taiPastNegative", "食べたくなかった", "たべたくなかった")]
    [InlineData("食べる", "たべる", "ru", "taiPolite", "食べたいです", "たべたいです")]
    [InlineData("食べる", "たべる", "ru", "taiPoliteNegative", "食べたくないです", "たべたくないです")]
    [InlineData("食べる", "たべる", "ru", "taiPolitePast", "食べたかったです", "たべたかったです")]
    [InlineData("食べる", "たべる", "ru", "taiPolitePastNegative", "食べたくなかったです", "たべたくなかったです")]
    public void VerbForms_RenderWithExistingConjugators(string japanese, string kana, string type, string form,
        string expectedJapanese, string expectedKana)
    {
        var verb = new Verb { Japanese = japanese, Kana = kana, English = "test", Type = type };

        Assert.True(GenkiForms.Supports(verb, form));
        Assert.Equal(expectedJapanese, GenkiForms.Render(verb, form));
        Assert.Equal(expectedKana, GenkiForms.Render(verb, form, kana: true));
    }

    [Theory]
    [InlineData("高い", "たかい", "i", "polite", "高いです", "たかいです")]
    [InlineData("高い", "たかい", "i", "short", "高い", "たかい")]
    [InlineData("静か", "しずか", "na", "short", "静かだ", "しずかだ")]
    [InlineData("高い", "たかい", "i", "adverbial", "高く", "たかく")]
    [InlineData("静か", "しずか", "na", "attributive", "静かな", "しずかな")]
    [InlineData("静か", "しずか", "na", "adverbial", "静かに", "しずかに")]
    [InlineData("静か", "しずか", "na", "te", "静かで", "しずかで")]
    [InlineData("いい", "いい", "irregular", "adjectiveStem", "よ", "よ")]
    [InlineData("いい", "いい", "irregular", "te", "よくて", "よくて")]
    public void AdjectiveForms_RenderWithExistingConjugators(string japanese, string kana, string type, string form,
        string expectedJapanese, string expectedKana)
    {
        var adjective = new Adjective { Japanese = japanese, Kana = kana, English = "test", Type = type };

        Assert.True(GenkiForms.Supports(adjective, form));
        Assert.Equal(expectedJapanese, GenkiForms.Render(adjective, form));
        Assert.Equal(expectedKana, GenkiForms.Render(adjective, form, kana: true));
    }

    [Theory]
    [InlineData("〜年生", "〜ねんせい", "年生", "ねんせい", "g01-01")]
    [InlineData("〜時", "〜じ", "時", "じ", "g01-01")]
    [InlineData("〜歳", "〜さい", "歳", "さい", "g01-01")]
    [InlineData("〜番", "〜ばん", "番", "ばん", "g01-01")]
    [InlineData("〜枚", "〜まい", "枚", "まい", "g05-06")]
    [InlineData("〜時間", "〜じかん", "時間", "じかん", "g04-06")]
    [InlineData("〜人", "〜にん", "人", "にん", "g07-06")]
    public void CounterSuffix_OnlyRemovesTheSupportedTemplateMarker(string japanese, string kana,
        string expectedJapanese, string expectedKana, string grammarPoint)
    {
        var word = new Word { Japanese = japanese, Kana = kana, English = "counter" };

        Assert.True(GenkiForms.Supports(word, "counterSuffix"));
        Assert.Equal(expectedJapanese, GenkiForms.Render(word, "counterSuffix"));
        Assert.Equal(expectedKana, GenkiForms.Render(word, "counterSuffix", kana: true));
        Assert.Equal([grammarPoint], GenkiForms.RequiredGrammar("counterSuffix", word));
    }

    [Theory]
    [InlineData("（〜を）お願いします", "（〜を）おねがいします", "をお願いします", "をおねがいします")]
    [InlineData("（〜を）ください", "（〜を）ください", "をください", "をください")]
    public void RequestEnding_UnwrapsOnlyKnownCatalogTemplates(string japanese, string kana,
        string expectedJapanese, string expectedKana)
    {
        var word = new Word { Japanese = japanese, Kana = kana, English = "request" };

        Assert.True(GenkiForms.Supports(word, "requestEnding"));
        Assert.Equal(expectedJapanese, GenkiForms.Render(word, "requestEnding"));
        Assert.Equal(expectedKana, GenkiForms.Render(word, "requestEnding", kana: true));
        Assert.Equal(["g02-09"], GenkiForms.RequiredGrammar("requestEnding", word));
    }

    [Fact]
    public void FormsExposeTheirGrammarIntroductionAndRejectUnsupportedForms()
    {
        var verb = new Verb { Japanese = "食べる", Kana = "たべる", English = "eat", Type = "ru" };
        var adjective = new Adjective { Japanese = "高い", Kana = "たかい", English = "high", Type = "i" };
        var noun = new Word { Japanese = "大学", Kana = "だいがく", English = "university" };

        Assert.Equal(["g03-01"], GenkiForms.RequiredGrammar("polite", verb));
        Assert.Equal(["g06-01"], GenkiForms.RequiredGrammar("te", verb));
        Assert.Equal(["g11-01"], GenkiForms.RequiredGrammar("tai", verb));
        Assert.Equal(["g11-01"], GenkiForms.RequiredGrammar("taiPolite", verb));
        Assert.Equal(["g05-03"], GenkiForms.RequiredGrammar("attributive", adjective));
        Assert.False(GenkiForms.Supports(noun, "polite"));
        Assert.Throws<NotSupportedException>(() => GenkiForms.Render(noun, "polite"));
    }
}
