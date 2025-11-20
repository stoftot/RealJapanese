using System.Text.Json.Serialization;

namespace DataLoaders.Models;

public record Word
{
    [JsonPropertyName("japanese")]
    public required string Japanese { get; set; }
    
    [JsonPropertyName("kana")]
    public required string Kana { get; set; }
    
    [JsonPropertyName("english")]
    public required string English { get; set; }
}