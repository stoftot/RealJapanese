namespace AudioStudio;

public sealed record GeneratedAudio(string File, string RawFile, double Seconds, string Sha256, string PitchSource);
public sealed record CheckedAudio(string? Text, string? Error, string Source, IReadOnlyDictionary<string, string>? Diagnostics = null);

// The scheduler owns checkpoints; model adapters own one request at a time.
public interface IGenerationBackend
{
    void Prepare(StudioRun run);
    IVoiceBatch OpenVoice(StudioRun run, string engine);
    ICheckBatch OpenChecks(StudioRun run);
}
public interface IVoiceBatch : IAsyncDisposable
{
    Task<GeneratedAudio> GenerateAsync(CatalogWord word, CancellationToken token);
}
public interface ICheckBatch : IAsyncDisposable
{
    Task<CheckedAudio> CheckAsync(StudioClip clip, CancellationToken token);
}
