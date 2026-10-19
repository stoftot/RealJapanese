using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenerateAudioUsingTTS;

public sealed record Settings
{
    public required string LibraryRoot { get; init; }
    public required string ModelsDirectory { get; init; }
    public required string BertPython { get; init; }
    public required string FishPython { get; init; }
    public required string IrodoriPython { get; init; }
    public required string Ffmpeg { get; init; }
    public required string ReferenceAudio { get; init; }
    public double ReferenceStartSeconds { get; init; } = 10;
    public double ReferenceDurationSeconds { get; init; } = 10;
    public string? ReferenceTranscript { get; init; }
    public string ReferenceTranscriptSource { get; init; } = "User supplied; verification not recorded";
    public int Seed { get; init; } = 41;
    public string BertVoice { get; init; } = "M1";
    public bool UseGpu { get; init; } = true;
    public string VoiceDescription { get; init; } = "自然で落ち着いた会話調の声。日本語の辞書形の単語を一語だけ、明瞭に、普通の速さで発音する。語尾を伸ばさず、説明や他の言葉を付け加えない。";

    public void Validate()
    {
        foreach (var path in new[] { LibraryRoot, ModelsDirectory, BertPython, FishPython, IrodoriPython, Ffmpeg, ReferenceAudio })
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("All configured paths must be absolute.");
        if (!double.IsFinite(ReferenceStartSeconds) || ReferenceStartSeconds < 0
            || !double.IsFinite(ReferenceDurationSeconds) || ReferenceDurationSeconds is < 1 or > 30)
            throw new ArgumentException("Reference start must be nonnegative; duration must be 1–30 seconds.");
        if (!new[] { "M1", "M2", "F1", "F2" }.Contains(BertVoice))
            throw new ArgumentException("BertVoice must be M1, M2, F1 or F2.");
        if (string.IsNullOrWhiteSpace(VoiceDescription)) throw new ArgumentException("VoiceDescription must not be blank.");
        if (Seed < 0) throw new ArgumentException("Seed must be nonnegative.");
    }
}

public sealed record Options(string SettingsPath, string Output, int FirstId, int LastId, string[] Engines,
    bool DryRun, bool AllowUnconditioned)
{
    public static Options Parse(string[] args, string root)
    {
        string settings = Path.Combine(root, ".tooling", "tts-settings.json");
        string output = Path.Combine(root, ".tooling", "tts-runs", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        int first = 0, last = 10;
        string[] engines = ["bert", "fish", "irodori"];
        bool dryRun = false, allowUnconditioned = false;
        for (int i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for {args[i - 1]}.");
            switch (args[i])
            {
                case "--settings": settings = Path.GetFullPath(Value()); break;
                case "--output": output = Path.GetFullPath(Value()); break;
                case "--first-id": first = int.Parse(Value()); break;
                case "--last-id": last = int.Parse(Value()); break;
                case "--engines": engines = Value().ToLowerInvariant().Split(','); break;
                case "--dry-run": dryRun = true; break;
                case "--allow-unconditioned": allowUnconditioned = true; break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        if (first < 0 || last < first || last - (long)first > 1000) throw new ArgumentException("Invalid inclusive ID range (maximum 1001 words).");
        if (engines.Length == 0 || engines.Distinct().Count() != engines.Length
            || engines.Any(e => e is not ("bert" or "fish" or "irodori")))
            throw new ArgumentException("Engines must be a unique comma-separated selection of bert,fish,irodori.");
        Paths.RequireChild(output, Path.Combine(root, ".tooling", "tts-runs"));
        return new(settings, output, first, last, engines, dryRun, allowUnconditioned);
    }
}

public static class Paths
{
    public static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RealJapanese", "Data", "Verbs", "Verbs.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run from this repository's build output.");
    }

    public static void RequireChild(string path, string parent)
    {
        string full = Path.GetFullPath(path);
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Output must be a new subdirectory of {parent}.");
        RejectLinks(full);
    }

    public static void RejectLinks(string path)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(path)); item is not null; item = item.Parent)
            if (item.Exists && item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException($"Reparse points are not allowed in output paths: {item.FullName}");
    }

    public static void RequireOutsideLibrary(string path, string libraryRoot)
    {
        string library = Path.TrimEndingDirectorySeparator(Path.GetFullPath(libraryRoot));
        string full = Path.GetFullPath(path);
        if (full.Equals(library, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(library + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The AI library checkout is read-only; choose an output path outside it.");
        RejectLinks(full);
    }
}

public sealed record Verb(int Id, string Japanese, string Kana, string English)
{
    public static IReadOnlyList<Verb> Load(string path, int first, int last)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var all = document.RootElement.EnumerateArray().Select(item => new Verb(
            item.GetProperty("id").ValueKind == JsonValueKind.String
                ? int.Parse(item.GetProperty("id").GetString()!) : item.GetProperty("id").GetInt32(),
            item.GetProperty("japanese").GetString()!, item.GetProperty("kana").GetString()!,
            item.GetProperty("english").GetString()!)).ToArray();
        var selected = all.Where(v => v.Id >= first && v.Id <= last).OrderBy(v => v.Id).ToArray();
        if (selected.Length != last - first + 1 || selected.Select(v => v.Id).Distinct().Count() != selected.Length)
            throw new InvalidDataException("The requested catalog ID range contains missing or duplicate IDs.");
        if (selected.Any(v => string.IsNullOrWhiteSpace(v.Japanese) || string.IsNullOrWhiteSpace(v.Kana)))
            throw new InvalidDataException("Every selected verb needs Japanese text and a kana reading.");
        return selected;
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
}
