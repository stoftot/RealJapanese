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

    public string TypeShortForm() => Type == "irregular" ? "ir" : Type;

    public abstract string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType);
}

public static class ConjugationTypeExtensions
{
    public static string ToDisplayString(this Conjugatabel.ConjugationType conjugationType)
    {
        switch (conjugationType)
        {
            case Conjugatabel.ConjugationType.PresentAffirmative:
                return "present affirmative";
            case Conjugatabel.ConjugationType.PresentNegative:
                return "present negative";
            case Conjugatabel.ConjugationType.PastAffirmative:
                return "past affirmative";
            case Conjugatabel.ConjugationType.PastNegative:
                return "past negative";
            default:
                throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null);
        }
    }
}