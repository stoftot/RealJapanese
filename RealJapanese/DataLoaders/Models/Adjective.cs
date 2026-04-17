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
    
    private string StemI(string str) => str[..^1];

    private string StemIrregular(string str, ConjugationType conjugationType) =>
        conjugationType == ConjugationType.PresentAffirmative ? str : str[..^2] + "よ";

    private string Stem(ToConjugate conjugate, ConjugationType conjugationType)
    {
        var str = conjugate switch
        {
            ToConjugate.Japanese => Japanese,
            ToConjugate.Kana => Kana
        };

        if (!Enum.TryParse(Type.ToUpper(), out AdjectiveType adjectiveType))
            throw new ArgumentException($"Unknown adjective type: {Type}");

        return adjectiveType switch
        {
            AdjectiveType.I => StemI(str),
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
}