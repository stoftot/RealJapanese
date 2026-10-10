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

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = "self added";

    // A repeated lesson entry shares one identity and one study status.
    [JsonPropertyName("additionalOrigins")]
    public List<string> AdditionalOrigins { get; init; } = [];

    public virtual bool Equals(Word? other) => other is not null && EqualityContract == other.EqualityContract &&
        Id == other.Id && Japanese == other.Japanese && Kana == other.Kana && English == other.English &&
        Category == other.Category && Origin == other.Origin &&
        (AdditionalOrigins ?? []).SequenceEqual(other.AdditionalOrigins ?? []);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EqualityContract);
        hash.Add(Id);
        hash.Add(Japanese);
        hash.Add(Kana);
        hash.Add(English);
        hash.Add(Category);
        hash.Add(Origin);
        foreach (var origin in AdditionalOrigins ?? []) hash.Add(origin);
        return hash.ToHashCode();
    }
}
