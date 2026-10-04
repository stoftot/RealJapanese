using System.Text.Json;
using Repositories.Genki;

namespace Genki.Generation;

/// <summary>One atomic checkpoint per work item, with an OS-held writer lock and atomic bank replacement.</summary>
public sealed class BatchStore
{
    public string Root { get; }
    public BatchStore(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }
    public IDisposable Lock() => new FileStream(Path.Combine(Root, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    public string FilePath(string area, string identity) => Path.Combine(Root, area, GenkiJson.Hash(identity) + ".json");
    public T? Read<T>(string area, string identity) => ReadFile<T>(FilePath(area, identity));
    public void Save<T>(string area, string identity, T value) => WriteAtomic(FilePath(area, identity), value);
    public IEnumerable<T> ReadAll<T>(string area)
    {
        var directory = Path.Combine(Root, area);
        if (!Directory.Exists(directory)) yield break;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            yield return ReadFile<T>(path) ?? throw new InvalidDataException($"Empty checkpoint {path}.");
    }
    public static T? ReadFile<T>(string path)
    {
        if (!File.Exists(path)) return default;
        // Readers see one complete version and allow atomic replacement on Windows.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<T>(stream, GenkiJson.Options);
    }
    public static void WriteAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, GenkiJson.Options); stream.Flush(flushToDisk: true); }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void ExportBank(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, leaveOpen: true))
                {
                    foreach (var state in ReadAll<QuestionState>("questions"))
                        if (state.Status == "completed" && state.Question is not null)
                            writer.WriteLine(JsonSerializer.Serialize(state.Question, GenkiJson.Compact));
                    writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed record BatchOptions
{
    public int MaxAttempts { get; init; } = 3;
    public int TagGroupSize { get; init; } = 12;
    public int AlternativeLimit { get; init; } = 3;
    public int? Limit { get; init; }
    public TimeSpan? TimeBudget { get; init; }
    public int DelayMilliseconds { get; init; }
    public void Validate()
    {
        if (MaxAttempts < 1 || MaxAttempts > 10 || TagGroupSize < 1 || TagGroupSize > 100 ||
            AlternativeLimit < 0 || AlternativeLimit > 20 || Limit is < 1 || TimeBudget <= TimeSpan.Zero || DelayMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(BatchOptions), "Invalid resource/retry bounds.");
    }
}
public sealed class BatchSummary
{
    public long Examined { get; set; }
    public long Reused { get; set; }
    public long Completed { get; set; }
    public long Rejected { get; set; }
    public long NeedsReview { get; set; }
    public long Failed { get; set; }
    public bool Paused { get; set; }
}
