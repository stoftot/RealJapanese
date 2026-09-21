using System.Text.Json;
using System.Text.Json.Nodes;
using DataLoaders.Models;
using Repositories;
using Repositories.Sync;

internal static class SyncChecks
{
    public static void Run(string catalogRoot, string temporaryRoot)
    {
        VerifyRepositoryCategoryChanges(catalogRoot, temporaryRoot);
        VerifyLegacyMigration(catalogRoot, temporaryRoot);
        VerifyAllDatasetsTransferAndRestart(catalogRoot, temporaryRoot);
        VerifyPreviewModesDoNotMutate(catalogRoot, temporaryRoot);
        VerifyMergePoliciesAndReplace(catalogRoot, temporaryRoot);
        VerifyRecoveryAfterRestart(catalogRoot, temporaryRoot);
        VerifyRecoveryLifecycle(catalogRoot, temporaryRoot);
        VerifyStalePreview(catalogRoot, temporaryRoot);
        VerifyRejectedSnapshotsDoNotWrite(catalogRoot, temporaryRoot);
        VerifyFailedAtomicWrite(catalogRoot, temporaryRoot);
        VerifyParallelAssignmentsRemainExclusive(catalogRoot, temporaryRoot);
        VerifySeparateOwnersCannotOverwrite(catalogRoot, temporaryRoot);
    }

    private static void VerifyRepositoryCategoryChanges(string catalogRoot, string temporaryRoot)
    {
        var root = NewRoot(temporaryRoot, "repository-categories");
        var fixture = Open(catalogRoot, root);
        var moved = fixture.Words.Words.First();
        var preserved = fixture.Words.Words.Skip(1).First();
        var preservedVerb = fixture.Verbs.Words.First();
        fixture.Words.AddToVocab(preserved);
        fixture.Verbs.AddToTraining(preservedVerb);

        fixture.Words.AddToVocab(moved);
        Assert(fixture.Words.VocabWordIds.Order().SequenceEqual(new[] { moved.Id, preserved.Id }.Order()),
            "Adding a known word lost existing word progress.");
        fixture.Words.AddToTraining(moved);
        fixture.Words.AddToTraining(moved);
        Assert(!fixture.Words.VocabWordIds.Contains(moved.Id) && fixture.Words.TrainingWordIds.SequenceEqual([moved.Id]),
            "Moving a word to training did not remain exclusive and duplicate-free.");
        fixture.Words.AddToRehearsing(moved);
        fixture.Words.AddToRehearsing(moved);
        Assert(!fixture.Words.TrainingWordIds.Contains(moved.Id) && fixture.Words.RehearsingWordIds.SequenceEqual([moved.Id]),
            "Moving a word to rehearsing did not remain exclusive and duplicate-free.");

        fixture.Words.RemoveFromRehearsing(moved);
        fixture.Words.AddToVocab(moved);
        fixture.Words.RemoveFromVocab(moved);
        fixture.Words.AddToTraining(moved);
        fixture.Words.RemoveFromTraining(moved);
        Assert(!fixture.Words.VocabWordIds.Contains(moved.Id) && !fixture.Words.TrainingWordIds.Contains(moved.Id) &&
            !fixture.Words.RehearsingWordIds.Contains(moved.Id), "Removing a word from each category left progress behind.");

        var beforeRejectedAdd = File.ReadAllBytes(Path.Combine(root, "Progress.json"));
        var unknown = new Word { Id = int.MaxValue, Japanese = "外", Kana = "そと", English = "outside" };
        AssertThrows<ArgumentException>(() => fixture.Words.AddToVocab(unknown), "A word outside the catalog was accepted.");
        Assert(beforeRejectedAdd.SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Progress.json"))),
            "Rejecting an unknown word changed progress on disk.");
        Assert(fixture.Words.VocabWordIds.SequenceEqual([preserved.Id]) &&
            fixture.Verbs.TrainingWordIds.SequenceEqual([preservedVerb.Id]),
            "Rejecting an unknown word changed other IDs or datasets.");

        var restarted = Open(catalogRoot, root);
        Assert(restarted.Words.VocabWordIds.SequenceEqual([preserved.Id]) &&
            restarted.Verbs.TrainingWordIds.SequenceEqual([preservedVerb.Id]),
            "Repository category changes did not survive restart.");
    }

    private static void VerifySeparateOwnersCannotOverwrite(string catalogRoot, string temporaryRoot)
    {
        var root = NewRoot(temporaryRoot, "separate-owners");
        var first = Open(catalogRoot, root);
        var second = Open(catalogRoot, root);
        _ = second.Service.ExportSnapshot(); // Cache the old revision before the first writer commits.
        first.Words.AddToTraining(first.Words.Words.First());
        var saved = File.ReadAllBytes(Path.Combine(root, "Progress.json"));
        AssertThrows<InvalidOperationException>(() => second.Words.AddToVocab(second.Words.Words.First()),
            "A second progress owner overwrote a newer on-disk revision.");
        Assert(saved.SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Progress.json"))), "Conflicting writer changed disk progress.");
        Assert(!second.Words.VocabWordIds.Any(), "Conflicting writer changed cached progress.");
    }

    private static void VerifyLegacyMigration(string catalogRoot, string temporaryRoot)
    {
        var root = NewRoot(temporaryRoot, "legacy");
        var catalogs = Open(catalogRoot, NewRoot(temporaryRoot, "legacy-catalogs"));
        var ids = CatalogIds(catalogs);
        var legacyFiles = new Dictionary<string, byte[]>();
        var expected = new Dictionary<string, VocabSaveFile>();
        for (var index = 0; index < ProgressStore.DatasetNames.Length; index++)
        {
            var dataset = ProgressStore.DatasetNames[index];
            var folder = Path.Combine(root, dataset);
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
                var datasetIds = ids[dataset];
                saved = new VocabSaveFile
                {
                    KnownIds = [datasetIds[0], datasetIds[0]],
                    TrainingIds = [datasetIds[0], datasetIds[1], datasetIds[1]],
                    RehearsingIds = [datasetIds[0], datasetIds[1], datasetIds[2], datasetIds[2]]
                };
                expected[dataset] = new VocabSaveFile
                {
                    KnownIds = [datasetIds[0]], TrainingIds = [datasetIds[1]], RehearsingIds = [datasetIds[2]]
                };
            }
            var bytes = index % 2 == 0
                ? JsonSerializer.SerializeToUtf8Bytes(saved)
                : JsonSerializer.SerializeToUtf8Bytes(new[] { saved });
            File.WriteAllBytes(path, bytes);
            legacyFiles[path] = bytes;
        }

        var fixture = Open(catalogRoot, root);
        var migrated = Snapshot(fixture);
        foreach (var dataset in ProgressStore.DatasetNames)
            AssertSaved(migrated.Data[dataset], expected[dataset], $"Legacy {dataset} progress was not normalized.");
        fixture.Words.AddToVocab(fixture.Words.Words.First(word => word.Id == expected["Words"].KnownIds[0]));

        Assert(File.Exists(Path.Combine(root, "Progress.json")), "Legacy progress was not migrated to the unified store on first change.");
        foreach (var pair in legacyFiles)
            Assert(File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value), $"Legacy file '{pair.Key}' changed during migration.");

        foreach (var path in legacyFiles.Keys)
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(new VocabSaveFile()));
        var restarted = Open(catalogRoot, root);
        var afterLegacyEdit = Snapshot(restarted);
        foreach (var dataset in ProgressStore.DatasetNames)
            AssertSaved(afterLegacyEdit.Data[dataset], expected[dataset],
                $"Edited legacy {dataset} progress overrode the unified store after restart.");
    }

    private static void VerifyPreviewModesDoNotMutate(string catalogRoot, string temporaryRoot)
    {
        foreach (var mode in Enum.GetValues<ImportMode>())
        {
            var localRoot = NewRoot(temporaryRoot, "preview-local-" + mode);
            var local = Open(catalogRoot, localRoot);
            var a = local.Words.Words.First();
            var b = local.Words.Words.Skip(1).First();
            var c = local.Words.Words.Skip(2).First();
            local.Words.AddToVocab(a);
            local.Words.AddToTraining(b);

            var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "preview-incoming-" + mode));
            incoming.Words.AddToVocab(incoming.Words.Words.First(word => word.Id == b.Id));
            incoming.Words.AddToRehearsing(incoming.Words.Words.First(word => word.Id == c.Id));

            var progressPath = Path.Combine(localRoot, "Progress.json");
            var beforeBytes = File.ReadAllBytes(progressPath);
            var beforeData = CaptureData(local);
            var exported = incoming.Service.ExportSnapshot();
            var preview = local.Service.PreviewSnapshot(exported, mode);
            var words = preview.Summary.Single(summary => summary.Dataset == "Words");
            var expected = mode switch
            {
                ImportMode.MergeKeepLocal => new SyncSummary("Words", 1, 0, 0, 1),
                ImportMode.MergeUseIncoming => new SyncSummary("Words", 1, 1, 0, 1),
                _ => new SyncSummary("Words", 1, 1, 1, 1)
            };
            Assert(words == expected, $"{mode} preview returned an incorrect exact Words summary.");
            Assert(preview.Summary.Where(summary => summary.Dataset != "Words")
                .All(summary => summary.Added == 0 && summary.Changed == 0 && summary.Removed == 0 && summary.Conflicts == 0),
                $"{mode} preview reported changes in untouched datasets.");
            Assert(beforeBytes.SequenceEqual(File.ReadAllBytes(progressPath)), $"{mode} preview or export changed progress on disk.");
            Assert(beforeData.SequenceEqual(CaptureData(local)), $"{mode} preview or export changed live categories.");
        }
    }

    private static void VerifyAllDatasetsTransferAndRestart(string catalogRoot, string temporaryRoot)
    {
        var source = Open(catalogRoot, NewRoot(temporaryRoot, "all-source"));
        source.Words.AddToVocab(source.Words.Words.First());
        source.Verbs.AddToTraining(source.Verbs.Words.First());
        source.Adjectives.AddToRehearsing(source.Adjectives.Words.First());
        source.Kanji.Single.AddToVocab(source.Kanji.Single.Words.First());
        source.Kanji.Combined.AddToTraining(source.Kanji.Combined.Words.First());

        var targetRoot = NewRoot(temporaryRoot, "all-target");
        var target = Open(catalogRoot, targetRoot);
        target.Service.Apply(target.Service.PreviewSnapshot(source.Service.ExportSnapshot(), ImportMode.Replace));
        AssertFiveAssignments(target, "Existing repository instances did not observe an imported snapshot.");

        var restarted = Open(catalogRoot, targetRoot);
        AssertFiveAssignments(restarted, "Imported progress did not survive a fresh RepositoryPaths restart.");
    }

    private static void VerifyMergePoliciesAndReplace(string catalogRoot, string temporaryRoot)
    {
        VerifyMergePolicy(catalogRoot, temporaryRoot, ImportMode.MergeKeepLocal, expectIncomingConflict: false);
        VerifyMergePolicy(catalogRoot, temporaryRoot, ImportMode.MergeUseIncoming, expectIncomingConflict: true);

        var local = Open(catalogRoot, NewRoot(temporaryRoot, "replace-local"));
        var removed = local.Words.Words.First();
        local.Words.AddToVocab(removed);
        var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "replace-incoming"));
        var retained = incoming.Words.Words.Skip(1).First();
        incoming.Words.AddToTraining(retained);

        local.Service.Apply(local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        Assert(!local.Words.VocabWordIds.Contains(removed.Id), "Replace import retained an ID absent from the incoming snapshot.");
        Assert(local.Words.TrainingWordIds.SequenceEqual([retained.Id]), "Replace import did not use the incoming category set.");
    }

    private static void VerifyMergePolicy(string catalogRoot, string temporaryRoot, ImportMode mode, bool expectIncomingConflict)
    {
        var suffix = mode.ToString();
        var local = Open(catalogRoot, NewRoot(temporaryRoot, "merge-local-" + suffix));
        var conflict = local.Words.Words.First();
        local.Words.AddToVocab(conflict);
        var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "merge-incoming-" + suffix));
        incoming.Words.AddToTraining(incoming.Words.Words.First(word => word.Id == conflict.Id));
        var added = incoming.Words.Words.Skip(1).First();
        incoming.Words.AddToRehearsing(added);

        var preview = local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), mode);
        var wordsSummary = preview.Summary.Single(summary => summary.Dataset == "Words");
        Assert(wordsSummary.Conflicts == 1 && wordsSummary.Added == 1, $"{mode} preview summary did not report its conflict and addition.");
        local.Service.Apply(preview);

        Assert(local.Words.RehearsingWordIds.Contains(added.Id), $"{mode} did not add the incoming-only ID.");
        Assert(local.Words.TrainingWordIds.Contains(conflict.Id) == expectIncomingConflict, $"{mode} resolved a conflict incorrectly.");
        Assert(local.Words.VocabWordIds.Contains(conflict.Id) != expectIncomingConflict, $"{mode} left the conflict in the wrong category.");
    }

    private static void VerifyRecoveryAfterRestart(string catalogRoot, string temporaryRoot)
    {
        var targetRoot = NewRoot(temporaryRoot, "recovery-target");
        var target = Open(catalogRoot, targetRoot);
        var original = target.Words.Words.First();
        target.Words.AddToVocab(original);
        var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "recovery-incoming"));
        var replacement = incoming.Words.Words.Skip(1).First();
        incoming.Words.AddToTraining(replacement);
        target.Service.Apply(target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));

        var restarted = Open(catalogRoot, targetRoot);
        Assert(restarted.Service.HasRecovery, "Recovery snapshot was not available after restart.");
        restarted.Service.Apply(restarted.Service.PreviewRecovery());
        Assert(restarted.Words.VocabWordIds.SequenceEqual([original.Id]), "Recovery did not restore the pre-import progress.");
        Assert(!restarted.Words.TrainingWordIds.Any(), "Recovery retained imported progress.");
    }

    private static void VerifyRecoveryLifecycle(string catalogRoot, string temporaryRoot)
    {
        VerifyOrdinaryEditsPreserveRecovery(catalogRoot, temporaryRoot);
        VerifySecondImportReplacesRecovery(catalogRoot, temporaryRoot);
        VerifyMissingAndStaleRecovery(catalogRoot, temporaryRoot);
    }

    private static void VerifyOrdinaryEditsPreserveRecovery(string catalogRoot, string temporaryRoot)
    {
        var root = NewRoot(temporaryRoot, "recovery-edit");
        var target = Open(catalogRoot, root);
        var original = target.Words.Words.First();
        target.Words.AddToVocab(original);
        var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "recovery-edit-incoming"));
        var imported = incoming.Words.Words.Skip(1).First();
        incoming.Words.AddToTraining(imported);
        target.Service.Apply(target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        target.Words.AddToRehearsing(target.Words.Words.Skip(2).First());

        var restarted = Open(catalogRoot, root);
        Assert(restarted.Service.HasRecovery, "An ordinary study edit discarded recovery after restart.");
        restarted.Service.Apply(restarted.Service.PreviewRecovery());
        Assert(restarted.Words.VocabWordIds.SequenceEqual([original.Id]) &&
            !restarted.Words.TrainingWordIds.Any() && !restarted.Words.RehearsingWordIds.Any(),
            "Recovery changed by an ordinary study edit instead of restoring the pre-import state.");
    }

    private static void VerifySecondImportReplacesRecovery(string catalogRoot, string temporaryRoot)
    {
        var root = NewRoot(temporaryRoot, "recovery-replaced");
        var target = Open(catalogRoot, root);
        target.Words.AddToVocab(target.Words.Words.First());

        var firstIncoming = Open(catalogRoot, NewRoot(temporaryRoot, "recovery-replaced-first"));
        var firstImported = firstIncoming.Words.Words.Skip(1).First();
        firstIncoming.Words.AddToTraining(firstImported);
        target.Service.Apply(target.Service.PreviewSnapshot(firstIncoming.Service.ExportSnapshot(), ImportMode.Replace));

        var secondIncoming = Open(catalogRoot, NewRoot(temporaryRoot, "recovery-replaced-second"));
        var secondImported = secondIncoming.Words.Words.Skip(2).First();
        secondIncoming.Words.AddToRehearsing(secondImported);
        target.Service.Apply(target.Service.PreviewSnapshot(secondIncoming.Service.ExportSnapshot(), ImportMode.Replace));

        var restarted = Open(catalogRoot, root);
        restarted.Service.Apply(restarted.Service.PreviewRecovery());
        Assert(restarted.Words.TrainingWordIds.SequenceEqual([firstImported.Id]) &&
            !restarted.Words.VocabWordIds.Any() && !restarted.Words.RehearsingWordIds.Any(),
            "A second import did not replace recovery with its immediate pre-import state.");
    }

    private static void VerifyMissingAndStaleRecovery(string catalogRoot, string temporaryRoot)
    {
        var withoutRecovery = Open(catalogRoot, NewRoot(temporaryRoot, "recovery-missing"));
        withoutRecovery.Words.AddToVocab(withoutRecovery.Words.Words.First());
        AssertThrows<InvalidOperationException>(() => withoutRecovery.Service.PreviewRecovery(),
            "Recovery preview succeeded without an import backup.");

        var root = NewRoot(temporaryRoot, "recovery-stale");
        var target = Open(catalogRoot, root);
        var original = target.Words.Words.First();
        target.Words.AddToVocab(original);
        var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "recovery-stale-incoming"));
        incoming.Words.AddToTraining(incoming.Words.Words.Skip(1).First());
        target.Service.Apply(target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        var recovery = target.Service.PreviewRecovery();
        var subsequentEdit = target.Words.Words.Skip(2).First();
        target.Words.AddToRehearsing(subsequentEdit);
        var afterEdit = File.ReadAllBytes(Path.Combine(root, "Progress.json"));

        AssertThrows<InvalidOperationException>(() => target.Service.Apply(recovery),
            "A stale recovery preview overwrote a subsequent study edit.");
        Assert(afterEdit.SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Progress.json"))) &&
            target.Words.RehearsingWordIds.SequenceEqual([subsequentEdit.Id]),
            "Rejecting a stale recovery preview changed the subsequent study edit.");
    }

    private static void VerifyStalePreview(string catalogRoot, string temporaryRoot)
    {
        var target = Open(catalogRoot, NewRoot(temporaryRoot, "stale-target"));
        var incoming = Open(catalogRoot, NewRoot(temporaryRoot, "stale-incoming"));
        incoming.Words.AddToVocab(incoming.Words.Words.First());
        var preview = target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace);
        var edit = target.Words.Words.Skip(1).First();
        target.Words.AddToTraining(edit);

        AssertThrows<InvalidOperationException>(() => target.Service.Apply(preview), "A preview remained valid after local progress changed.");
        Assert(target.Words.TrainingWordIds.SequenceEqual([edit.Id]), "Rejecting a stale preview changed local progress.");
    }

    private static void VerifyRejectedSnapshotsDoNotWrite(string catalogRoot, string temporaryRoot)
    {
        var source = Open(catalogRoot, NewRoot(temporaryRoot, "invalid-source"));
        var valid = source.Service.ExportSnapshot();
        var validWordId = source.Words.Words.First().Id;
        var freshRoot = NewRoot(temporaryRoot, "reject-fresh");
        var fresh = Open(catalogRoot, freshRoot);
        AssertThrows<InvalidDataException>(() => fresh.Service.PreviewSnapshot("not json"u8.ToArray(), ImportMode.Replace),
            "A malformed snapshot was accepted on a fresh installation.");
        Assert(!File.Exists(Path.Combine(freshRoot, "Progress.json")), "A rejected snapshot created progress on a fresh installation.");
        VerifyRejected(catalogRoot, temporaryRoot, "malformed", "not json"u8.ToArray());
        VerifyRejected(catalogRoot, temporaryRoot, "version", Mutate(valid, root => root["Version"] = 2));
        VerifyRejected(catalogRoot, temporaryRoot, "missing-version", Mutate(valid, root => root.Remove("Version")));
        VerifyRejected(catalogRoot, temporaryRoot, "missing-category", Mutate(valid, root =>
            root["Data"]!.AsObject()["Words"]!.AsObject().Remove("KnownIds")));
        VerifyRejected(catalogRoot, temporaryRoot, "extra-field", Mutate(valid, root => root["Path"] = "ignored-is-not-allowed"));
        VerifyRejected(catalogRoot, temporaryRoot, "too-many-entries", Mutate(valid, root =>
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] =
                new JsonArray(Enumerable.Range(0, 10000).Select(id => JsonValue.Create(id)).ToArray())));
        VerifyRejected(catalogRoot, temporaryRoot, "hash", Mutate(valid, root => root["CatalogHashes"]!.AsObject()["Words"] = "BAD"));
        VerifyRejected(catalogRoot, temporaryRoot, "unknown-id", Mutate(valid, root =>
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"]!.AsArray().Add(int.MaxValue)));
        VerifyRejected(catalogRoot, temporaryRoot, "null-category", Mutate(valid, root =>
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] = null));
        VerifyRejected(catalogRoot, temporaryRoot, "negative-id", Mutate(valid, root =>
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"]!.AsArray().Add(-1)));
        VerifyRejected(catalogRoot, temporaryRoot, "duplicate-id", Mutate(valid, root =>
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] = new JsonArray(validWordId, validWordId)));
        VerifyRejected(catalogRoot, temporaryRoot, "overlapping-id", Mutate(valid, root =>
        {
            root["Data"]!.AsObject()["Words"]!.AsObject()["KnownIds"] = new JsonArray(validWordId);
            root["Data"]!.AsObject()["Words"]!.AsObject()["TrainingIds"] = new JsonArray(validWordId);
        }));
        VerifyRejected(catalogRoot, temporaryRoot, "missing-dataset", Mutate(valid, root =>
            root["Data"]!.AsObject().Remove("Verbs")));
        VerifyRejected(catalogRoot, temporaryRoot, "extra-dataset", Mutate(valid, root =>
            root["Data"]!.AsObject()["Other"] = JsonSerializer.SerializeToNode(new VocabSaveFile())));
        VerifyRejected(catalogRoot, temporaryRoot, "oversized", new byte[LocalProgressTransfer.MaxSnapshotBytes + 1]);
        VerifyRejectedMode(catalogRoot, temporaryRoot, valid);
    }

    private static void VerifyRejected(string catalogRoot, string temporaryRoot, string name, byte[] snapshot)
    {
        var root = NewRoot(temporaryRoot, "reject-" + name);
        var fixture = Open(catalogRoot, root);
        SeedRejectedFixture(fixture);
        var progressPath = Path.Combine(root, "Progress.json");
        var beforeBytes = File.ReadAllBytes(progressPath);
        var beforeData = CaptureData(fixture);
        AssertThrows<InvalidDataException>(() => fixture.Service.PreviewSnapshot(snapshot, ImportMode.Replace), $"The {name} snapshot was accepted.");
        Assert(beforeBytes.SequenceEqual(File.ReadAllBytes(progressPath)), $"Rejecting the {name} snapshot changed progress bytes.");
        Assert(beforeData.SequenceEqual(CaptureData(fixture)), $"Rejecting the {name} snapshot changed live categories.");
    }

    private static void VerifyRejectedMode(string catalogRoot, string temporaryRoot, byte[] valid)
    {
        var root = NewRoot(temporaryRoot, "reject-import-mode");
        var fixture = Open(catalogRoot, root);
        SeedRejectedFixture(fixture);
        var progressPath = Path.Combine(root, "Progress.json");
        var beforeBytes = File.ReadAllBytes(progressPath);
        var beforeData = CaptureData(fixture);
        AssertThrows<ArgumentOutOfRangeException>(() => fixture.Service.PreviewSnapshot(valid, (ImportMode)int.MaxValue),
            "An undefined import mode was accepted.");
        Assert(beforeBytes.SequenceEqual(File.ReadAllBytes(progressPath)), "Rejecting an undefined import mode changed progress bytes.");
        Assert(beforeData.SequenceEqual(CaptureData(fixture)), "Rejecting an undefined import mode changed live categories.");
    }

    private static void SeedRejectedFixture(Fixture fixture)
    {
        fixture.Words.AddToVocab(fixture.Words.Words.First());
        fixture.Words.AddToTraining(fixture.Words.Words.Skip(1).First());
        fixture.Words.AddToRehearsing(fixture.Words.Words.Skip(2).First());
        fixture.Verbs.AddToVocab(fixture.Verbs.Words.First());
    }

    private static void VerifyFailedAtomicWrite(string catalogRoot, string temporaryRoot)
    {
        var root = NewRoot(temporaryRoot, "atomic");
        var fixture = Open(catalogRoot, root);
        var original = fixture.Words.Words.First();
        fixture.Words.AddToVocab(original);
        var progressPath = Path.Combine(root, "Progress.json");
        var originalBytes = File.ReadAllBytes(progressPath);
        var attempted = fixture.Words.Words.Skip(1).First();

        using (new FileStream(progressPath, FileMode.Open, FileAccess.Read, FileShare.None))
            AssertFileWriteFails(() => fixture.Words.AddToTraining(attempted), "A write unexpectedly succeeded while Progress.json was locked.");

        Assert(File.ReadAllBytes(progressPath).SequenceEqual(originalBytes), "A failed atomic write changed the existing progress file.");
        Assert(fixture.Words.VocabWordIds.SequenceEqual([original.Id]) && !fixture.Words.TrainingWordIds.Any(),
            "A failed atomic write changed the published in-memory progress.");
    }

    private static void VerifyParallelAssignmentsRemainExclusive(string catalogRoot, string temporaryRoot)
    {
        var fixture = Open(catalogRoot, NewRoot(temporaryRoot, "parallel"));
        var word = fixture.Words.Words.First();
        Parallel.For(0, 24, index =>
        {
            switch (index % 3)
            {
                case 0: fixture.Words.AddToVocab(word); break;
                case 1: fixture.Words.AddToTraining(word); break;
                default: fixture.Words.AddToRehearsing(word); break;
            }
        });

        var occurrences = fixture.Words.VocabWordIds.Count(id => id == word.Id)
            + fixture.Words.TrainingWordIds.Count(id => id == word.Id)
            + fixture.Words.RehearsingWordIds.Count(id => id == word.Id);
        Assert(occurrences == 1, "Parallel category assignments left an ID missing, duplicated, or overlapping.");
    }

    private static Fixture Open(string catalogRoot, string progressRoot)
    {
        var paths = new RepositoryPaths(catalogRoot, progressRoot);
        var words = new WordData(paths);
        var verbs = new VerbData(paths);
        var adjectives = new AdjectiveData(paths);
        var kanji = new KanjiData(paths);
        return new(paths, words, verbs, adjectives, kanji,
            new ProgressSyncService(paths, words, verbs, adjectives, kanji));
    }

    private static string NewRoot(string temporaryRoot, string name)
    {
        var root = Path.Combine(temporaryRoot, "sync-" + name);
        Directory.CreateDirectory(root);
        return root;
    }

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(source)!.AsObject();
        mutate(root);
        return JsonSerializer.SerializeToUtf8Bytes(root);
    }

    private static ProgressSnapshot Snapshot(Fixture fixture) =>
        JsonSerializer.Deserialize<ProgressSnapshot>(fixture.Service.ExportSnapshot())
        ?? throw new InvalidOperationException("An exported snapshot could not be read by the storage check.");

    private static byte[] CaptureData(Fixture fixture) => JsonSerializer.SerializeToUtf8Bytes(Snapshot(fixture).Data);

    private static Dictionary<string, int[]> CatalogIds(Fixture fixture) => new()
    {
        ["Words"] = fixture.Words.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Verbs"] = fixture.Verbs.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Adjectives"] = fixture.Adjectives.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Kanji/Singel"] = fixture.Kanji.Single.Words.Take(3).Select(word => word.Id).ToArray(),
        ["Kanji/Combined"] = fixture.Kanji.Combined.Words.Take(3).Select(word => word.Id).ToArray()
    };

    private static void AssertSaved(VocabSaveFile actual, VocabSaveFile expected, string message)
    {
        Assert(actual.KnownIds.SequenceEqual(expected.KnownIds) &&
            actual.TrainingIds.SequenceEqual(expected.TrainingIds) &&
            actual.RehearsingIds.SequenceEqual(expected.RehearsingIds), message);
    }

    private static void AssertFiveAssignments(Fixture fixture, string message)
    {
        Assert(fixture.Words.VocabWordIds.Count() == 1, message);
        Assert(fixture.Verbs.TrainingWordIds.Count() == 1, message);
        Assert(fixture.Adjectives.RehearsingWordIds.Count() == 1, message);
        Assert(fixture.Kanji.Single.VocabWordIds.Count() == 1, message);
        Assert(fixture.Kanji.Combined.TrainingWordIds.Count() == 1, message);
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void AssertFileWriteFails(Action action, string message)
    {
        try { action(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record Fixture(
        RepositoryPaths Paths,
        WordData Words,
        VerbData Verbs,
        AdjectiveData Adjectives,
        KanjiData Kanji,
        ProgressSyncService Service);
}
