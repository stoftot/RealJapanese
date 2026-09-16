using System.Text.Json;
using System.Text.Json.Nodes;
using DataLoaders.Models;
using Repositories;
using Repositories.Sync;

internal static class SyncChecks
{
    public static void Run(string catalogRoot, string temporaryRoot)
    {
        VerifyLegacyMigration(catalogRoot, temporaryRoot);
        VerifyAllDatasetsTransferAndRestart(catalogRoot, temporaryRoot);
        VerifyMergePoliciesAndReplace(catalogRoot, temporaryRoot);
        VerifyRecoveryAfterRestart(catalogRoot, temporaryRoot);
        VerifyStalePreview(catalogRoot, temporaryRoot);
        VerifyRejectedSnapshotsDoNotWrite(catalogRoot, temporaryRoot);
        VerifyFailedAtomicWrite(catalogRoot, temporaryRoot);
        VerifyParallelAssignmentsRemainExclusive(catalogRoot, temporaryRoot);
        VerifySeparateOwnersCannotOverwrite(catalogRoot, temporaryRoot);
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
        var legacyFiles = new Dictionary<string, byte[]>();
        foreach (var dataset in ProgressStore.DatasetNames)
        {
            var folder = Path.Combine(root, dataset);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "SavedData.json");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { new VocabSaveFile() });
            File.WriteAllBytes(path, bytes);
            legacyFiles[path] = bytes;
        }

        var fixture = Open(catalogRoot, root);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToVocab(word);

        Assert(File.Exists(Path.Combine(root, "Progress.json")), "Legacy progress was not migrated to the unified store on first change.");
        foreach (var pair in legacyFiles)
            Assert(File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value), $"Legacy file '{pair.Key}' changed during migration.");
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
    }

    private static void VerifyRejected(string catalogRoot, string temporaryRoot, string name, byte[] snapshot)
    {
        var root = NewRoot(temporaryRoot, "reject-" + name);
        var fixture = Open(catalogRoot, root);
        AssertThrows<InvalidDataException>(() => fixture.Service.PreviewSnapshot(snapshot, ImportMode.Replace), $"The {name} snapshot was accepted.");
        Assert(!File.Exists(Path.Combine(root, "Progress.json")), $"Rejecting the {name} snapshot wrote progress to disk.");
        Assert(AllCategoriesEmpty(fixture), $"Rejecting the {name} snapshot changed in-memory progress.");
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

    private static bool AllCategoriesEmpty(Fixture fixture) =>
        !fixture.Words.VocabWordIds.Any() && !fixture.Words.TrainingWordIds.Any() && !fixture.Words.RehearsingWordIds.Any();

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
