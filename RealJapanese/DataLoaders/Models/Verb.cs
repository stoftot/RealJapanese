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

    [JsonPropertyName("conjugations")] public List<VerbConjugation> Conjugations { get; set; } = [];

    public record VerbConjugation : Word
    {
        [JsonPropertyName("mainType")] public required string MainType { get; set; }

        [JsonPropertyName("secondaryType")] public required string SecondaryType { get; set; }
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
    
    private string StemU()
    {
        var (firstPart, lastChar) = Japanese.LastChar();
        return firstPart + uConjugations[lastChar];
    }

    private string StemRu()
    {
        var (firstPart, lastChar) = Japanese.LastChar();
        return firstPart + ruConjugations[lastChar];
    }

    private string StemIrregular()
    {
        var (firstPart, lastTwoChars) = Japanese.LastTwoChars();
        return firstPart + irregularConjugations[lastTwoChars];
    }

    private string Stem()
    {
        return VerbType switch
        {
            "u" => StemU(),
            "ru" => StemRu(),
            "irregular" => StemIrregular()
        };
    }

    public string PresentAffirmative() => Stem() + PresentAffirmativeEnding;
    public string PresentNegative() => Stem() + PresentNegativeEnding;
    public string PastAffirmative() => Stem() + PastAffirmativeEnding;
    public string PastNegative() => Stem() + PastNegativeEnding;
}