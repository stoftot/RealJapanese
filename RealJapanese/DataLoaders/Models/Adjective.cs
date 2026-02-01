using System.Text.Json.Serialization;

namespace DataLoaders.Models;

public record Adjective : Word
{
    [JsonPropertyName("type")] public required string Type { get; set; }
}