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
        
        if(!Enum.TryParse(Type.ToUpper(), out VerbType verbType))
            throw new ArgumentException($"Unknown verb type: {Type}");
        
        return verbType switch
        {
            VerbType.U => StemU(str),
            VerbType.RU => StemRu(str), 
            VerbType.IRREGULAR => StemIrregular(str)
        };
    }
    
    public override string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType)=>
        conjugationType switch
        {
            ConjugationType.PresentAffirmative => Stem(toConjugate) + PresentAffirmativeEnding,
            ConjugationType.PresentNegative => Stem(toConjugate) + PresentNegativeEnding,
            ConjugationType.PastAffirmative => Stem(toConjugate) + PastAffirmativeEnding,
            ConjugationType.PastNegative => Stem(toConjugate) + PastNegativeEnding,
        };
}