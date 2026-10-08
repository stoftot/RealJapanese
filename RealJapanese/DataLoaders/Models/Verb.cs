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

    private VerbType CategoryAsVerbType()
    {
        if (!Enum.TryParse(Type.ToUpper(), out VerbType verbType))
            throw new ArgumentException($"Unknown verb type: {Type}");
        return verbType;
    }
    
    public static readonly IReadOnlyList<Verb> possibleEndings = new List<Verb>()
    {
        new (){ Japanese = "う", Kana = "う", Type = "u", English = "NAN" },
        new (){ Japanese = "る", Kana = "る", Type = "ru", English = "NAN" },
        new (){ Japanese = "する", Kana = "する", Type = "irregular", English = "NAN" },
        new (){ Japanese = "くる", Kana = "くる", Type = "irregular", English = "NAN" }
    }.AsReadOnly();
    
    public static readonly IReadOnlyList<Verb> allPossibleEndings = new List<Verb>()
    {
        new (){ Japanese = "う", Kana = "う", Type = "u", English = "NAN" },
        new (){ Japanese = "く", Kana = "く", Type = "u", English = "NAN" },
        new (){ Japanese = "ぐ", Kana = "ぐ", Type = "u", English = "NAN" },
        new (){ Japanese = "す", Kana = "す", Type = "u", English = "NAN" },
        new (){ Japanese = "つ", Kana = "つ", Type = "u", English = "NAN" },
        new (){ Japanese = "ぬ", Kana = "ぬ", Type = "u", English = "NAN" },
        new (){ Japanese = "ぶ", Kana = "ぶ", Type = "u", English = "NAN" },
        new (){ Japanese = "む", Kana = "む", Type = "u", English = "NAN" },
        new (){ Japanese = "る", Kana = "る", Type = "u", English = "NAN" },
        new (){ Japanese = "る", Kana = "る", Type = "ru", English = "NAN" },
        new (){ Japanese = "する", Kana = "する", Type = "irregular", English = "NAN" },
        new (){ Japanese = "くる", Kana = "くる", Type = "irregular", English = "NAN" }
    }.AsReadOnly();

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

    private string Stem(string conjugate)
    {
        return CategoryAsVerbType() switch
        {
            VerbType.U => StemU(conjugate),
            VerbType.RU => StemRu(conjugate),
            VerbType.IRREGULAR => StemIrregular(conjugate)
        };
    }
    
    private string Conjugate(string toConjugate, ConjugationType conjugationType)
    {
        return conjugationType switch
        {
            ConjugationType.PresentAffirmative => Stem(toConjugate) + PresentAffirmativeEnding,
            ConjugationType.PresentNegative => Stem(toConjugate) + PresentNegativeEnding,
            ConjugationType.PastAffirmative => Stem(toConjugate) + PastAffirmativeEnding,
            ConjugationType.PastNegative => Stem(toConjugate) + PastNegativeEnding,
        };
    }

    public override string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };
        return Conjugate(str, conjugationType);
    }

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

        switch (CategoryAsVerbType())
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

    #region Action/change form

    private static readonly Verb actionOrChangeAuxiliary = new()
    {
        Japanese = "いる", Kana = "いる", English = "to be", Type = "ru"
    };

    public string ActionOrChangeForm(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        var teForm = Form(toConjugate, VerbForm.TE);
        return teForm + actionOrChangeAuxiliary.Conjugate(ToConjugate.Kana, conjugationType);
    }

    #endregion

    #region Short forms
    
    private const string ShortPresentNegativeEnding = "ない";
    private const string ShortPastNegativeEnding = "なかった";
    
    private static readonly FrozenDictionary<string, string> ShortUConjugations =
        new Dictionary<string, string>
        {
            { "う", "わ" },
            { "く", "か" },
            { "ぐ", "が" },
            { "す", "さ" },
            { "つ", "た" },
            { "ぬ", "な" },
            { "ぶ", "ば" },
            { "む", "ま" },
            { "る", "ら" }
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> ShortRuConjugations =
        new Dictionary<string, string>
        {
            { "る", "" }
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> ShortIrregularConjugations =
        new Dictionary<string, string>
        {
            { "する", "し" },
            { "くる", "こ" }
        }.ToFrozenDictionary();
    
    private string ShortStemU(string str)
    {
        var (firstPart, lastChar) = str.LastChar();
        return firstPart + ShortUConjugations[lastChar];
    }

    private string ShortStemRu(string str)
    {
        var (firstPart, lastChar) = str.LastChar();
        return firstPart + ShortRuConjugations[lastChar];
    }

    private string ShortStemIrregular(string str)
    {
        var (firstPart, lastTwoChars) = str.LastTwoChars();
        return firstPart + ShortIrregularConjugations[lastTwoChars];
    }
    
    private string ShortStem(string conjugate)
    {
        return CategoryAsVerbType() switch
        {
            VerbType.U => ShortStemU(conjugate),
            VerbType.RU => ShortStemRu(conjugate),
            VerbType.IRREGULAR => ShortStemIrregular(conjugate)
        };
    }

    private string ShortFormPastAffirmative(ToConjugate toConjugate)
    {
        var teForm = Form(toConjugate, VerbForm.TE);
        var(firstPart, lastChar) = teForm.LastChar();
        return lastChar.Equals("て") ? firstPart + "た" : firstPart + "だ";
    }

    public string ShortForm(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };
        
        return conjugationType switch
        {
            ConjugationType.PresentAffirmative => Stem(str),
            ConjugationType.PresentNegative => (str == "ある" ? "" : ShortStem(str)) + ShortPresentNegativeEnding,
            ConjugationType.PastAffirmative => ShortFormPastAffirmative(toConjugate),
            ConjugationType.PastNegative => ShortStem(str) + ShortPastNegativeEnding,
        };
    }
    #endregion
}