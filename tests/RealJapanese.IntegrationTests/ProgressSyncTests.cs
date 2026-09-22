using System.Text.Json;
using System.Text.Json.Nodes;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Exercises unified progress persistence, snapshot validation, merge previews, imports, and recovery.</summary>
public sealed class ProgressSyncTests
{
    [Fact]
    public void Study_edits_and_snapshot_import_leave_catalog_files_unchanged()
    {
        using var workspace = new TestWorkspace();
        var original = Directory.EnumerateFiles(workspace.CatalogRoot, "*.json", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        var local = Open(workspace, "catalog-read-only-local");
        local.Words.AddToVocab(local.Words.Words.First());
        local.Verbs.AddToTraining(local.Verbs.Words.First());
        var incoming = Open(workspace, "catalog-read-only-incoming");
        incoming.Adjectives.AddToRehearsing(incoming.Adjectives.Words.First());
        local.Service.Apply(local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.MergeKeepLocal));

        foreach (var (path, bytes) in original) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void Repository_category_changes_are_exclusive_idempotent_validated_and_persistent()
    {
        using var workspace = new TestWorkspace();
        var fixture = Open(workspace, "repository-categories");
        var moved = fixture.Words.Words.First();
        var preserved = fixture.Words.Words.Skip(1).First();
        var preservedVerb = fixture.Verbs.Words.First();
        fixture.Words.AddToVocab(preserved);
        fixture.Verbs.AddToTraining(preservedVerb);

        fixture.Words.AddToVocab(moved);
        Assert.Equal(new[] { moved.Id, preserved.Id }.Order(), fixture.Words.VocabWordIds.Order());
        fixture.Words.AddToTraining(moved);
        fixture.Words.AddToTraining(moved);
        Assert.DoesNotContain(moved.Id, fixture.Words.VocabWordIds);
        Assert.Equal([moved.Id], fixture.Words.TrainingWordIds);
        fixture.Words.AddToRehearsing(moved);
        fixture.Words.AddToRehearsing(moved);
        Assert.DoesNotContain(moved.Id, fixture.Words.TrainingWordIds);
        Assert.Equal([moved.Id], fixture.Words.RehearsingWordIds);

        fixture.Words.RemoveFromRehearsing(moved);
        fixture.Words.AddToVocab(moved);
        fixture.Words.RemoveFromVocab(moved);
        fixture.Words.AddToTraining(moved);
        fixture.Words.RemoveFromTraining(moved);
        Assert.DoesNotContain(moved.Id, fixture.Words.VocabWordIds);
        Assert.DoesNotContain(moved.Id, fixture.Words.TrainingWordIds);
        Assert.DoesNotContain(moved.Id, fixture.Words.RehearsingWordIds);

        var progressPath = Path.Combine(fixture.Paths.ProgressRoot, "Progress.json");
        var beforeRejectedAdd = File.ReadAllBytes(progressPath);
        var unknown = new Word { Id = int.MaxValue, Japanese = "外", Kana = "そと", English = "outside" };
        Assert.Throws<ArgumentException>(() => fixture.Words.AddToVocab(unknown));
        Assert.Equal(beforeRejectedAdd, File.ReadAllBytes(progressPath));
        Assert.Equal([preserved.Id], fixture.Words.VocabWordIds);
        Assert.Equal([preservedVerb.Id], fixture.Verbs.TrainingWordIds);

        var restarted = Open(workspace, "repository-categories");
        Assert.Equal([preserved.Id], restarted.Words.VocabWordIds);
        Assert.Equal([preservedVerb.Id], restarted.Verbs.TrainingWordIds);
    }

    /// <summary>Legacy object and array saves normalize with Known, Training, Rehearsing precedence before the unified file takes ownership.</summary>
    [Fact]
    public void Legacy_progress_migrates_normalized_without_rewriting_legacy_files()
    {
        using var workspace = new TestWorkspace();
        var ids = CatalogIds(Open(workspace, "legacy-catalogs"));
        var progressRoot = Path.Combine(workspace.Root, "legacy");
        var legacyFiles = new Dictionary<string, byte[]>();
        var expected = new Dictionary<string, VocabSaveFile>();
        for (var index = 0; index < ProgressStore.DatasetNames.Length; index++)
        {
            var dataset = ProgressStore.DatasetNames[index];
            var folder = Path.Combine(progressRoot, dataset);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "SavedData.json");
            VocabSaveFile saved;
            if (dataset == "Kanji/Combined")
            {
                saved = new VocabSaveFile { KnownIds = null!, TrainingIds = null!, RehearsingIds = null! };
                expected[dataset] = new();
            }
            else
            {
                var values = ids[dataset];
                saved = new VocabSaveFile
                {
                    KnownIds = [values[0], values[0]],
                    TrainingIds = [values[0], values[1], values[1]],
                    RehearsingIds = [values[0], values[1], values[2], values[2]]
                };
                expected[dataset] = new VocabSaveFile
                {
                    KnownIds = [values[0]], TrainingIds = [values[1]], RehearsingIds = [values[2]]
                };
            }
            var bytes = index % 2 == 0 ? JsonSerializer.SerializeToUtf8Bytes(saved) : JsonSerializer.SerializeToUtf8Bytes(new[] { saved });
            File.WriteAllBytes(path, bytes);
            legacyFiles[path] = bytes;
        }

        var fixture = OpenAt(workspace, progressRoot);
        AssertSnapshotData(Snapshot(fixture).Data, expected);
        fixture.Words.AddToVocab(fixture.Words.Words.First(word => word.Id == expected["Words"].KnownIds[0]));
        Assert.True(File.Exists(Path.Combine(progressRoot, "Progress.json")));
        foreach (var (path, bytes) in legacyFiles) Assert.Equal(bytes, File.ReadAllBytes(path));

        foreach (var path in legacyFiles.Keys) File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(new VocabSaveFile()));
        AssertSnapshotData(Snapshot(OpenAt(workspace, progressRoot)).Data, expected);
    }

    [Fact]
    public void Snapshot_transfers_all_five_datasets_to_live_instances_and_restart()
    {
        using var workspace = new TestWorkspace();
        var source = Open(workspace, "all-source");
        source.Words.AddToVocab(source.Words.Words.First());
        source.Verbs.AddToTraining(source.Verbs.Words.First());
        source.Adjectives.AddToRehearsing(source.Adjectives.Words.First());
        source.Kanji.Single.AddToVocab(source.Kanji.Single.Words.First());
        source.Kanji.Combined.AddToTraining(source.Kanji.Combined.Words.First());

        var target = Open(workspace, "all-target");
        target.Service.Apply(target.Service.PreviewSnapshot(source.Service.ExportSnapshot(), ImportMode.Replace));
        AssertFiveAssignments(target);
        AssertFiveAssignments(Open(workspace, "all-target"));
    }

    [Theory]
    [InlineData(ImportMode.MergeKeepLocal, 1, 0, 0, 1)]
    [InlineData(ImportMode.MergeUseIncoming, 1, 1, 0, 1)]
    [InlineData(ImportMode.Replace, 1, 1, 1, 1)]
    public void Preview_reports_exact_mode_summary_without_mutating_disk_or_live_state(
        ImportMode mode, int added, int changed, int removed, int conflicts)
    {
        using var workspace = new TestWorkspace();
        var local = Open(workspace, "preview-local");
        var a = local.Words.Words.First();
        var b = local.Words.Words.Skip(1).First();
        var c = local.Words.Words.Skip(2).First();
        local.Words.AddToVocab(a);
        local.Words.AddToTraining(b);
        var incoming = Open(workspace, "preview-incoming");
        incoming.Words.AddToVocab(incoming.Words.Words.First(word => word.Id == b.Id));
        incoming.Words.AddToRehearsing(incoming.Words.Words.First(word => word.Id == c.Id));

        var progressPath = Path.Combine(local.Paths.ProgressRoot, "Progress.json");
        var beforeBytes = File.ReadAllBytes(progressPath);
        var beforeData = CaptureData(local);
        var preview = local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), mode);

        Assert.Equal(new SyncSummary("Words", added, changed, removed, conflicts),
            preview.Summary.Single(summary => summary.Dataset == "Words"));
        Assert.All(preview.Summary.Where(summary => summary.Dataset != "Words"),
            summary => Assert.Equal(new SyncSummary(summary.Dataset, 0, 0, 0, 0), summary));
        Assert.Equal(beforeBytes, File.ReadAllBytes(progressPath));
        Assert.Equal(beforeData, CaptureData(local));
    }

    [Theory]
    [InlineData(ImportMode.MergeKeepLocal, false)]
    [InlineData(ImportMode.MergeUseIncoming, true)]
    public void Merge_policy_resolves_conflict_and_adds_incoming_only_id(ImportMode mode, bool incomingWins)
    {
        using var workspace = new TestWorkspace();
        var local = Open(workspace, "merge-local");
        var conflict = local.Words.Words.First();
        local.Words.AddToVocab(conflict);
        var incoming = Open(workspace, "merge-incoming");
        incoming.Words.AddToTraining(incoming.Words.Words.First(word => word.Id == conflict.Id));
        var added = incoming.Words.Words.Skip(1).First();
        incoming.Words.AddToRehearsing(added);

        var preview = local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), mode);
        var summary = preview.Summary.Single(item => item.Dataset == "Words");
        Assert.Equal(1, summary.Conflicts);
        Assert.Equal(1, summary.Added);
        local.Service.Apply(preview);
        Assert.Contains(added.Id, local.Words.RehearsingWordIds);
        Assert.Equal(incomingWins, local.Words.TrainingWordIds.Contains(conflict.Id));
        Assert.Equal(!incomingWins, local.Words.VocabWordIds.Contains(conflict.Id));
    }

    [Fact]
    public void Replace_removes_local_only_ids_and_uses_incoming_categories()
    {
        using var workspace = new TestWorkspace();
        var local = Open(workspace, "replace-local");
        var removed = local.Words.Words.First();
        local.Words.AddToVocab(removed);
        var incoming = Open(workspace, "replace-incoming");
        var retained = incoming.Words.Words.Skip(1).First();
        incoming.Words.AddToTraining(retained);
        local.Service.Apply(local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        Assert.DoesNotContain(removed.Id, local.Words.VocabWordIds);
        Assert.Equal([retained.Id], local.Words.TrainingWordIds);
    }

    [Fact]
    public void Recovery_survives_restart_and_restores_pre_import_progress()
    {
        using var workspace = new TestWorkspace();
        var target = Open(workspace, "recovery-target");
        var original = target.Words.Words.First();
        target.Words.AddToVocab(original);
        var incoming = Open(workspace, "recovery-incoming");
        incoming.Words.AddToTraining(incoming.Words.Words.Skip(1).First());
        target.Service.Apply(target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));

        var restarted = Open(workspace, "recovery-target");
        Assert.True(restarted.Service.HasRecovery);
        restarted.Service.Apply(restarted.Service.PreviewRecovery());
        Assert.Equal([original.Id], restarted.Words.VocabWordIds);
        Assert.Empty(restarted.Words.TrainingWordIds);
    }

    [Fact]
    public void Ordinary_study_edit_preserves_recovery_across_restart()
    {
        using var workspace = new TestWorkspace();
        var target = Open(workspace, "recovery-edit");
        var original = target.Words.Words.First();
        target.Words.AddToVocab(original);
        var incoming = Open(workspace, "recovery-edit-incoming");
        incoming.Words.AddToTraining(incoming.Words.Words.Skip(1).First());
        target.Service.Apply(target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        target.Words.AddToRehearsing(target.Words.Words.Skip(2).First());

        var restarted = Open(workspace, "recovery-edit");
        Assert.True(restarted.Service.HasRecovery);
        restarted.Service.Apply(restarted.Service.PreviewRecovery());
        Assert.Equal([original.Id], restarted.Words.VocabWordIds);
        Assert.Empty(restarted.Words.TrainingWordIds);
        Assert.Empty(restarted.Words.RehearsingWordIds);
    }

    /// <summary>Each successful import replaces recovery with the state immediately before that import.</summary>
    [Fact]
    public void Second_import_replaces_recovery_with_immediate_pre_import_state()
    {
        using var workspace = new TestWorkspace();
        var target = Open(workspace, "recovery-replaced");
        target.Words.AddToVocab(target.Words.Words.First());
        var firstIncoming = Open(workspace, "recovery-first");
        var firstImported = firstIncoming.Words.Words.Skip(1).First();
        firstIncoming.Words.AddToTraining(firstImported);
        target.Service.Apply(target.Service.PreviewSnapshot(firstIncoming.Service.ExportSnapshot(), ImportMode.Replace));
        var secondIncoming = Open(workspace, "recovery-second");
        secondIncoming.Words.AddToRehearsing(secondIncoming.Words.Words.Skip(2).First());
        target.Service.Apply(target.Service.PreviewSnapshot(secondIncoming.Service.ExportSnapshot(), ImportMode.Replace));

        var restarted = Open(workspace, "recovery-replaced");
        restarted.Service.Apply(restarted.Service.PreviewRecovery());
        Assert.Equal([firstImported.Id], restarted.Words.TrainingWordIds);
        Assert.Empty(restarted.Words.VocabWordIds);
        Assert.Empty(restarted.Words.RehearsingWordIds);
    }

    [Fact]
    public void Recovery_preview_without_backup_is_rejected()
    {
        using var workspace = new TestWorkspace();
        var fixture = Open(workspace, "recovery-missing");
        fixture.Words.AddToVocab(fixture.Words.Words.First());
        Assert.Throws<InvalidOperationException>(() => fixture.Service.PreviewRecovery());
    }

    [Fact]
    public void Stale_recovery_preview_cannot_overwrite_subsequent_edit()
    {
        using var workspace = new TestWorkspace();
        var target = Open(workspace, "recovery-stale");
        target.Words.AddToVocab(target.Words.Words.First());
        var incoming = Open(workspace, "recovery-stale-incoming");
        incoming.Words.AddToTraining(incoming.Words.Words.Skip(1).First());
        target.Service.Apply(target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        var recovery = target.Service.PreviewRecovery();
        var edit = target.Words.Words.Skip(2).First();
        target.Words.AddToRehearsing(edit);
        var path = Path.Combine(target.Paths.ProgressRoot, "Progress.json");
        var afterEdit = File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(() => target.Service.Apply(recovery));
        Assert.Equal(afterEdit, File.ReadAllBytes(path));
        Assert.Equal([edit.Id], target.Words.RehearsingWordIds);
    }

    [Fact]
    public void Stale_import_preview_cannot_overwrite_subsequent_edit()
    {
        using var workspace = new TestWorkspace();
        var target = Open(workspace, "stale-target");
        var incoming = Open(workspace, "stale-incoming");
        incoming.Words.AddToVocab(incoming.Words.Words.First());
        var preview = target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace);
        var edit = target.Words.Words.Skip(1).First();
        target.Words.AddToTraining(edit);
        Assert.Throws<InvalidOperationException>(() => target.Service.Apply(preview));
        Assert.Equal([edit.Id], target.Words.TrainingWordIds);
    }

    public static TheoryData<string> InvalidSnapshotCases => new()
    {
        "malformed", "version", "missing-version", "missing-category", "extra-field", "too-many-entries",
        "hash", "unknown-id", "null-category", "negative-id", "duplicate-id", "overlapping-id",
        "missing-dataset", "extra-dataset", "oversized"
    };

    [Theory]
    [MemberData(nameof(InvalidSnapshotCases))]
    public void Invalid_snapshot_is_rejected_without_changing_existing_bytes_or_categories(string scenario)
    {
        using var workspace = new TestWorkspace();
        var source = Open(workspace, "invalid-source");
        var valid = source.Service.ExportSnapshot();
        var validWordId = source.Words.Words.First().Id;
        var snapshot = InvalidSnapshot(scenario, valid, validWordId);
        var fixture = Open(workspace, "reject-target");
        SeedRejectedFixture(fixture);
        var path = Path.Combine(fixture.Paths.ProgressRoot, "Progress.json");
        var beforeBytes = File.ReadAllBytes(path);
        var beforeData = CaptureData(fixture);

        Assert.Throws<InvalidDataException>(() => fixture.Service.PreviewSnapshot(snapshot, ImportMode.Replace));
        Assert.Equal(beforeBytes, File.ReadAllBytes(path));
        Assert.Equal(beforeData, CaptureData(fixture));
    }

    [Fact]
    public void Malformed_snapshot_does_not_create_progress_on_fresh_installation()
    {
        using var workspace = new TestWorkspace();
        var fixture = Open(workspace, "reject-fresh");
        Assert.Throws<InvalidDataException>(() => fixture.Service.PreviewSnapshot("not json"u8.ToArray(), ImportMode.Replace));
        Assert.False(File.Exists(Path.Combine(fixture.Paths.ProgressRoot, "Progress.json")));
    }

    [Fact]
    public void Undefined_import_mode_is_rejected_without_mutation()
    {
        using var workspace = new TestWorkspace();
        var source = Open(workspace, "invalid-mode-source");
        var fixture = Open(workspace, "invalid-mode-target");
        SeedRejectedFixture(fixture);
        var path = Path.Combine(fixture.Paths.ProgressRoot, "Progress.json");
        var beforeBytes = File.ReadAllBytes(path);
        var beforeData = CaptureData(fixture);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.Service.PreviewSnapshot(source.Service.ExportSnapshot(), (ImportMode)int.MaxValue));
        Assert.Equal(beforeBytes, File.ReadAllBytes(path));
        Assert.Equal(beforeData, CaptureData(fixture));
    }

    [Fact]
    public void Failed_atomic_write_preserves_disk_and_published_memory()
    {
        using var workspace = new TestWorkspace();
        var fixture = Open(workspace, "atomic");
        var original = fixture.Words.Words.First();
        fixture.Words.AddToVocab(original);
        var path = Path.Combine(fixture.Paths.ProgressRoot, "Progress.json");
        var originalBytes = File.ReadAllBytes(path);
        var attempted = fixture.Words.Words.Skip(1).First();
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = Record.Exception(() => fixture.Words.AddToTraining(attempted));
            Assert.True(error is IOException or UnauthorizedAccessException, $"Unexpected write result: {error}");
        }
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Equal([original.Id], fixture.Words.VocabWordIds);
        Assert.Empty(fixture.Words.TrainingWordIds);
    }

    [Fact]
    public void Parallel_assignments_leave_exactly_one_category_membership()
    {
        using var workspace = new TestWorkspace();
        var fixture = Open(workspace, "parallel");
        var word = fixture.Words.Words.First();
        Parallel.For(0, 24, index =>
        {
            if (index % 3 == 0) fixture.Words.AddToVocab(word);
            else if (index % 3 == 1) fixture.Words.AddToTraining(word);
            else fixture.Words.AddToRehearsing(word);
        });
        var occurrences = fixture.Words.VocabWordIds.Count(id => id == word.Id) +
            fixture.Words.TrainingWordIds.Count(id => id == word.Id) +
            fixture.Words.RehearsingWordIds.Count(id => id == word.Id);
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Separate_cached_owners_cannot_overwrite_newer_disk_revision()
    {
        using var workspace = new TestWorkspace();
        var paths = workspace.CreatePaths("separate-owners");
        var first = OpenAt(workspace, paths.ProgressRoot);
        var second = OpenAt(workspace, paths.ProgressRoot);
        _ = second.Service.ExportSnapshot();
        first.Words.AddToTraining(first.Words.Words.First());
        var path = Path.Combine(paths.ProgressRoot, "Progress.json");
        var saved = File.ReadAllBytes(path);
        Assert.Throws<InvalidOperationException>(() => second.Words.AddToVocab(second.Words.Words.First()));
        Assert.Equal(saved, File.ReadAllBytes(path));
        Assert.Empty(second.Words.VocabWordIds);
    }

    private static byte[] InvalidSnapshot(string scenario, byte[] valid, int validWordId) => scenario switch
    {
        "malformed" => "not json"u8.ToArray(),
        "version" => Mutate(valid, root => root["Version"] = 2),
        "missing-version" => Mutate(valid, root => root.Remove("Version")),
        "missing-category" => Mutate(valid, root => root["Data"]!.AsObject()["Words"]!.AsObject().Remove("KnownIds")),
        "extra-field" => Mutate(valid, root => root["Path"] = "not-allowed"),
        "too-many-entries" => Mutate(valid, root => root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] =
            new JsonArray(Enumerable.Range(0, 10000).Select(id => JsonValue.Create(id)).ToArray())),
        "hash" => Mutate(valid, root => root["CatalogHashes"]!.AsObject()["Words"] = "BAD"),
        "unknown-id" => Mutate(valid, root => root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"]!.AsArray().Add(int.MaxValue)),
        "null-category" => Mutate(valid, root => root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] = null),
        "negative-id" => Mutate(valid, root => root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"]!.AsArray().Add(-1)),
        "duplicate-id" => Mutate(valid, root => root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] = new JsonArray(validWordId, validWordId)),
        "overlapping-id" => Mutate(valid, root =>
        {
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] = new JsonArray(validWordId);
            root["Data"]!.AsObject()["Words"]!.AsObject()["TrainingIds"] = new JsonArray(validWordId);
        }),
        "missing-dataset" => Mutate(valid, root => root["Data"]!.AsObject().Remove("Verbs")),
        "extra-dataset" => Mutate(valid, root => root["Data"]!.AsObject()["Other"] = JsonSerializer.SerializeToNode(new VocabSaveFile())),
        "oversized" => new byte[LocalProgressTransfer.MaxSnapshotBytes + 1],
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
    };

    private static Fixture Open(TestWorkspace workspace, string name) => OpenAt(workspace, workspace.CreatePaths(name).ProgressRoot);

    private static Fixture OpenAt(TestWorkspace workspace, string progressRoot)
    {
        var paths = new RepositoryPaths(workspace.CatalogRoot, progressRoot);
        var words = new WordData(paths);
        var verbs = new VerbData(paths);
        var adjectives = new AdjectiveData(paths);
        var kanji = new KanjiData(paths);
        return new(paths, words, verbs, adjectives, kanji, new ProgressSyncService(paths, words, verbs, adjectives, kanji));
    }

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(source)!.AsObject();
        mutate(root);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static ProgressSnapshot Snapshot(Fixture fixture) =>
        JsonSerializer.Deserialize<ProgressSnapshot>(fixture.Service.ExportSnapshot())!;

    private static byte[] CaptureData(Fixture fixture) => JsonSerializer.SerializeToUtf8Bytes(Snapshot(fixture).Data);

    private static Dictionary<string, int[]> CatalogIds(Fixture fixture) => new()
    {
        ["Words"] = fixture.Words.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Verbs"] = fixture.Verbs.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Adjectives"] = fixture.Adjectives.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Kanji/Singel"] = fixture.Kanji.Single.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Kanji/Combined"] = fixture.Kanji.Combined.Words.Take(3).Select(word => word.Id).ToArray()
    };

    private static void AssertSnapshotData(Dictionary<string, VocabSaveFile> actual, Dictionary<string, VocabSaveFile> expected)
    {
        foreach (var dataset in ProgressStore.DatasetNames)
        {
            Assert.Equal(expected[dataset].KnownIds, actual[dataset].KnownIds);
            Assert.Equal(expected[dataset].TrainingIds, actual[dataset].TrainingIds);
            Assert.Equal(expected[dataset].RehearsingIds, actual[dataset].RehearsingIds);
        }
    }

    private static void AssertFiveAssignments(Fixture fixture)
    {
        Assert.Single(fixture.Words.VocabWordIds);
        Assert.Single(fixture.Verbs.TrainingWordIds);
        Assert.Single(fixture.Adjectives.RehearsingWordIds);
        Assert.Single(fixture.Kanji.Single.VocabWordIds);
        Assert.Single(fixture.Kanji.Combined.TrainingWordIds);
    }

    private static void SeedRejectedFixture(Fixture fixture)
    {
        fixture.Words.AddToVocab(fixture.Words.Words.First());
        fixture.Words.AddToTraining(fixture.Words.Words.Skip(1).First());
        fixture.Words.AddToRehearsing(fixture.Words.Words.Skip(2).First());
        fixture.Verbs.AddToVocab(fixture.Verbs.Words.First());
    }

    /// <summary>Holds repositories that share one progress owner for a test scenario.</summary>
    private sealed record Fixture(RepositoryPaths Paths, WordData Words, VerbData Verbs, AdjectiveData Adjectives,
        KanjiData Kanji, ProgressSyncService Service);
}
