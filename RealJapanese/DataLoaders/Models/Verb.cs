using System.Text.Json.Serialization;

namespace DataLoaders.Models;

public record Verb : Word
{

    [JsonPropertyName("verbType")]
    public required string VerbType { get; set; }
    
    [JsonPropertyName("conjugations")]
    public List<VerbConjugation> Conjugations { get; set; } = [];

    public record VerbConjugation : Word
    {
        [JsonPropertyName("mainType")]
        public required string MainType { get; set; }
        
        [JsonPropertyName("secondaryType")]
        public required string SecondaryType { get; set; }
    }
}