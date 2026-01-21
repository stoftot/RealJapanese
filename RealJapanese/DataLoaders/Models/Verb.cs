using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using DataLoaders.Exstensions;

namespace DataLoaders.Models;

public record Verb : Word
{
    [JsonPropertyName("verbType")] public required string VerbType { get; set; }

    public enum Conjugate
    {
        Japanese,
        Kana
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

    private string Stem(Conjugate conjugate)
    {
        var str = conjugate switch
        {
            Conjugate.Japanese => Japanese,
            Conjugate.Kana => Kana
        };
        
        return VerbType switch
        {
            "u" => StemU(str),
            "ru" => StemRu(str),
            "irregular" => StemIrregular(str)
        };
    }

    public string PresentAffirmative(Conjugate conjugate) => Stem(conjugate) + PresentAffirmativeEnding;
    public string PresentNegative(Conjugate conjugate) => Stem(conjugate) + PresentNegativeEnding;
    public string PastAffirmative(Conjugate conjugate) => Stem(conjugate) + PastAffirmativeEnding;
    public string PastNegative(Conjugate conjugate) => Stem(conjugate) + PastNegativeEnding;
}