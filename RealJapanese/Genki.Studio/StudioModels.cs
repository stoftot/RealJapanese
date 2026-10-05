using DataLoaders.Models.Genki;
using Genki.Generation;
using Genki.Inference;

namespace Genki.Studio;

public sealed class StudioSettings
{
    public string DataRoot { get; set; } = "";
    public string StateRoot { get; set; } = "";
    public string PublishPath { get; set; } = "";
    public string ModelsDirectory { get; set; } = "";
    public string ServerExecutable { get; set; } = "";
    public string QwenModelId { get; set; } = "qwen2.5-14b-instruct";
    public string TranslationModelId { get; set; } = "lfm2-enjp-translation";
    public string QwenModelFile { get; set; } = "";
    public string TranslationModelFile { get; set; } = "";
    public int QwenContextSize { get; set; } = 16384;
    public int TranslationContextSize { get; set; } = 4096;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; } = 2048;
    public int TimeoutSeconds { get; set; } = 300;
    public int? GpuLayers { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public int TagGroupSize { get; set; } = 8;
    public int AlternativeLimit { get; set; } = 3;
    public int DelayMilliseconds { get; set; }
}

public sealed record StudioRequest
{
    // generate, tag, scan (no models), publish
    public string Kind { get; init; } = "generate";
    // resume reuses checkpoints; retry-failed keeps successful stages; regenerate archives and resets the selected scope
    public string Mode { get; init; } = "resume";
    public string[] SchemaIds { get; init; } = [];
    public WordRef[]? Words { get; init; }
    public string[]? TagIds { get; init; }
    public int? Limit { get; init; } = 5;
    public double? Minutes { get; init; } = 5;
    public bool Exhaustive { get; init; }
    public bool ConfirmRegeneration { get; init; }
    public bool ConfirmPublication { get; init; }
    public string? ExpectedPublishPath { get; init; }
}

public sealed record StudioWord(WordRef Reference, string Japanese, string Kana, string English);
public sealed record SchemaCoverage(string SchemaId, string Scope, string InputsFingerprint, long Enumerated,
    long NotStarted, bool Complete, DateTimeOffset UpdatedAt);
public sealed record StudioSchemaRow(string Id, int Lesson, string LessonTitle, string GrammarId, string GrammarTitle,
    string Register, string Pattern, string CandidateUpperBound, long Recorded, long Completed, long Failed,
    long NeedsReview, long Rejected, long Pending, string QueueStatus, SchemaCoverage? Coverage, bool CoverageStale);
public sealed record StudioJobView(string Id, string Kind, string Mode, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, string[] SchemaIds, string Scope, int? Limit, double? Minutes, bool Exhaustive,
    string Activity, BatchSummary Summary, string? Error, string? ArchiveDirectory);
public sealed record StudioSnapshot(StudioSettings Settings, string ConfigurationFile, bool InferenceAvailable,
    bool Busy, string[] Errors, StudioSchemaRow[] Schemas, StudioWord[] Words, SemanticTag[] Tags,
    StudioJobView[] Jobs, DateTimeOffset? RefreshedAt)
{
    public StudioTagCoverage[] TagCoverage { get; init; } = [];
}
public sealed record StudioTagCoverage(WordRef Word, long Matches, long DoesNotMatch, long Uncertain, long Failed,
    long Pending, long NotEvaluated, bool NeedsReview, string? Reason);
public sealed record StudioQuestion(string Id, string SchemaId, string Status, string? Stage, string? Reason,
    string? English, string? Japanese, string? Kana, string[] Answers);
public sealed record StudioReviewPage(StudioQuestion[] Questions, bool HasMore);

public sealed class StudioJob
{
    public required string Id { get; init; }
    public required StudioRequest Request { get; init; }
    public required GenkiConfiguration Configuration { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "queued";
    public string Activity { get; set; } = "Waiting for the worker";
    public BatchSummary Summary { get; set; } = new();
    public string? Error { get; set; }
    public bool ResetApplied { get; set; }
    public bool RetryFailedOnResume { get; set; }
    public string? ArchiveDirectory { get; set; }
}
