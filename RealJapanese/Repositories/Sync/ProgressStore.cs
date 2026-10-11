using System.Text.Json;
using DataLoaders;
using DataLoaders.Models;

namespace Repositories.Sync;

// One atomic file is the commit boundary for all datasets. Legacy saves remain untouched.
public sealed class ProgressStore
{
    public const int CurrentVersion = 4;
    public const string LegacyVersionFile = "LegacyProgressVersion.json";
    public static readonly string[] Version2DatasetNames = ["Words", "Nouns", "Verbs", "Adjectives", "Kanji/Singel", "Kanji/Combined"];
    public static readonly string[] DatasetNames = [.. Version2DatasetNames, .. VocabularyCatalog.AdditionalDatasets];
    private readonly object gate = new();
    private readonly string path;
    private readonly string catalogRoot;
    private ProgressDocument? document;

    internal ProgressStore(string root, string catalogRoot)
    {
        path = Path.Combine(root, "Progress.json");
        this.catalogRoot = catalogRoot;
    }

    internal ProgressDocument Read()
    {
        lock (gate) return Clone(Load());
    }

    internal void Change(string dataset, Action<VocabSaveFile> update)
        => ChangeIf(dataset, saved => { update(saved); return true; });

    internal void ChangeIf(string dataset, Func<VocabSaveFile, bool> update)
    {
        lock (gate)
        {
            var next = Clone(Load());
            if (update(next.Data[dataset])) Commit(next);
        }
    }

    internal void Import(Guid expectedRevision, Dictionary<string, VocabSaveFile> data, ProgressSnapshot backup)
    {
        lock (gate)
        {
            if (Load().Revision != expectedRevision)
                throw new InvalidOperationException("Progress changed after this preview. Refresh the preview before applying.");
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
            if (loaded.Version == 1)
            {
                ValidateShape(loaded.Data, version: 1);
                WordNounMigration.Apply(loaded.Data);
                WordNounMigration.ApplyRecovery(loaded.Recovery);
                loaded.Version = 2;
            }
            if (loaded.Version == 2)
            {
                ValidateShape(loaded.Data, version: 2);
                VocabularyMigration.Apply(loaded.Data);
                VocabularyMigration.ApplyRecovery(loaded.Recovery, catalogRoot);
                loaded.Version = 3;
            }
            if (loaded.Version == 3) loaded.Version = CurrentVersion;
            if (loaded.Recovery?.Version == 3) loaded.Recovery.Version = CurrentVersion;
            if (loaded.Version != CurrentVersion) throw new InvalidDataException("Unsupported progress version.");
            ValidateShape(loaded.Data);
            return document = loaded;
        }

        var migrated = new ProgressDocument();
        var root = Path.GetDirectoryName(path)!;
        var versionPath = Path.Combine(root, LegacyVersionFile);
        var version = File.Exists(Path.Combine(root, "Nouns", "SavedData.json")) ? 2 : 1;
        if (File.Exists(versionPath))
        {
            using var marker = JsonDocument.Parse(File.ReadAllBytes(versionPath));
            version = marker.RootElement.GetProperty("Version").GetInt32();
            if (version != 3) throw new InvalidDataException("Unsupported legacy progress version.");
        }
        else if (VocabularyCatalog.AdditionalDatasets.Any(name => File.Exists(Path.Combine(root, name, "SavedData.json"))))
            throw new InvalidDataException("Categorized legacy saves require LegacyProgressVersion.json to identify their IDs safely.");
        foreach (var name in NamesForVersion(version))
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
        if (version == 1) WordNounMigration.Apply(migrated.Data);
        if (version < 3) VocabularyMigration.Apply(migrated.Data);
        return document = migrated;
    }

    private void Commit(ProgressDocument next)
    {
        ValidateShape(next.Data);
        next.Revision = Guid.NewGuid();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(next);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Separate app processes must never silently overwrite each other's cached progress.
        using var ownership = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path))
        {
            var onDisk = JsonSerializer.Deserialize<ProgressDocument>(File.ReadAllBytes(path))
                ?? throw new InvalidDataException("The progress file is empty.");
            if (onDisk.Revision != Load().Revision)
                throw new InvalidOperationException("Another app changed this progress folder. Restart this app before saving or syncing.");
        }
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

    internal static IEnumerable<string> NamesForVersion(int version) => version switch
    {
        1 => Version2DatasetNames.Where(name => name != "Nouns"),
        2 => Version2DatasetNames,
        3 => DatasetNames,
        CurrentVersion => DatasetNames,
        _ => throw new InvalidDataException("Unsupported progress version.")
    };

    internal static void ValidateShape(Dictionary<string, VocabSaveFile>? data, int version = CurrentVersion)
    {
        var names = NamesForVersion(version).ToArray();
        if (data is null || data.Count != names.Length || names.Any(name => !data.ContainsKey(name)))
            throw new InvalidDataException("The file must contain all study datasets for its version.");
        foreach (var saved in data.Values)
        {
            if (saved?.KnownIds is null || saved.TrainingIds is null || saved.RehearsingIds is null)
                throw new InvalidDataException("Missing progress categories.");
            var ids = saved.KnownIds.Concat(saved.TrainingIds).Concat(saved.RehearsingIds).ToArray();
            if (ids.Any(id => id < 0) || ids.Distinct().Count() != ids.Length)
                throw new InvalidDataException("Progress contains invalid IDs or overlapping categories.");
            if (saved.Rehearsal is null || saved.Rehearsal.Any(pair =>
                    pair.Value is null || pair.Value.CorrectDays is < 0 or > 6 ||
                    (pair.Value.CorrectDays > 0 && pair.Value.LastCorrectDay is null) ||
                    (saved.KnownIds.Contains(pair.Key)
                        ? !pair.Value.AwaitingRetry || pair.Value.CorrectDays != 0 || pair.Value.LastResetDay is null
                        : !saved.RehearsingIds.Contains(pair.Key) || pair.Value.AwaitingRetry)))
                throw new InvalidDataException("Progress contains invalid rehearsal history.");
        }
    }
}

internal sealed class ProgressDocument
{
    public int Version { get; set; } = ProgressStore.CurrentVersion;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Dictionary<string, VocabSaveFile> Data { get; set; } = [];
    public ProgressSnapshot? Recovery { get; set; }
}

public sealed class ProgressSnapshot
{
    public int Version { get; set; } = ProgressStore.CurrentVersion;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> CatalogHashes { get; set; } = [];
    public Dictionary<string, VocabSaveFile> Data { get; set; } = [];
}
