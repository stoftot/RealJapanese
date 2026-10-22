using System.Text.Json;

namespace Repositories.Audio;

/// <summary>A reviewed audio clip and the catalog snapshot it belongs to.</summary>
public sealed class StudyAudioClip
{
    internal StudyAudioClip(string dataset, int id, string japanese, string kana, string file)
    {
        Dataset = dataset;
        Id = id;
        Japanese = japanese;
        Kana = kana;
        File = file;
    }

    public string Dataset { get; }
    public int Id { get; }
    public string Japanese { get; }
    public string Kana { get; }
    /// <summary>Manifest-relative path beneath Data/Audio, with forward slashes.</summary>
    public string File { get; }
}

/// <summary>Loads reviewed Audio Studio exports without modifying study data.</summary>
public sealed class StudyAudioCatalog
{
    private const long MaxAudioBytes = 16 * 1024 * 1024;
    private readonly string audioRoot;
    private readonly IReadOnlyList<StudyAudioClip> clips;

    public StudyAudioCatalog(RepositoryPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        audioRoot = Path.GetFullPath(Path.Combine(paths.CatalogRoot, "Audio"));
        string manifest = Path.Combine(audioRoot, "manifest.json");
        try
        {
            clips = ReadClips(File.ReadAllText(manifest)).Where(IsPayloadAvailable).ToArray();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            clips = Array.Empty<StudyAudioClip>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            Error = "Audio manifest could not be loaded.";
            clips = Array.Empty<StudyAudioClip>();
        }
    }

    public string? Error { get; private set; }
    public IReadOnlyList<StudyAudioClip> Clips => clips;

    /// <summary>Parses valid entries from an Audio Studio manifest without checking payload files.</summary>
    /// <exception cref="FormatException">The JSON is not a manifest array.</exception>
    public static IReadOnlyList<StudyAudioClip> ReadClips(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new FormatException("Audio manifest must be a JSON array.");

        // Assignment in manifest order means a later valid entry replaces an earlier duplicate.
        var byKey = new Dictionary<(string Dataset, int Id), StudyAudioClip>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!TryReadClip(entry, out var clip)) continue;
            byKey[(clip.Dataset, clip.Id)] = clip;
        }
        return byKey.Values.OrderBy(clip => clip.Dataset, StringComparer.Ordinal)
            .ThenBy(clip => clip.Id).ToArray();
    }

    public StudyAudioClip? Find(string dataset, int id, string japanese, string kana)
    {
        if (string.IsNullOrWhiteSpace(dataset) || japanese is null || kana is null) return null;
        string datasetSnapshot = dataset.Trim();
        string japaneseSnapshot = japanese.Trim();
        string kanaSnapshot = kana.Trim();
        return clips.SingleOrDefault(clip => clip.Dataset == datasetSnapshot && clip.Id == id
            && clip.Japanese == japaneseSnapshot && clip.Kana == kanaSnapshot);
    }

    /// <summary>Reads a validated local payload into a browser-playable data URL.</summary>
    public async Task<string?> GetDataUrlAsync(StudyAudioClip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (!clips.Any(candidate => ReferenceEquals(candidate, clip))) return null;

        string path;
        try { path = ResolvePath(audioRoot, clip.File); }
        catch (ArgumentException) { return null; }
        if (!File.Exists(path)) return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > MaxAudioBytes) return null;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, useAsync: true);
            if (stream.Length is <= 0 or > MaxAudioBytes) return null;
            using var buffer = new MemoryStream((int)stream.Length);
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxAudioBytes) return null;
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            if (buffer.Length == 0) return null;
            string mime = MimeType(clip.File);
            return $"data:{mime};base64,{Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryReadClip(JsonElement entry, out StudyAudioClip clip)
    {
        clip = null!;
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("word", out var word)
            || word.ValueKind != JsonValueKind.Object
            || !TryString(word, "dataset", out var dataset) || string.IsNullOrWhiteSpace(dataset)
            || !word.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number
            || !idElement.TryGetInt32(out int id) || id < 0
            || !TryString(word, "japanese", out var japanese) || string.IsNullOrWhiteSpace(japanese)
            || !TryString(word, "kana", out var kana) || string.IsNullOrWhiteSpace(kana)
            || !TryString(entry, "file", out var file)) return false;

        dataset = dataset.Trim();
        japanese = japanese.Trim();
        kana = kana.Trim();
        try { _ = ResolveRelativePath(file); }
        catch (ArgumentException) { return false; }
        clip = new StudyAudioClip(dataset, id, japanese, kana, file.Replace('\\', '/'));
        return true;
    }

    private static bool TryString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(property, out var field) || field.ValueKind != JsonValueKind.String) return false;
        value = field.GetString() ?? string.Empty;
        return true;
    }

    private bool IsPayloadAvailable(StudyAudioClip clip)
    {
        try
        {
            string path = ResolvePath(audioRoot, clip.File);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Length is > 0 and <= MaxAudioBytes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static string ResolvePath(string root, string relative)
    {
        string normalized = ResolveRelativePath(relative);
        string path = Path.GetFullPath(Path.Combine(root, normalized));
        string prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Audio path escapes its root.", nameof(relative));
        return path;
    }

    private static string ResolveRelativePath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains(':'))
            throw new ArgumentException("Audio path must be relative.", nameof(relative));
        string normalized = relative.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new ArgumentException("Audio path must stay beneath its root.", nameof(relative));
        string extension = Path.GetExtension(normalized);
        if (extension is not (".opus" or ".mp3" or ".wav"))
            throw new ArgumentException("Unsupported audio extension.", nameof(relative));
        return normalized.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string MimeType(string relative) => Path.GetExtension(relative) switch
    {
        ".opus" => "audio/ogg",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        _ => throw new ArgumentException("Unsupported audio extension.", nameof(relative))
    };
}
