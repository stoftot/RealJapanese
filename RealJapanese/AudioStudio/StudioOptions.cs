using System.Text.Json;
using GenerateAudioUsingTTS;

namespace AudioStudio;

public sealed record StudioOptions(string RepositoryRoot, string WorkRoot, string ExportRoot, Settings Speech)
{
    public string DataRoot => Path.Combine(RepositoryRoot, "RealJapanese", "Data");
    public string RunsRoot => Path.Combine(WorkRoot, "runs");
    public string ReferencesRoot => Path.Combine(WorkRoot, "references");
    public string RuntimeRoot => Path.Combine(WorkRoot, "runtime");
    public string PitchRoot => Path.Combine(RepositoryRoot, "tooling", "audio-studio", "pitch");
    public static StudioOptions Load(IConfiguration config)
    {
        string root = Paths.RepositoryRoot();
        string settingsFile = config["AudioStudio:Settings"] ?? Path.Combine(root, ".tooling", "tts-settings.json");
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFile), GenerateAudioUsingTTS.Json.Format)
            ?? throw new InvalidDataException("Missing speech settings.");
        settings.Validate();
        var options = new StudioOptions(root,
            Path.GetFullPath(config["AudioStudio:WorkRoot"] ?? Path.Combine(root, ".tooling", "audio-studio")),
            Path.GetFullPath(config["AudioStudio:ExportRoot"] ?? Path.Combine(root, "RealJapanese", "Data", "Audio")), settings);
        foreach (string path in new[] { options.WorkRoot, options.ExportRoot })
        {
            Paths.RequireOutsideLibrary(path, settings.LibraryRoot);
            Paths.RequireChild(path, root);
        }
        return options;
    }
    public MediaTools CreateMedia() => new(Speech.Ffmpeg,
        Path.Combine(RepositoryRoot, ".tooling", "tts-transcriber", "Scripts", "python.exe"),
        Path.Combine(RepositoryRoot, ".tooling", "tts-asr-models"),
        Path.Combine(RepositoryRoot, ".tooling", "tts-asr-cache"),
        Path.Combine(RepositoryRoot, "tooling", "audio-studio-transcribe.py"), RuntimeRoot);
}
