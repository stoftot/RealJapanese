// ReSharper disable InconsistentNaming

using DataLoaders.Exstensions;

namespace DataLoaders.Models;

public record Adjective : Conjugatabel
{
    private enum AdjectiveType
    {
        I,
        NA,
        IRREGULAR
    }

    private AdjectiveType CateGoryAsAdjectiveType()
    {
        if (!Enum.TryParse(Type.ToUpper(), out AdjectiveType adjectiveType))
            throw new ArgumentException($"Unknown adjective type: {Type}");
        return adjectiveType;
    }

    #region conjugation
    private const string I_PresentAffirmativeEnding = "です";
    private const string I_PresentNegativeEnding = "くないです";
    private const string I_PastAffirmativeEnding = "かったです";
    private const string I_PastNegativeEnding = "くなかったです";

    private const string NA_PresentAffirmativeEnding = "です";
    private const string NA_PresentNegativeEnding = "じゃないです";
    private const string NA_PastAffirmativeEnding = "でした";
    private const string NA_PastNegativeEnding = "じゃなかったです";

    public static readonly IReadOnlyList<Adjective> possibleEndings = new List<Adjective>()
    {
        new (){ Japanese = "い", Kana = "い", Type = "i", English = "NAN" },
        new (){ Japanese = "な", Kana = "な", Type = "na", English = "NAN" },
        new (){ Japanese = "いい", Kana = "いい", Type = "irregular", English = "NAN" }
    }.AsReadOnly();
    
    private string StemI(string str) => str.EndsWith("い", StringComparison.Ordinal)
        ? str[..^1]
        : throw new ArgumentException($"An i-adjective must end in い: {str}", nameof(str));

    private string StemIrregular(string str, ConjugationType conjugationType) =>
        conjugationType == ConjugationType.PresentAffirmative ? str : str[..^2] + "よ";

    private string Stem(ToConjugate conjugate, ConjugationType conjugationType)
    {
        var str = conjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };

        return CateGoryAsAdjectiveType() switch
        {
            AdjectiveType.I => conjugationType == ConjugationType.PresentAffirmative ? str : StemI(str),
            AdjectiveType.NA => str,
            AdjectiveType.IRREGULAR => StemIrregular(str, conjugationType),
        };
    }

    public override string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        if (!Enum.TryParse(Type.ToUpper(), out AdjectiveType adjectiveType))
            throw new ArgumentException($"Unknown adjective type: {Type}");

        var stem = Stem(toConjugate, conjugationType);

        var ending = adjectiveType switch
        {
            AdjectiveType.I or AdjectiveType.IRREGULAR => conjugationType switch
            {
                ConjugationType.PresentAffirmative => I_PresentAffirmativeEnding,
                ConjugationType.PresentNegative => I_PresentNegativeEnding,
                ConjugationType.PastAffirmative => I_PastAffirmativeEnding,
                ConjugationType.PastNegative => I_PastNegativeEnding,
                _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
            },

            AdjectiveType.NA => conjugationType switch
            {
                ConjugationType.PresentAffirmative => NA_PresentAffirmativeEnding,
                ConjugationType.PresentNegative => NA_PresentNegativeEnding,
                ConjugationType.PastAffirmative => NA_PastAffirmativeEnding,
                ConjugationType.PastNegative => NA_PastNegativeEnding,
                _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
        };

        return string.Concat(stem, ending);
    }
    #endregion

    #region TeForm

    public string TeForm(ToConjugate toConjugate)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };

        return CateGoryAsAdjectiveType() switch
        {
            AdjectiveType.I => StemI(str) + "くて",
            AdjectiveType.NA => str.EndsWith("な", StringComparison.Ordinal) ? str[..^1] + "で" : str + "で",
            AdjectiveType.IRREGULAR => IrregularTeForm(str),
        };
    }

    private static string IrregularTeForm(string str) =>
        str.EndsWith("いい", StringComparison.Ordinal) ? str[..^2] + "よくて" : throw new ArgumentException(
            $"Unsupported irregular adjective: {str}", nameof(str));

    /// <summary>Returns the form used directly before a noun.</summary>
    public string AttributiveForm(ToConjugate toConjugate)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana,
            _ => throw new ArgumentOutOfRangeException(nameof(toConjugate), toConjugate, null)
        };
        return CateGoryAsAdjectiveType() == AdjectiveType.NA ? RemoveTrailingNa(str) + "な" : str;
    }

    /// <summary>Returns the adverbial form used to modify an action or description.</summary>
    public string AdverbialForm(ToConjugate toConjugate)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana,
            _ => throw new ArgumentOutOfRangeException(nameof(toConjugate), toConjugate, null)
        };
        return CateGoryAsAdjectiveType() == AdjectiveType.NA
            ? RemoveTrailingNa(str) + "に"
            : AdjectiveStem(str) + "く";
    }

    /// <summary>Returns the adjective stem used with constructions such as すぎる.</summary>
    public string AdjectiveStem(ToConjugate toConjugate)
    {
        var str = toConjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana,
            _ => throw new ArgumentOutOfRangeException(nameof(toConjugate), toConjugate, null)
        };
        return AdjectiveStem(str);
    }

    private string AdjectiveStem(string str) => CateGoryAsAdjectiveType() switch
    {
        AdjectiveType.I => StemI(str),
        AdjectiveType.NA => RemoveTrailingNa(str),
        AdjectiveType.IRREGULAR when str.EndsWith("いい", StringComparison.Ordinal) => str[..^2] + "よ",
        _ => throw new ArgumentException($"Unsupported adjective spelling: {str}", nameof(str))
    };

    private static string RemoveTrailingNa(string str) =>
        str.EndsWith("な", StringComparison.Ordinal) ? str[..^1] : str;

    #endregion

    #region Short forms
    private const string Short_I_PresentNegativeEnding = "くない";
    private const string Short_I_PastAffirmativeEnding = "かった";
    private const string Short_I_PastNegativeEnding = "くなかった";

    private const string Short_NA_PresentAffirmativeEnding = "だ";
    private const string Short_NA_PresentNegativeEnding = "じゃない";
    private const string Short_NA_PastAffirmativeEnding = "だった";
    private const string Short_NA_PastNegativeEnding = "じゃなかった";
    
    public string ShortForm(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        var adjectiveType = CateGoryAsAdjectiveType();

        var stem = Stem(toConjugate, conjugationType);

        var ending = adjectiveType switch
        {
            AdjectiveType.I or AdjectiveType.IRREGULAR => conjugationType switch
            {
                ConjugationType.PresentAffirmative => "",
                ConjugationType.PresentNegative => Short_I_PresentNegativeEnding,
                ConjugationType.PastAffirmative => Short_I_PastAffirmativeEnding,
                ConjugationType.PastNegative => Short_I_PastNegativeEnding,
                _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
            },

            AdjectiveType.NA => conjugationType switch
            {
                ConjugationType.PresentAffirmative => Short_NA_PresentAffirmativeEnding,
                ConjugationType.PresentNegative => Short_NA_PresentNegativeEnding,
                ConjugationType.PastAffirmative => Short_NA_PastAffirmativeEnding,
                ConjugationType.PastNegative => Short_NA_PastNegativeEnding,
                _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
        };

        return string.Concat(stem, ending);
    }
    
    #endregion
}
