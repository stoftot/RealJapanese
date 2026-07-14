using System.Text.Json.Serialization;
using DataLoaders.JsonConverters;

namespace DataLoaders.Models;

public record Kanji
{
    [JsonPropertyName("id")] 
    [JsonConverter(typeof(StringToIntConverter))]
    public int Id { get; set; } = -1;

    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }
    
    [JsonPropertyName("onReading")]
    public required string OnReading { get; init; }

    [JsonPropertyName("kunReading")]
    public required string KunReading { get; init; }
    
    [JsonPropertyName("commonEnglish")]
    public required string CommonEnglish { get; init; }
    
    [JsonPropertyName("otherEnglish")]
    public required List<string> OtherEnglish { get; init; } = [];
}