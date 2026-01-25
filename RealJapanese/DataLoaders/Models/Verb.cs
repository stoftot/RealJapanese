using System.Collections.Frozen;
using System.Text.Json.Serialization;
using DataLoaders.Exstensions;

namespace DataLoaders.Models;

public record Verb : Word
{
    [JsonPropertyName("verbType")] public required string VerbType { get; set; }

    public enum ToConjugate
    {
        Japanese,
        Kana
    }
    
    public enum ConjugationType
    {
        PresentAffirmative,
        PresentNegative,
        PastAffirmative,
        PastNegative
    }
    
    private const string PresentAffirmativeEnding = "ます";
    private const string PresentNegativeEnding = "ません";
    private const string PastAffirmativeEnding = "ました";
    private const string PastNegativeEnding = "ませんでした";

    private readonly FrozenDictionary<string, string> uConjugations =
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

    private readonly FrozenDictionary<string, string> ruConjugations =
        new Dictionary<string, string>
        {
            { "る", "" }
        }.ToFrozenDictionary();

    private readonly FrozenDictionary<string, string> irregularConjugations =
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
        
        return VerbType switch
        {
            "u" => StemU(str),
            "ru" => StemRu(str),
            "irregular" => StemIrregular(str)
        };
    }
    
    public string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType)=>
        conjugationType switch
        {
            ConjugationType.PresentAffirmative => Stem(toConjugate) + PresentAffirmativeEnding,
            ConjugationType.PresentNegative => Stem(toConjugate) + PresentNegativeEnding,
            ConjugationType.PastAffirmative => Stem(toConjugate) + PastAffirmativeEnding,
            ConjugationType.PastNegative => Stem(toConjugate) + PastNegativeEnding,
        };
}