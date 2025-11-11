using System.Text.Json.Serialization;

namespace DataLoaders.Models;

public class Sentence
{
    [JsonPropertyName("japanese")]
    public required string Japanese { get; set; }
    
    [JsonPropertyName("kanji")]
    public required string Kanji { get; set; }
    
    [JsonPropertyName("romaji")]
    public required string Romaji { get; set; }
    
    [JsonPropertyName("english")]
    public required string English { get; set; }
    
    [JsonPropertyName("category")]
    public required string Category { get; set; }
    
    [JsonPropertyName("level")]
    public required string Level { get; set; }
}