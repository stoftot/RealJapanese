using System.Text.Json;
using DataLoaders;
using DataLoaders.Models;

namespace Repositories.Sync;

// One atomic file is the commit boundary for all five datasets. Legacy saves remain untouched.
public sealed class ProgressStore
{
    public static readonly string[] DatasetNames = ["Words", "Verbs", "Adjectives", "Kanji/Singel", "Kanji/Combined"];
    private readonly object gate = new();
    private readonly string path;
    private ProgressDocument? document;

    internal ProgressStore(string root) => path = Path.Combine(root, "Progress.json");

    internal ProgressDocument Read()
    {
        lock (gate) return Clone(Load());
    }

    internal void Change(string dataset, Action<VocabSaveFile> update)
    {
        lock (gate)
        {
            var next = Clone(Load());
            update(next.Data[dataset]);
            Commit(next);
        }
    }

    internal void Import(Guid expectedRevision, Dictionary<string, VocabSaveFile> data, ProgressSnapshot backup)
    {
        lock (gate)
        {
            if (Load().Revision != expectedRevision)
                throw new InvalidOperationException("Progress changed after this preview. Preview the file again before importing.");
            Commit(new ProgressDocument { Data = Clone(data), Recovery = Clone(backup) });
        }
    }

    private ProgressDocument Load()
    {
        if (document is not null) return document;
        if (File.Exists(path))
        {
            var loaded = JsonSerializer.Deserialize<ProgressDocument>(File.ReadAllBytes(path))
                ?? throw new InvalidDataException("The progress file is empty.");
            if (loaded.Version != 1) throw new InvalidDataException("Unsupported progress version.");
            ValidateShape(loaded.Data);
            return document = loaded;
        }

        var migrated = new ProgressDocument();
        foreach (var name in DatasetNames)
        {
            var folder = Path.Combine(Path.GetDirectoryName(path)!, name);
            var saved = File.Exists(Path.Combine(folder, "SavedData.json"))
                ? new JsonLoader<VocabSaveFile>(folder, "SavedData.json").Load().FirstOrDefault() ?? new()
                : new VocabSaveFile();
            // Old versions allowed duplicate/overlapping categories. Normalize deterministically.
            saved.KnownIds = (saved.KnownIds ?? []).Distinct().ToList();
            saved.TrainingIds = (saved.TrainingIds ?? []).Except(saved.KnownIds).Distinct().ToList();
            saved.RehearsingIds = (saved.RehearsingIds ?? []).Except(saved.KnownIds).Except(saved.TrainingIds).Distinct().ToList();
            migrated.Data.Add(name, saved);
        }
        return document = migrated;
    }

    private void Commit(ProgressDocument next)
    {
        ValidateShape(next.Data);
        next.Revision = Guid.NewGuid();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            document = next; // Publish only after the durable write succeeds.
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;

    internal static void ValidateShape(Dictionary<string, VocabSaveFile>? data)
    {
        if (data is null || data.Count != DatasetNames.Length || DatasetNames.Any(name => !data.ContainsKey(name)))
            throw new InvalidDataException("The file must contain all five study datasets.");
        foreach (var saved in data.Values)
        {
            if (saved?.KnownIds is null || saved.TrainingIds is null || saved.RehearsingIds is null)
                throw new InvalidDataException("Missing progress categories.");
            var ids = saved.KnownIds.Concat(saved.TrainingIds).Concat(saved.RehearsingIds).ToArray();
            if (ids.Any(id => id < 0) || ids.Distinct().Count() != ids.Length)
                throw new InvalidDataException("Progress contains invalid IDs or overlapping categories.");
        }
    }
}

internal sealed class ProgressDocument
{
    public int Version { get; set; } = 1;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Dictionary<string, VocabSaveFile> Data { get; set; } = [];
    public ProgressSnapshot? Recovery { get; set; }
}

public sealed class ProgressSnapshot
{
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> CatalogHashes { get; set; } = [];
    public Dictionary<string, VocabSaveFile> Data { get; set; } = [];
}
