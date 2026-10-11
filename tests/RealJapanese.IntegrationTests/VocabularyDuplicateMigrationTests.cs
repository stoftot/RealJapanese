using System.Security.Cryptography;
using System.Text.Json;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Protects merged vocabulary identities across progress loading, recovery, and sync.</summary>
public sealed class VocabularyDuplicateMigrationTests
{
    private const string PreviousVerbCatalogHash = "D354931A38118E6D20F1E59E6D8161CB7625F1977C1650B17CCB49C1FCB890D6";
    private const string PreviousNounCatalogHash = "53EB5A93C5B7A46EE27A1A031510BEBCE18FDCDBC1435BEC461996C7A0A03DDF";
    private const string PreviousPronounCatalogHash = "6CB6E79E528E7677706859D7880B7A0D3C315AD3C27F36055F8392C8F6A81D0D";
    private static readonly DateOnly PracticeDay = new(2026, 9, 21);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_v3_and_unified_v4_move_retired_rehearsal_progress_to_id_2(bool unified)
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var saved = new VocabSaveFile
        {
            RehearsingIds = [61],
            Rehearsal = { [61] = Rehearsal(correctDays: 3) }
        };
        WriteProgress(paths, unified ? 4 : 3, saved);

        var verbs = new VerbData(paths);

        Assert.Contains(2, verbs.RehearsingWordIds);
        Assert.DoesNotContain(61, verbs.RehearsingWordIds);
        Assert.Equal(Rehearsal(correctDays: 3), Snapshot(paths).Data["Verbs"].Rehearsal[2]);
    }

    [Fact]
    public void Higher_priority_category_wins_and_retired_entry_history_moves_with_it()
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var saved = new VocabSaveFile
        {
            KnownIds = [61],
            TrainingIds = [2],
            Rehearsal = { [61] = new RehearsalProgress { AwaitingRetry = true, LastResetDay = PracticeDay } }
        };
        WriteProgress(paths, 4, saved);

        var verbs = new VerbData(paths);

        Assert.Contains(2, verbs.VocabWordIds);
        Assert.DoesNotContain(2, verbs.TrainingWordIds);
        Assert.DoesNotContain(61, verbs.VocabWordIds);
        Assert.Equal(saved.Rehearsal[61], Snapshot(paths).Data["Verbs"].Rehearsal[2]);
    }

    [Fact]
    public void Retained_id_history_wins_when_both_entries_are_in_the_winning_category()
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var retainedHistory = new RehearsalProgress { AwaitingRetry = true, LastResetDay = PracticeDay };
        var retiredHistory = new RehearsalProgress { AwaitingRetry = true, LastResetDay = PracticeDay.AddDays(-1) };
        var saved = new VocabSaveFile
        {
            KnownIds = [2, 61],
            Rehearsal = { [2] = retainedHistory, [61] = retiredHistory }
        };
        WriteProgress(paths, 4, saved);

        var verbs = new VerbData(paths);

        Assert.Equal([2], verbs.VocabWordIds);
        Assert.Equal(retainedHistory, Snapshot(paths).Data["Verbs"].Rehearsal[2]);
        Assert.DoesNotContain(61, Snapshot(paths).Data["Verbs"].Rehearsal.Keys);
    }

    [Fact]
    public void Saving_and_restarting_does_not_restore_retired_id_61()
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        WriteProgress(paths, 4, new VocabSaveFile { RehearsingIds = [61], Rehearsal = { [61] = Rehearsal(2) } });

        var verbs = new VerbData(paths);
        verbs.AddToVocab(verbs.Words.Single(word => word.Id == 2));
        using var persisted = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(paths.ProgressRoot, "Progress.json")));
        Assert.DoesNotContain(61, persisted.RootElement.GetProperty("Data").GetProperty("Verbs").GetProperty("RehearsingIds")
            .EnumerateArray().Select(id => id.GetInt32()));

        var restarted = new VerbData(workspace.CreatePaths());
        Assert.Contains(2, restarted.VocabWordIds);
        Assert.DoesNotContain(61, restarted.VocabWordIds.Concat(restarted.TrainingWordIds).Concat(restarted.RehearsingWordIds));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Earlier_noun_known_entry_wins_over_pronoun_rehearsal_and_clears_losing_history(int version)
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var data = EmptyData();
        data["Nouns"].KnownIds = [348];
        data["Pronouns"].RehearsingIds = [21];
        data["Pronouns"].Rehearsal[21] = Rehearsal(3);
        WriteProgress(paths, version, data);

        var nouns = new NounData(paths);
        var pronouns = new PronounData(paths);

        Assert.DoesNotContain(348, nouns.VocabWordIds.Concat(nouns.TrainingWordIds).Concat(nouns.RehearsingWordIds));
        Assert.Contains(21, pronouns.VocabWordIds);
        Assert.DoesNotContain(21, pronouns.RehearsingWordIds);
        Assert.DoesNotContain(21, Snapshot(paths).Data["Pronouns"].Rehearsal.Keys);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Earlier_noun_rehearsal_history_moves_to_canonical_pronoun_id(int version)
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var data = EmptyData();
        data["Nouns"].RehearsingIds = [349];
        data["Nouns"].Rehearsal[349] = Rehearsal(4);
        WriteProgress(paths, version, data);

        var nouns = new NounData(paths);
        var pronouns = new PronounData(paths);

        Assert.DoesNotContain(349, nouns.VocabWordIds.Concat(nouns.TrainingWordIds).Concat(nouns.RehearsingWordIds));
        Assert.Contains(22, pronouns.RehearsingWordIds);
        Assert.Equal(Rehearsal(4), Snapshot(paths).Data["Pronouns"].Rehearsal[22]);
        Assert.DoesNotContain(349, Snapshot(paths).Data["Nouns"].Rehearsal.Keys);
    }

    [Fact]
    public void Cross_category_migrations_survive_save_restart_and_sync_without_retired_noun_ids()
    {
        using var sourceWorkspace = new TestWorkspace(seedGeneralWords: false);
        using var targetWorkspace = new TestWorkspace(seedGeneralWords: false);
        var sourcePaths = sourceWorkspace.CreatePaths("source");
        var targetPaths = targetWorkspace.CreatePaths("target");
        var data = EmptyData();
        data["Nouns"].KnownIds = [348];
        data["Pronouns"].RehearsingIds = [21];
        data["Pronouns"].Rehearsal[21] = Rehearsal(2);
        data["Nouns"].RehearsingIds = [349];
        data["Nouns"].Rehearsal[349] = Rehearsal(5);
        WriteProgress(sourcePaths, 4, data);

        var sourcePronouns = new PronounData(sourcePaths);
        sourcePronouns.AddToTraining(sourcePronouns.Words.First(word => word.Id != 21 && word.Id != 22));
        var persisted = Snapshot(sourcePaths);
        Assert.Empty(persisted.Data["Nouns"].KnownIds);
        Assert.Empty(persisted.Data["Nouns"].RehearsingIds);
        Assert.Contains(21, persisted.Data["Pronouns"].KnownIds);
        Assert.Contains(22, persisted.Data["Pronouns"].RehearsingIds);
        Assert.Equal(Rehearsal(5), persisted.Data["Pronouns"].Rehearsal[22]);
        Assert.DoesNotContain(21, persisted.Data["Pronouns"].Rehearsal.Keys);
        Assert.DoesNotContain(349, persisted.Data["Nouns"].Rehearsal.Keys);

        var restartedNouns = new NounData(sourceWorkspace.CreatePaths("source"));
        Assert.DoesNotContain(348, restartedNouns.VocabWordIds.Concat(restartedNouns.TrainingWordIds).Concat(restartedNouns.RehearsingWordIds));
        Assert.DoesNotContain(349, restartedNouns.VocabWordIds.Concat(restartedNouns.TrainingWordIds).Concat(restartedNouns.RehearsingWordIds));

        var target = Sync(targetPaths);
        target.Apply(target.PreviewSnapshot(Sync(sourcePaths).ExportSnapshot(), ImportMode.Replace));
        var imported = Snapshot(targetPaths);
        Assert.Empty(imported.Data["Nouns"].KnownIds);
        Assert.Empty(imported.Data["Nouns"].RehearsingIds);
        Assert.Contains(21, imported.Data["Pronouns"].KnownIds);
        Assert.Contains(22, imported.Data["Pronouns"].RehearsingIds);
        Assert.Equal(Rehearsal(5), imported.Data["Pronouns"].Rehearsal[22]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Recovery_migrates_previous_verb_noun_and_pronoun_catalog_hashes_and_rejects_unknown_hash(bool recognized)
    {
        using var workspace = new TestWorkspace(seedGeneralWords: false);
        var paths = workspace.CreatePaths();
        var recoveryData = EmptyData();
        recoveryData["Verbs"] = new VocabSaveFile
        {
            RehearsingIds = [61],
            Rehearsal = { [61] = Rehearsal(4) }
        };
        recoveryData["Nouns"] = new VocabSaveFile
        {
            KnownIds = [348],
            RehearsingIds = [349],
            Rehearsal = { [349] = Rehearsal(5) }
        };
        recoveryData["Pronouns"] = new VocabSaveFile
        {
            RehearsingIds = [21],
            Rehearsal = { [21] = Rehearsal(2) }
        };
        var hashes = CurrentCatalogHashes(paths);
        hashes["Verbs"] = recognized ? PreviousVerbCatalogHash : "UNKNOWN";
        hashes["Nouns"] = recognized ? PreviousNounCatalogHash : "UNKNOWN";
        hashes["Pronouns"] = recognized ? PreviousPronounCatalogHash : "UNKNOWN";
        var recovery = new ProgressSnapshot { Version = ProgressStore.CurrentVersion, Data = recoveryData, CatalogHashes = hashes };
        Directory.CreateDirectory(paths.ProgressRoot);
        File.WriteAllText(Path.Combine(paths.ProgressRoot, "Progress.json"), JsonSerializer.Serialize(new
        {
            Version = ProgressStore.CurrentVersion,
            Revision = Guid.NewGuid(),
            Data = EmptyData(),
            Recovery = recovery
        }));

        var service = Sync(paths);

        if (recognized)
        {
            var preview = service.PreviewRecovery();
            service.Apply(preview);
            Assert.Equal(Rehearsal(4), Snapshot(paths).Data["Verbs"].Rehearsal[2]);
            Assert.Contains(2, new VerbData(paths).RehearsingWordIds);
            var restored = Snapshot(paths);
            Assert.Empty(restored.Data["Nouns"].KnownIds);
            Assert.Empty(restored.Data["Nouns"].RehearsingIds);
            Assert.Contains(21, restored.Data["Pronouns"].KnownIds);
            Assert.Contains(22, restored.Data["Pronouns"].RehearsingIds);
            Assert.Equal(Rehearsal(5), restored.Data["Pronouns"].Rehearsal[22]);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => service.PreviewRecovery());
        }
    }

    [Fact]
    public void Current_export_syncs_to_a_fresh_workspace_without_retired_id()
    {
        using var sourceWorkspace = new TestWorkspace(seedGeneralWords: false);
        using var targetWorkspace = new TestWorkspace(seedGeneralWords: false);
        var sourcePaths = sourceWorkspace.CreatePaths("source");
        var targetPaths = targetWorkspace.CreatePaths("target");
        WriteProgress(sourcePaths, 4, new VocabSaveFile { RehearsingIds = [61], Rehearsal = { [61] = Rehearsal(5) } });
        var source = Sync(sourcePaths);
        var exported = source.ExportSnapshot();
        var exportedSnapshot = JsonSerializer.Deserialize<ProgressSnapshot>(exported)!;

        var target = Sync(targetPaths);
        target.Apply(target.PreviewSnapshot(exported, ImportMode.Replace));

        Assert.Equal(ProgressStore.CurrentVersion, exportedSnapshot.Version);
        Assert.Equal(Rehearsal(5), Snapshot(targetPaths).Data["Verbs"].Rehearsal[2]);
        Assert.Contains(2, new VerbData(targetPaths).RehearsingWordIds);
        Assert.DoesNotContain(61, Snapshot(targetPaths).Data["Verbs"].Rehearsal.Keys);
    }

    private static void WriteProgress(RepositoryPaths paths, int version, VocabSaveFile verbs)
    {
        var data = EmptyData();
        data["Verbs"] = verbs;
        WriteProgress(paths, version, data);
    }

    private static void WriteProgress(RepositoryPaths paths, int version, Dictionary<string, VocabSaveFile> data)
    {
        if (version == 3)
        {
            Directory.CreateDirectory(paths.ProgressRoot);
            File.WriteAllText(Path.Combine(paths.ProgressRoot, ProgressStore.LegacyVersionFile), "{\"Version\":3}");
            foreach (var (name, saved) in data.Where(pair => pair.Value.KnownIds.Count + pair.Value.TrainingIds.Count +
                         pair.Value.RehearsingIds.Count + pair.Value.Rehearsal.Count > 0))
            {
                var folder = Path.Combine(paths.ProgressRoot, name);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "SavedData.json"), JsonSerializer.Serialize(saved));
            }
            return;
        }

        Directory.CreateDirectory(paths.ProgressRoot);
        File.WriteAllText(Path.Combine(paths.ProgressRoot, "Progress.json"), JsonSerializer.Serialize(new { Version = version, Revision = Guid.NewGuid(), Data = data }));
    }

    private static Dictionary<string, VocabSaveFile> EmptyData() =>
        ProgressStore.DatasetNames.ToDictionary(name => name, _ => new VocabSaveFile());

    private static Dictionary<string, string> CurrentCatalogHashes(RepositoryPaths paths) =>
        ProgressStore.DatasetNames.ToDictionary(name => name, name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Path.Combine(paths.CatalogRoot, name, name.Split('/')[^1] + ".json")))));

    private static RehearsalProgress Rehearsal(int correctDays) =>
        new() { CorrectDays = correctDays, LastCorrectDay = PracticeDay };

    private static ProgressSnapshot Snapshot(RepositoryPaths paths) =>
        JsonSerializer.Deserialize<ProgressSnapshot>(Sync(paths).ExportSnapshot())!;

    private static ProgressSyncService Sync(RepositoryPaths paths) =>
        new(paths, new WordData(paths), new VerbData(paths), new AdjectiveData(paths), new KanjiData(paths), new NounData(paths));
}
