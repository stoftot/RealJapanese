using System.Collections.Frozen;
using DataLoaders.Exstensions;

namespace DataLoaders.Models;

public record Verb : Conjugatabel
{
    private enum VerbType
    {
        U,
        RU,
        IRREGULAR
    }

    #region conjugation

    private const string PresentAffirmativeEnding = "ます";
    private const string PresentNegativeEnding = "ません";
    private const string PastAffirmativeEnding = "ました";
    private const string PastNegativeEnding = "ませんでした";

    private static readonly FrozenDictionary<string, string> uConjugations =
        new Dictionary<string, string>
        {
            { "う", "い" },
            { "く", "き" },
            { "ぐ", "ぎ" },
            { "す", "し" },
            { "つ", "ち" },
            { "ぬ", "に" },
            { "ぶ", "び" },
            { "む", "み" },
            { "る", "り" }
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> ruConjugations =
        new Dictionary<string, string>
        {
            { "る", "" }
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> irregularConjugations =
        new Dictionary<string, string>
        {
            { "する", "し" },
            { "くる", "き" }
        }.ToFrozenDictionary();

    private string StemU(string str)
    {
        var (firstPart, lastChar) = str.LastChar();
        return firstPart + uConjugations[lastChar];
    }

    private string StemRu(string str)
    {
        var (firstPart, lastChar) = str.LastChar();
        return firstPart + ruConjugations[lastChar];
    }

    private string StemIrregular(string str)
    {
        var (firstPart, lastTwoChars) = str.LastTwoChars();
        return firstPart + irregularConjugations[lastTwoChars];
    }

    private string Stem(ToConjugate conjugate)
    {
        var str = conjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };

        if (!Enum.TryParse(Type.ToUpper(), out VerbType verbType))
            throw new ArgumentException($"Unknown verb type: {Type}");

        return verbType switch
        {
            VerbType.U => StemU(str),
            VerbType.RU => StemRu(str),
            VerbType.IRREGULAR => StemIrregular(str)
        };
    }

    public override string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType) =>
        conjugationType switch
        {
            ConjugationType.PresentAffirmative => Stem(toConjugate) + PresentAffirmativeEnding,
            ConjugationType.PresentNegative => Stem(toConjugate) + PresentNegativeEnding,
            ConjugationType.PastAffirmative => Stem(toConjugate) + PastAffirmativeEnding,
            ConjugationType.PastNegative => Stem(toConjugate) + PastNegativeEnding,
        };

    #endregion

    #region Form

    public enum VerbForm
    {
        TE,
        TA
    }

    public string Form(ToConjugate toConjugate, VerbForm form)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };

        if (!Enum.TryParse(Type.ToUpper(), out VerbType verbType))
            throw new ArgumentException($"Unknown verb type: {Type}");

        switch (verbType)
        {
            case VerbType.U:
            {
                var (firstPart, lastChar) = str.LastChar();
                return firstPart + uForm(lastChar, form);
            }
            case VerbType.RU:
            {
                var (firstPart, _) = str.LastChar();
                return firstPart + ruForm(form);
            }
            case VerbType.IRREGULAR:
            {
                var (firstPart, lastTwoChar) = str.LastTwoChars();
                return firstPart + irregularForm(lastTwoChar, form);
            }
            default:
                throw new ArgumentException($"Unknown verb type: {Type}");
        }
    }

    private static string uForm(string kana, VerbForm form) =>
        kana switch
        {
            "う" or "つ" or "る"
                => "っ" + (form.Equals(VerbForm.TE) ? "て" : "な"),
            "む" or "ぶ" or "ぬ"
                => "ん" + (form.Equals(VerbForm.TE) ? "で" : "だ"),
            "く"
                => "い" + (form.Equals(VerbForm.TE) ? "て" : "な"),
            "ぐ"
                => "い" + (form.Equals(VerbForm.TE) ? "で" : "だ"),
            "す"
                => "し" + (form.Equals(VerbForm.TE) ? "て" : "な"),
            _ => throw new ArgumentOutOfRangeException(nameof(kana), kana, null)
        };

    private static string ruForm(VerbForm form) => form == VerbForm.TE ? "て" : "な";

    private static string irregularForm(string kana, VerbForm form) =>
        kana switch
        {
            "する" => "し" + (form.Equals(VerbForm.TE) ? "て" : "な"),
            "くる" => "き" + (form.Equals(VerbForm.TE) ? "て" : "な")
        };

    #endregion
}