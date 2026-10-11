namespace DataLoaders.Models;

/// <summary>Calendar-day learning credit and a known word's unfinished retry.</summary>
public sealed record RehearsalProgress
{
    public int CorrectDays { get; init; }
    public DateOnly? LastCorrectDay { get; init; }
    public DateOnly? LastResetDay { get; init; }
    public bool AwaitingRetry { get; init; }
}
