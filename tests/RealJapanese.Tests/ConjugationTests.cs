using DataLoaders.Models;

namespace RealJapanese.Tests;

/// <summary>Checks literal polite conjugations and verb forms for each supported ending family.</summary>
public sealed class VerbConjugationTests
{
    public static TheoryData<string, string, string, string[], string[]> PoliteCases => new()
    {
        { "買う", "かう", "u", ["買います", "買いません", "買いました", "買いませんでした"], ["かいます", "かいません", "かいました", "かいませんでした"] },
        { "書く", "かく", "u", ["書きます", "書きません", "書きました", "書きませんでした"], ["かきます", "かきません", "かきました", "かきませんでした"] },
        { "泳ぐ", "およぐ", "u", ["泳ぎます", "泳ぎません", "泳ぎました", "泳ぎませんでした"], ["およぎます", "およぎません", "およぎました", "およぎませんでした"] },
        { "話す", "はなす", "u", ["話します", "話しません", "話しました", "話しませんでした"], ["はなします", "はなしません", "はなしました", "はなしませんでした"] },
        { "待つ", "まつ", "u", ["待ちます", "待ちません", "待ちました", "待ちませんでした"], ["まちます", "まちません", "まちました", "まちませんでした"] },
        { "死ぬ", "しぬ", "u", ["死にます", "死にません", "死にました", "死にませんでした"], ["しにます", "しにません", "しにました", "しにませんでした"] },
        { "遊ぶ", "あそぶ", "u", ["遊びます", "遊びません", "遊びました", "遊びませんでした"], ["あそびます", "あそびません", "あそびました", "あそびませんでした"] },
        { "読む", "よむ", "u", ["読みます", "読みません", "読みました", "読みませんでした"], ["よみます", "よみません", "よみました", "よみませんでした"] },
        { "帰る", "かえる", "u", ["帰ります", "帰りません", "帰りました", "帰りませんでした"], ["かえります", "かえりません", "かえりました", "かえりませんでした"] },
        { "食べる", "たべる", "ru", ["食べます", "食べません", "食べました", "食べませんでした"], ["たべます", "たべません", "たべました", "たべませんでした"] },
        { "する", "する", "irregular", ["します", "しません", "しました", "しませんでした"], ["します", "しません", "しました", "しませんでした"] },
        { "来る", "くる", "irregular", ["来ます", "来ません", "来ました", "来ませんでした"], ["きます", "きません", "きました", "きませんでした"] },
        { "くる", "くる", "irregular", ["きます", "きません", "きました", "きませんでした"], ["きます", "きません", "きました", "きませんでした"] }
    };

    public static TheoryData<string, string, string, string, string> TeCases => new()
    {
        { "買う", "かう", "u", "買って", "かって" },
        { "書く", "かく", "u", "書いて", "かいて" },
        { "泳ぐ", "およぐ", "u", "泳いで", "およいで" },
        { "話す", "はなす", "u", "話して", "はなして" },
        { "待つ", "まつ", "u", "待って", "まって" },
        { "死ぬ", "しぬ", "u", "死んで", "しんで" },
        { "遊ぶ", "あそぶ", "u", "遊んで", "あそんで" },
        { "読む", "よむ", "u", "読んで", "よんで" },
        { "帰る", "かえる", "u", "帰って", "かえって" },
        { "食べる", "たべる", "ru", "食べて", "たべて" },
        { "する", "する", "irregular", "して", "して" },
        { "来る", "くる", "irregular", "来て", "きて" },
        { "くる", "くる", "irregular", "きて", "きて" }
    };

    [Theory]
    [MemberData(nameof(PoliteCases))]
    public void PoliteConjugations_MatchLiteralJapaneseAndKana(
        string japanese,
        string kana,
        string type,
        string[] japaneseExpected,
        string[] kanaExpected)
    {
        var verb = CreateVerb(japanese, kana, type);

        Assert.Equal(japaneseExpected, ConjugateAll(verb, Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(kanaExpected, ConjugateAll(verb, Conjugatabel.ToConjugate.Kana));
    }

    [Theory]
    [MemberData(nameof(TeCases))]
    public void TeForm_MatchesEachRegularAndIrregularEnding(
        string japanese,
        string kana,
        string type,
        string japaneseExpected,
        string kanaExpected)
    {
        var verb = CreateVerb(japanese, kana, type);

        Assert.Equal(japaneseExpected, verb.Form(Conjugatabel.ToConjugate.Japanese, Verb.VerbForm.TE));
        Assert.Equal(kanaExpected, verb.Form(Conjugatabel.ToConjugate.Kana, Verb.VerbForm.TE));
    }

    [Fact]
    public void TaForm_RuVerbUsesTaEnding()
    {
        var verb = CreateVerb("食べる", "たべる", "ru");

        Assert.Equal("食べた", verb.Form(Conjugatabel.ToConjugate.Japanese, Verb.VerbForm.TA));
    }

    [Fact]
    public void TeForm_IkuUsesExceptionalTteEnding()
    {
        var verb = CreateVerb("行く", "いく", "u");

        Assert.Equal("行って", verb.Form(Conjugatabel.ToConjugate.Japanese, Verb.VerbForm.TE));
    }

    [Theory]
    [InlineData("買う", "かう", "u", "買った", "かった")]
    [InlineData("待つ", "まつ", "u", "待った", "まった")]
    [InlineData("帰る", "かえる", "u", "帰った", "かえった")]
    [InlineData("読む", "よむ", "u", "読んだ", "よんだ")]
    [InlineData("泳ぐ", "およぐ", "u", "泳いだ", "およいだ")]
    [InlineData("話す", "はなす", "u", "話した", "はなした")]
    [InlineData("食べる", "たべる", "ru", "食べた", "たべた")]
    [InlineData("する", "する", "irregular", "した", "した")]
    [InlineData("来る", "くる", "irregular", "来た", "きた")]
    [InlineData("行く", "いく", "u", "行った", "いった")]
    public void TaForm_MatchesIndependentLiteralJapaneseAndKana(
        string japanese, string kana, string type, string expected, string kanaExpected)
    {
        var verb = CreateVerb(japanese, kana, type);

        Assert.Equal(expected, verb.Form(Conjugatabel.ToConjugate.Japanese, Verb.VerbForm.TA));
        Assert.Equal(kanaExpected, verb.Form(Conjugatabel.ToConjugate.Kana, Verb.VerbForm.TA));
    }

    [Theory]
    [InlineData("買う", "かう", "u")]
    [InlineData("食べる", "たべる", "ru")]
    [InlineData("勉強する", "べんきょうする", "irregular")]
    [InlineData("来る", "くる", "irregular")]
    public void ShortPresentAffirmative_RetainsDictionaryForm(string japanese, string kana, string type)
    {
        var verb = CreateVerb(japanese, kana, type);

        Assert.Equal(japanese, verb.ShortForm(Conjugatabel.ToConjugate.Japanese,
            Conjugatabel.ConjugationType.PresentAffirmative));
        Assert.Equal(kana, verb.ShortForm(Conjugatabel.ToConjugate.Kana,
            Conjugatabel.ConjugationType.PresentAffirmative));
    }

    [Theory]
    [InlineData("ある", "ある", "ない", "なかった")]
    [InlineData("来る", "くる", "来ない", "来なかった")]
    public void ShortNegativeForms_HandleExistenceAndKanjiKuru(string japanese, string kana,
        string expectedNegative, string expectedPastNegative)
    {
        var verb = CreateVerb(japanese, kana, japanese == "ある" ? "u" : "irregular");

        Assert.Equal(expectedNegative, verb.ShortForm(Conjugatabel.ToConjugate.Japanese,
            Conjugatabel.ConjugationType.PresentNegative));
        Assert.Equal(expectedPastNegative, verb.ShortForm(Conjugatabel.ToConjugate.Japanese,
            Conjugatabel.ConjugationType.PastNegative));
    }

    private static string[] ConjugateAll(Verb verb, Conjugatabel.ToConjugate target) =>
    [
        verb.Conjugate(target, Conjugatabel.ConjugationType.PresentAffirmative),
        verb.Conjugate(target, Conjugatabel.ConjugationType.PresentNegative),
        verb.Conjugate(target, Conjugatabel.ConjugationType.PastAffirmative),
        verb.Conjugate(target, Conjugatabel.ConjugationType.PastNegative)
    ];

    private static Verb CreateVerb(string japanese, string kana, string type) => new()
    {
        Japanese = japanese,
        Kana = kana,
        English = "test",
        Type = type
    };
}

/// <summary>Checks i, na and irregular adjective conjugations against literal language expectations.</summary>
public sealed class AdjectiveConjugationTests
{
    public static TheoryData<string, string, string, Conjugatabel.ConjugationType, string, string> ValidCases => new()
    {
        { "高い", "たかい", "i", Conjugatabel.ConjugationType.PresentNegative, "高くないです", "たかくないです" },
        { "高い", "たかい", "i", Conjugatabel.ConjugationType.PastAffirmative, "高かったです", "たかかったです" },
        { "高い", "たかい", "i", Conjugatabel.ConjugationType.PastNegative, "高くなかったです", "たかくなかったです" },
        { "静か", "しずか", "na", Conjugatabel.ConjugationType.PresentAffirmative, "静かです", "しずかです" },
        { "静か", "しずか", "na", Conjugatabel.ConjugationType.PresentNegative, "静かじゃないです", "しずかじゃないです" },
        { "静か", "しずか", "na", Conjugatabel.ConjugationType.PastAffirmative, "静かでした", "しずかでした" },
        { "静か", "しずか", "na", Conjugatabel.ConjugationType.PastNegative, "静かじゃなかったです", "しずかじゃなかったです" },
        { "いい", "いい", "irregular", Conjugatabel.ConjugationType.PresentAffirmative, "いいです", "いいです" },
        { "いい", "いい", "irregular", Conjugatabel.ConjugationType.PresentNegative, "よくないです", "よくないです" },
        { "いい", "いい", "irregular", Conjugatabel.ConjugationType.PastAffirmative, "よかったです", "よかったです" },
        { "いい", "いい", "irregular", Conjugatabel.ConjugationType.PastNegative, "よくなかったです", "よくなかったです" }
    };

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void Conjugate_MatchesLiteralJapaneseAndKana(
        string japanese,
        string kana,
        string type,
        Conjugatabel.ConjugationType conjugation,
        string japaneseExpected,
        string kanaExpected)
    {
        var adjective = new Adjective
        {
            Japanese = japanese,
            Kana = kana,
            English = "test",
            Type = type
        };

        Assert.Equal(japaneseExpected, adjective.Conjugate(Conjugatabel.ToConjugate.Japanese, conjugation));
        Assert.Equal(kanaExpected, adjective.Conjugate(Conjugatabel.ToConjugate.Kana, conjugation));
    }

    [Fact]
    public void PresentAffirmative_IAdjectiveRetainsFinalI()
    {
        var adjective = new Adjective
        {
            Japanese = "高い",
            Kana = "たかい",
            English = "high",
            Type = "i"
        };

        Assert.Equal("高いです",
            adjective.Conjugate(Conjugatabel.ToConjugate.Japanese,
                Conjugatabel.ConjugationType.PresentAffirmative));
    }

    [Theory]
    [InlineData("高い", "たかい", "i", "高い", "たかい")]
    [InlineData("静か", "しずか", "na", "静かだ", "しずかだ")]
    [InlineData("いい", "いい", "irregular", "いい", "いい")]
    public void ShortPresentAffirmative_UsesDictionaryFormForIAndIrregularAndDaForNa(string japanese, string kana, string type,
        string expectedJapanese, string expectedKana)
    {
        var adjective = new Adjective { Japanese = japanese, Kana = kana, English = "test", Type = type };

        Assert.Equal(expectedJapanese, adjective.ShortForm(Conjugatabel.ToConjugate.Japanese,
            Conjugatabel.ConjugationType.PresentAffirmative));
        Assert.Equal(expectedKana, adjective.ShortForm(Conjugatabel.ToConjugate.Kana,
            Conjugatabel.ConjugationType.PresentAffirmative));
    }

    [Theory]
    [InlineData("高い", "たかい", "i", "高くて", "たかくて")]
    [InlineData("静か", "しずか", "na", "静かで", "しずかで")]
    [InlineData("いい", "いい", "irregular", "よくて", "よくて")]
    public void TeForm_MatchesIndependentLiteralJapaneseAndKana(string japanese, string kana, string type,
        string expectedJapanese, string expectedKana)
    {
        var adjective = new Adjective { Japanese = japanese, Kana = kana, English = "test", Type = type };

        Assert.Equal(expectedJapanese, adjective.TeForm(Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(expectedKana, adjective.TeForm(Conjugatabel.ToConjugate.Kana));
    }
}
