using System.Text.Json.Serialization;
using DataLoaders.JsonConverters;

namespace DataLoaders.Models;

public record Word
{
    [JsonPropertyName("id")] 
    [JsonConverter(typeof(StringToIntConverter))]
    public int Id { get; set; } = -1;
    
    [JsonPropertyName("japanese")]
    public required string Japanese { get; set; }
    
    [JsonPropertyName("kana")]
    public required string Kana { get; set; }
    
    [JsonPropertyName("english")]
    public required string English { get; set; }

    [JsonPropertyName("category")] 
    public string Category { get; set; } = "";
}