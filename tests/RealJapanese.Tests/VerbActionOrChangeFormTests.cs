using DataLoaders.Models;

namespace RealJapanese.Tests;

/// <summary>Checks that action/change forms conjugate auxiliary いる independently of the original verb type.</summary>
public sealed class VerbActionOrChangeFormTests
{
    public static TheoryData<string, string, string, string[], string[]> Cases => new()
    {
        { "読む", "よむ", "u",
            ["読んでいます", "読んでいません", "読んでいました", "読んでいませんでした"],
            ["よんでいます", "よんでいません", "よんでいました", "よんでいませんでした"] },
        { "食べる", "たべる", "ru",
            ["食べています", "食べていません", "食べていました", "食べていませんでした"],
            ["たべています", "たべていません", "たべていました", "たべていませんでした"] },
        { "する", "する", "irregular",
            ["しています", "していません", "していました", "していませんでした"],
            ["しています", "していません", "していました", "していませんでした"] },
        { "くる", "くる", "irregular",
            ["きています", "きていません", "きていました", "きていませんでした"],
            ["きています", "きていません", "きていました", "きていませんでした"] },
        { "勉強する", "べんきょうする", "irregular",
            ["勉強しています", "勉強していません", "勉強していました", "勉強していませんでした"],
            ["べんきょうしています", "べんきょうしていません", "べんきょうしていました", "べんきょうしていませんでした"] }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AuxiliaryUsesRuConjugationForEveryVerbType(
        string japanese, string kana, string type, string[] japaneseExpected, string[] kanaExpected)
    {
        var verb = new Verb { Japanese = japanese, Kana = kana, Type = type, English = "test" };

        Assert.Equal(japaneseExpected, ConjugateAll(verb, Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(kanaExpected, ConjugateAll(verb, Conjugatabel.ToConjugate.Kana));
    }

    [Theory]
    [InlineData("する", "しています")]
    [InlineData("くる", "きています")]
    public void KanaConjugationDoesNotDependOnJapaneseSpelling(string kana, string expected)
    {
        var verb = new Verb { Japanese = "", Kana = kana, Type = "irregular", English = "test" };

        Assert.Equal(expected, verb.ActionOrChangeForm(
            Conjugatabel.ToConjugate.Kana, Conjugatabel.ConjugationType.PresentAffirmative));
    }

    private static string[] ConjugateAll(Verb verb, Conjugatabel.ToConjugate target) =>
    [
        verb.ActionOrChangeForm(target, Conjugatabel.ConjugationType.PresentAffirmative),
        verb.ActionOrChangeForm(target, Conjugatabel.ConjugationType.PresentNegative),
        verb.ActionOrChangeForm(target, Conjugatabel.ConjugationType.PastAffirmative),
        verb.ActionOrChangeForm(target, Conjugatabel.ConjugationType.PastNegative)
    ];
}
