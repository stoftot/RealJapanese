using GenerateAudioUsingTTS;

namespace AudioStudio;

/// <summary>Keeps each voice's audio and transcript tied to the run's original reference snapshot.</summary>
public static class RunReferences
{
    public static string? AudioFile(StudioRun run, string engine) => run.ReferenceFor(engine) is null ? null
        : run.EngineReferences is null ? "reference.wav" : $"references/{engine}.wav";

    public static Settings SettingsFor(StudioRun run, string engine, Settings fallback) =>
        (run.SpeechSettings ?? fallback) with { ReferenceTranscript = run.ReferenceFor(engine)?.Transcript };

    public static void Prepare(StudioRun run, StudioStore store)
    {
        foreach (string engine in run.Engines)
        {
            if (AudioFile(run, engine) is not { } relative) continue;
            string destination = StudioStore.SafePath(store.RunFolder(run.Id), relative);
            // Resume uses the saved audio even when the library reference has since changed.
            if (File.Exists(destination)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(store.ReferenceAudio(run.ReferenceFor(engine)!.Id), destination, false);
        }
    }
}
