using System.Security.Cryptography;
using DataLoaders;
using DataLoaders.Models;

namespace Repositories.Sync;

/// <summary>Preserves progress when repeated word senses converge on their first book entry.</summary>
internal static class VocabularyDuplicateMigration
{
    private sealed record MergeEntry(string Source, int OldId, string Target, int Id, string Japanese);
    private static readonly MergeEntry[] Entries =
    [
        new("Verbs", 61, "Verbs", 2, "聞く"),
        new("Nouns", 348, "Pronouns", 21, "彼女"),
        new("Nouns", 349, "Pronouns", 22, "彼")
    ];
    // Recognize the previous bundled files in both LF and CRLF checkouts.
    private static readonly Dictionary<string, string[]> PreviousHashes = new()
    {
        ["Verbs"] = ["D354931A38118E6D20F1E59E6D8161CB7625F1977C1650B17CCB49C1FCB890D6", "339A9CF0E9D0444C6CED29D9F437112067DA884E9AC3782994390FB7BDF58866"],
        ["Nouns"] = ["53EB5A93C5B7A46EE27A1A031510BEBCE18FDCDBC1435BEC461996C7A0A03DDF", "9652D686BEC44D43F7A97626BD594A570FE45D7D234C2DDB7A3FC7099C0D445A"],
        ["Pronouns"] = ["6CB6E79E528E7677706859D7880B7A0D3C315AD3C27F36055F8392C8F6A81D0D", "C00197E078569B27094FEF110E77D36012D4502590B20B4DC37A7784353B75F9"]
    };

    internal static void Apply(Dictionary<string, VocabSaveFile> data, ProgressSnapshot? recovery, string catalogRoot)
    {
        var catalogs = PreviousHashes.Keys.ToDictionary(name => name,
            name => new JsonLoader<Word>(Path.Combine(catalogRoot, name), name + ".json").Load().ToArray());
        // Custom/older catalogs may still own the retired IDs.
        var active = Entries.Where(entry => !catalogs[entry.Source].Any(word => word.Id == entry.OldId) &&
            catalogs[entry.Target].Any(word => word.Id == entry.Id && word.Japanese == entry.Japanese)).ToArray();
        foreach (var entry in active) Merge(data, entry);
        if (recovery?.Version != ProgressStore.CurrentVersion || recovery.CatalogHashes is null) return;
        var names = active.SelectMany(entry => new[] { entry.Source, entry.Target }).Distinct().ToArray();
        var hashes = names.ToDictionary(name => name, name => Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(Path.Combine(catalogRoot, name, name + ".json")))));
        // Cross-category recovery must recognize both sides before changing either.
        if (names.Any(name => recovery.CatalogHashes.GetValueOrDefault(name) != hashes[name] &&
            !PreviousHashes[name].Contains(recovery.CatalogHashes.GetValueOrDefault(name)))) return;
        ProgressStore.ValidateShape(recovery.Data);
        foreach (var entry in active) Merge(recovery.Data, entry);
        foreach (var name in names) recovery.CatalogHashes[name] = hashes[name];
    }

    private static void Merge(Dictionary<string, VocabSaveFile> data, MergeEntry entry)
    {
        var source = data[entry.Source];
        var target = data[entry.Target];
        List<int>[] from = [source.KnownIds, source.TrainingIds, source.RehearsingIds];
        List<int>[] to = [target.KnownIds, target.TrainingIds, target.RehearsingIds];
        var oldCategory = Array.FindIndex(from, ids => ids.Contains(entry.OldId));
        if (oldCategory < 0) return;
        var category = Array.FindIndex(to, ids => ids.Contains(entry.Id));
        // Known > Training > Rehearsing. A tie keeps the retained entry's history.
        var useSource = category < 0 || oldCategory < category;
        var history = useSource ? source.Rehearsal.GetValueOrDefault(entry.OldId) : target.Rehearsal.GetValueOrDefault(entry.Id);
        foreach (var ids in from) ids.RemoveAll(id => id == entry.OldId);
        if (useSource)
        {
            foreach (var ids in to) ids.RemoveAll(id => id == entry.Id);
            to[oldCategory].Add(entry.Id);
        }
        source.Rehearsal.Remove(entry.OldId);
        target.Rehearsal.Remove(entry.Id);
        if (history is not null) target.Rehearsal[entry.Id] = history;
    }
}
