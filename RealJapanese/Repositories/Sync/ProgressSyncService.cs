using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataLoaders.Models;

namespace Repositories.Sync;

public enum ImportMode { MergeKeepLocal, MergeUseIncoming, Replace }

public sealed record SyncSummary(string Dataset, int Added, int Changed, int Removed, int Conflicts);

public sealed class ImportPreview
{
    internal Guid Revision { get; init; }
    internal Dictionary<string, VocabSaveFile> Result { get; init; } = [];
    internal ProgressSnapshot Backup { get; init; } = new();
    public DateTimeOffset CreatedUtc { get; internal init; }
    public IReadOnlyList<SyncSummary> Summary { get; internal init; } = [];
}

public sealed class ProgressSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly ProgressStore store;
    private readonly Dictionary<string, string> hashes;
    private readonly Dictionary<string, HashSet<int>> validIds;

    public ProgressSyncService(RepositoryPaths paths, WordData words, VerbData verbs, AdjectiveData adjectives, KanjiData kanji)
    {
        store = paths.Progress;
        validIds = new()
        {
            ["Words"] = words.Words.Select(w => w.Id).ToHashSet(),
            ["Verbs"] = verbs.Words.Select(w => w.Id).ToHashSet(),
            ["Adjectives"] = adjectives.Words.Select(w => w.Id).ToHashSet(),
            ["Kanji/Singel"] = kanji.Single.Words.Select(w => w.Id).ToHashSet(),
            ["Kanji/Combined"] = kanji.Combined.Words.Select(w => w.Id).ToHashSet()
        };
        hashes = ProgressStore.DatasetNames.ToDictionary(name => name, name =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(paths.CatalogRoot, name, name.Split('/')[^1] + ".json")))));
    }

    public bool HasRecovery => store.Read().Recovery is not null;
    public byte[] ExportSnapshot() => JsonSerializer.SerializeToUtf8Bytes(Snapshot(store.Read().Data), JsonOptions);

    public ImportPreview PreviewSnapshot(byte[] bytes, ImportMode mode)
    {
        if (bytes.Length > LocalProgressTransfer.MaxSnapshotBytes) throw new InvalidDataException("The sync snapshot is too large.");
        try
        {
            return Preview(JsonSerializer.Deserialize<ProgressSnapshot>(bytes, JsonOptions)
                ?? throw new InvalidDataException("The sync snapshot is empty."), mode);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("This is not a supported progress snapshot.", exception);
        }
    }

    public ImportPreview PreviewRecovery() => Preview(store.Read().Recovery
        ?? throw new InvalidOperationException("There is no previous import to restore."), ImportMode.Replace);

    public void Apply(ImportPreview preview) => store.Import(preview.Revision, preview.Result, preview.Backup);

    private ImportPreview Preview(ProgressSnapshot incoming, ImportMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (incoming.Version != 1) throw new InvalidDataException("Unsupported sync version. Update both applications.");
        ProgressStore.ValidateShape(incoming.Data);
        if (incoming.CatalogHashes is null || incoming.CatalogHashes.Count != hashes.Count ||
            hashes.Any(pair => !incoming.CatalogHashes.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
            throw new InvalidDataException("The study catalogs differ. Update both apps to the same catalog before syncing.");
        foreach (var name in ProgressStore.DatasetNames)
            if (Categories(incoming.Data[name]).Keys.Any(id => !validIds[name].Contains(id)))
                throw new InvalidDataException("The snapshot contains an ID that is not in the study catalog.");

        var current = store.Read();
        var backup = Snapshot(current.Data);
        var result = new Dictionary<string, VocabSaveFile>();
        var summary = new List<SyncSummary>();
        foreach (var name in ProgressStore.DatasetNames)
        {
            var local = Categories(backup.Data[name]);
            var remote = Categories(incoming.Data[name]);
            var combined = mode == ImportMode.Replace ? new Dictionary<int, int>(remote) : new(local);
            if (mode != ImportMode.Replace)
                foreach (var pair in remote)
                    if (mode == ImportMode.MergeUseIncoming || !combined.ContainsKey(pair.Key)) combined[pair.Key] = pair.Value;
            summary.Add(new(name,
                combined.Keys.Count(id => !local.ContainsKey(id)),
                combined.Count(pair => local.TryGetValue(pair.Key, out var old) && old != pair.Value),
                local.Keys.Count(id => !combined.ContainsKey(id)),
                remote.Count(pair => local.TryGetValue(pair.Key, out var old) && old != pair.Value)));
            result[name] = FromCategories(combined);
        }
        return new ImportPreview { Revision = current.Revision, Result = result, Backup = backup,
            CreatedUtc = incoming.CreatedUtc, Summary = summary.AsReadOnly() };
    }

    private ProgressSnapshot Snapshot(Dictionary<string, VocabSaveFile> data) => new()
    {
        CatalogHashes = new(hashes),
        Data = data.ToDictionary(pair => pair.Key,
            pair => FromCategories(Categories(pair.Value).Where(entry => validIds[pair.Key].Contains(entry.Key)).ToDictionary()))
    };

    private static Dictionary<int, int> Categories(VocabSaveFile saved) =>
        saved.KnownIds.Select(id => (id, category: 0))
            .Concat(saved.TrainingIds.Select(id => (id, category: 1)))
            .Concat(saved.RehearsingIds.Select(id => (id, category: 2)))
            .ToDictionary(pair => pair.id, pair => pair.category);

    private static VocabSaveFile FromCategories(Dictionary<int, int> categories) => new()
    {
        KnownIds = categories.Where(pair => pair.Value == 0).Select(pair => pair.Key).Order().ToList(),
        TrainingIds = categories.Where(pair => pair.Value == 1).Select(pair => pair.Key).Order().ToList(),
        RehearsingIds = categories.Where(pair => pair.Value == 2).Select(pair => pair.Key).Order().ToList()
    };
}
