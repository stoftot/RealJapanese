namespace DataLoaders.Models;

/// <summary>Calendar-day learning credit, with support for unfinished retries from older saves.</summary>
public sealed record RehearsalProgress
{
    public int CorrectDays { get; init; }
    public DateOnly? LastCorrectDay { get; init; }
    public DateOnly? LastResetDay { get; init; }
    // Kept for existing progress/sync files; new misses move to Rehearsing immediately.
    public bool AwaitingRetry { get; init; }
}
