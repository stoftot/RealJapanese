using System.Text.Json.Serialization;
using DataLoaders.JsonConverters;

namespace DataLoaders.Models;

public record Word
{
    [JsonPropertyName("id")] 
    [JsonConverter(typeof(StringToIntConverter))]
    public int Id { get; set; } = -1;
    
    [JsonPropertyName("japanese")]
    public required string Japanese { get; init; }
    
    [JsonPropertyName("kana")]
    public required string Kana { get; init; }
    
    [JsonPropertyName("english")]
    public required string English { get; init; }

    [JsonPropertyName("category")] 
    public string Category { get; init; } = "";
}