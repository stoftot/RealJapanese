using System.Text.Json;
using DataLoaders.Models;

namespace RealJapanese.Tests;

/// <summary>Checks noun predicates, te forms, and preservation of the noun stem.</summary>
public sealed class NounConjugationTests
{
    public static TheoryData<string, string, string[], string[]> PoliteCases => new()
    {
        { "学生", "がくせい", ["学生です", "学生じゃないです", "学生でした", "学生じゃなかったです"], ["がくせいです", "がくせいじゃないです", "がくせいでした", "がくせいじゃなかったです"] },
        { "時計", "とけい", ["時計です", "時計じゃないです", "時計でした", "時計じゃなかったです"], ["とけいです", "とけいじゃないです", "とけいでした", "とけいじゃなかったです"] },
        { "花", "はな", ["花です", "花じゃないです", "花でした", "花じゃなかったです"], ["はなです", "はなじゃないです", "はなでした", "はなじゃなかったです"] },
        { "コーヒー", "コーヒー", ["コーヒーです", "コーヒーじゃないです", "コーヒーでした", "コーヒーじゃなかったです"], ["コーヒーです", "コーヒーじゃないです", "コーヒーでした", "コーヒーじゃなかったです"] }
    };

    public static TheoryData<string, string, string[], string[]> ShortCases => new()
    {
        { "学生", "がくせい", ["学生だ", "学生じゃない", "学生だった", "学生じゃなかった"], ["がくせいだ", "がくせいじゃない", "がくせいだった", "がくせいじゃなかった"] },
        { "時計", "とけい", ["時計だ", "時計じゃない", "時計だった", "時計じゃなかった"], ["とけいだ", "とけいじゃない", "とけいだった", "とけいじゃなかった"] },
        { "花", "はな", ["花だ", "花じゃない", "花だった", "花じゃなかった"], ["はなだ", "はなじゃない", "はなだった", "はなじゃなかった"] },
        { "コーヒー", "コーヒー", ["コーヒーだ", "コーヒーじゃない", "コーヒーだった", "コーヒーじゃなかった"], ["コーヒーだ", "コーヒーじゃない", "コーヒーだった", "コーヒーじゃなかった"] }
    };

    [Theory]
    [MemberData(nameof(PoliteCases))]
    public void Conjugate_MatchesLiteralJapaneseAndKana(string japanese, string kana, string[] japaneseExpected, string[] kanaExpected)
    {
        var noun = CreateNoun(japanese, kana);

        Assert.Equal(japaneseExpected, ConjugateAll(noun, Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(kanaExpected, ConjugateAll(noun, Conjugatabel.ToConjugate.Kana));
    }

    [Theory]
    [MemberData(nameof(ShortCases))]
    public void ShortForm_MatchesLiteralJapaneseAndKana(string japanese, string kana, string[] japaneseExpected, string[] kanaExpected)
    {
        var noun = CreateNoun(japanese, kana);

        Assert.Equal(japaneseExpected, ShortFormAll(noun, Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(kanaExpected, ShortFormAll(noun, Conjugatabel.ToConjugate.Kana));
    }

    [Theory]
    [InlineData("学生", "がくせい", "学生で", "がくせいで")]
    [InlineData("時計", "とけい", "時計で", "とけいで")]
    [InlineData("花", "はな", "花で", "はなで")]
    [InlineData("コーヒー", "コーヒー", "コーヒーで", "コーヒーで")]
    public void TeForm_AppendsDeWithoutChangingTheNoun(string japanese, string kana, string japaneseExpected, string kanaExpected)
    {
        var noun = CreateNoun(japanese, kana);

        Assert.Equal(japaneseExpected, noun.TeForm(Conjugatabel.ToConjugate.Japanese));
        Assert.Equal(kanaExpected, noun.TeForm(Conjugatabel.ToConjugate.Kana));
    }

    [Fact]
    public void ConjugationMethods_RejectUnknownTarget()
    {
        var noun = CreateNoun("学生", "がくせい");
        var target = (Conjugatabel.ToConjugate)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => noun.Conjugate(target, Conjugatabel.ConjugationType.PresentAffirmative));
        Assert.Throws<ArgumentOutOfRangeException>(() => noun.ShortForm(target, Conjugatabel.ConjugationType.PresentAffirmative));
        Assert.Throws<ArgumentOutOfRangeException>(() => noun.TeForm(target));
    }

    [Fact]
    public void ConjugationMethods_RejectUnknownConjugation()
    {
        var noun = CreateNoun("学生", "がくせい");
        var conjugation = (Conjugatabel.ConjugationType)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => noun.Conjugate(Conjugatabel.ToConjugate.Japanese, conjugation));
        Assert.Throws<ArgumentOutOfRangeException>(() => noun.ShortForm(Conjugatabel.ToConjugate.Japanese, conjugation));
    }

    [Fact]
    public void Serialization_RoundTripsLegacyWordShapeWithoutTypeField()
    {
        const string json = "{\"id\":\"17\",\"japanese\":\"学生\",\"kana\":\"がくせい\",\"english\":\"student\",\"category\":\"People\"}";
        var noun = JsonSerializer.Deserialize<Noun>(json);

        Assert.NotNull(noun);
        Assert.Equal(17, noun.Id);
        Assert.Equal("学生", noun.Japanese);
        Assert.Equal("がくせい", noun.Kana);
        Assert.Equal("student", noun.English);
        Assert.Equal("People", noun.Category);

        var serialized = JsonSerializer.Serialize(noun);
        Assert.Equal(noun, JsonSerializer.Deserialize<Noun>(serialized));
        using var document = JsonDocument.Parse(serialized);
        Assert.Equal(new[] { "category", "english", "id", "japanese", "kana" },
            document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    private static string[] ConjugateAll(Noun noun, Conjugatabel.ToConjugate target) =>
    [
        noun.Conjugate(target, Conjugatabel.ConjugationType.PresentAffirmative),
        noun.Conjugate(target, Conjugatabel.ConjugationType.PresentNegative),
        noun.Conjugate(target, Conjugatabel.ConjugationType.PastAffirmative),
        noun.Conjugate(target, Conjugatabel.ConjugationType.PastNegative)
    ];

    private static string[] ShortFormAll(Noun noun, Conjugatabel.ToConjugate target) =>
    [
        noun.ShortForm(target, Conjugatabel.ConjugationType.PresentAffirmative),
        noun.ShortForm(target, Conjugatabel.ConjugationType.PresentNegative),
        noun.ShortForm(target, Conjugatabel.ConjugationType.PastAffirmative),
        noun.ShortForm(target, Conjugatabel.ConjugationType.PastNegative)
    ];

    private static Noun CreateNoun(string japanese, string kana) => new()
    {
        Japanese = japanese,
        Kana = kana,
        English = "test"
    };
}
