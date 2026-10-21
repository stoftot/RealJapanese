using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioStudio;

public sealed record CatalogWord(string Dataset, int Id, string Group, string Japanese, string Kana, string English)
{
    public string Key => $"{Dataset}/{Id}";
    public string SpokenJapanese => WordCatalog.SpokenJapanese(this);
    public string SpokenKana => WordCatalog.SpokenKana(this);
    public string? SpeechNote => Japanese.Contains('〜') || Japanese.Contains('～')
        ? "Placeholder omitted; spoken in isolation. Accent may change in context."
        : Kana == "Tシャツ" ? "Spoken reading: ティーシャツ" : null;
}

public sealed class StudioClip
{
    public required string Key { get; init; }
    public required string Engine { get; init; }
    public string Status { get; set; } = "Pending";
    public string? File { get; set; }
    public string? RawFile { get; set; }
    public string? AudioUrl { get; set; }
    public string? RawUrl { get; set; }
    public double? Seconds { get; set; }
    public string? Transcript { get; set; }
    public string Validation { get; set; } = "Pending";
    public string? Error { get; set; }
    public string? PitchSource { get; set; }
    public string? Sha256 { get; set; }
    public string? TranscriptSource { get; set; }
    public IReadOnlyDictionary<string, string>? TranscriptionDiagnostics { get; set; }
}

public sealed class StudioRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Queued";
    public required List<CatalogWord> Words { get; init; }
    public required string[] Engines { get; init; }
    public required string OutputQuality { get; init; }
    public string? ReferenceId { get; init; }
    public string? ReferenceName { get; init; }
    public ReferenceItem? ReferenceSnapshot { get; init; }
    public GenerateAudioUsingTTS.Settings? SpeechSettings { get; set; }
    public List<StudioClip> Clips { get; init; } = [];
    public Dictionary<string, string> Selections { get; init; } = [];
    public List<string> Notes { get; init; } = [];
    public string Phase { get; set; } = "Waiting";
    public int WordCount => Words.Count;
    public object Progress => new
    {
        Total = Clips.Count,
        Generated = Clips.Count(c => c.File is not null),
        Failed = Clips.Count(c => c.Status == "Failed"),
        ChecksCompleted = Clips.Count(c => c.Validation != "Pending" && c.File is not null),
        Remaining = Clips.Count(c => c.Status is "Pending" or "Generating"),
        Phase
    };
    public bool IsActive => Status is "Queued" or "Generating" or "Checking" or "Pausing";
    public bool CanResume => Status is "Paused" or "Interrupted";
}

public sealed record ReferenceItem(string Id, string Name, string Transcript, string TranscriptSource,
    double Seconds, string SourceName, DateTimeOffset CreatedAt)
{
    public string MediaUrl => $"/api/references/{Id}/audio";
}

public sealed record ExportedClip(CatalogWord Word, string Engine, string RunId, string OutputQuality,
    string File, string Sha256, DateTimeOffset ExportedAt, string? Transcript, string Validation)
{
    public string AudioUrl => $"/api/exports/audio?key={Uri.EscapeDataString(Word.Key)}";
}

public sealed record GenerationInput(string[] WordKeys, string[] Engines, string? ReferenceId, string OutputQuality = "OriginalWav");
public sealed record SelectionInput(Dictionary<string, string?> Choices);
public sealed record ExportInput(bool AcknowledgeUnselected = false);
public sealed record ReferenceEdit(string Name, string Transcript);
public sealed record ExportResult(int ExportedCount, int UnselectedCount);
public sealed class StudioException(string message, int status = 400, int? unselectedCount = null) : Exception(message)
{
    public int Status { get; } = status;
    public int? UnselectedCount { get; } = unselectedCount;
}

public static class StudioJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
}
