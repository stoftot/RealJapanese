using System.Text.Json.Serialization;

namespace DataLoaders.Models;

public abstract record Conjugatabel : Word
{
    [JsonPropertyName("type")] public required string Type { get; init; }

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

    public string TypeShortForm() => Type == "irregular" ? "irreg" : Type;

    public abstract string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType);
}