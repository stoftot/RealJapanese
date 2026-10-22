namespace AudioStudio;

/// <summary>Resolves persisted per-clip formats, including runs saved before multiple formats existed.</summary>
public static class ClipFormats
{
    public static readonly string[] Qualities = ["OriginalWav", "Wav24000Hz16BitMono", "Mp3Mono64Kbps", "OpusMono32Kbps"];
    public static string Extension(string quality) => quality switch
    {
        "OriginalWav" or "Wav24000Hz16BitMono" => ".wav",
        "Mp3Mono64Kbps" => ".mp3", "OpusMono32Kbps" => ".opus",
        _ => throw new StudioException("Unknown output quality.")
    };
    public static Dictionary<string, AudioVariant> Available(StudioRun run, StudioClip clip)
    {
        if (clip.Variants.Count > 0) return clip.Variants;
        if (clip.File is null || clip.Sha256 is null) return [];
        return new() { [run.OutputQuality] = new()
        {
            File = clip.File, Sha256 = clip.Sha256, Seconds = clip.Seconds, Transcript = clip.Transcript,
            Validation = clip.Validation, Error = clip.Error, TranscriptSource = clip.TranscriptSource,
            TranscriptionDiagnostics = clip.TranscriptionDiagnostics
        } };
    }
    public static AudioVariant Resolve(StudioRun run, StudioClip clip, string? quality = null)
    {
        quality ??= clip.SelectedQuality ?? run.OutputQuality;
        if (!Qualities.Contains(quality) || !Available(run, clip).TryGetValue(quality, out var variant))
            throw new StudioException("This audio quality is not available for the clip.", 409);
        return variant;
    }
}
