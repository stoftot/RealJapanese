using System.Text.Json.Serialization;

namespace DataLoaders.Models;

public class ParticalsModel
{
    [JsonPropertyName("japanese")]
    public required string Japanese { get; set; }
    
    [JsonPropertyName("english")]
    public required string English { get; set; }

    [JsonPropertyName("missing_particles")]
    public required string[] MissingParticles { get; set; }
}