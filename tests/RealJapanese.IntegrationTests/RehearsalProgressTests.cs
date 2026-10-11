using System.Text.Json;
using System.Text.Json.Nodes;
using DataLoaders.Models;
using RealJapanese.TestSupport;
using Repositories;
using Repositories.Sync;

namespace RealJapanese.IntegrationTests;

/// <summary>Protects rehearsal calendar credit, retry state, migration, and sync persistence.</summary>
public sealed class RehearsalProgressTests
{
    [Fact]
    public void Six_distinct_success_days_persist_and_seventh_promotes()
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var fixture = Open(workspace, "six-days", clock);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToRehearsing(word);

        foreach (var day in new[] { 0, 2, 5, 8, 11, 14 })
        {
            clock.Set(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddDays(day));
            fixture.Words.RecordPracticeAnswer(word.Id, true);
            fixture.Words.RecordPracticeAnswer(word.Id, true);
            fixture = Open(workspace, "six-days", clock);
            Assert.Equal(day switch { 0 => 1, 2 => 2, 5 => 3, 8 => 4, 11 => 5, _ => 6 }, State(fixture, word.Id).CorrectDays);
            Assert.Contains(word.Id, fixture.Words.RehearsingWordIds);
        }
        clock.Set(clock.GetUtcNow().AddDays(2));
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Contains(word.Id, fixture.Words.VocabWordIds);
        Assert.False(StateMap(fixture).ContainsKey(word.Id));
    }

    [Fact]
    public void Wrong_answer_resets_before_or_after_credit_and_miss_day_cannot_earn_credit()
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var fixture = Open(workspace, "miss-reset", clock);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToRehearsing(word);
        fixture.Words.RecordPracticeAnswer(word.Id, false);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(0, State(fixture, word.Id).CorrectDays);

        clock.Set(clock.GetUtcNow().AddDays(1));
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(1, State(fixture, word.Id).CorrectDays);
        fixture.Words.RecordPracticeAnswer(word.Id, false);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(0, State(fixture, word.Id).CorrectDays);
        Assert.Equal(DateOnly.FromDateTime(clock.GetLocalNow().DateTime), State(fixture, word.Id).LastResetDay);

        clock.Set(clock.GetUtcNow().AddDays(1));
        fixture = Open(workspace, "miss-reset", clock);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(1, State(fixture, word.Id).CorrectDays);
        for (var day = 0; day < 6; day++)
        {
            clock.Set(clock.GetUtcNow().AddDays(1));
            fixture.Words.RecordPracticeAnswer(word.Id, true);
        }
        Assert.Contains(word.Id, fixture.Words.VocabWordIds);
    }

    [Fact]
    public void Known_miss_demotes_immediately_and_persists_without_a_retry()
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var fixture = Open(workspace, "known-retry", clock);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToVocab(word);
        Assert.True(fixture.Words.RecordPracticeAnswer(word.Id, false));
        fixture = Open(workspace, "known-retry", clock);
        Assert.DoesNotContain(word.Id, fixture.Words.VocabWordIds);
        Assert.Contains(word.Id, fixture.Words.RehearsingWordIds);
        Assert.False(State(fixture, word.Id).AwaitingRetry);
        Assert.Equal(0, State(fixture, word.Id).CorrectDays);
        Assert.Equal(new DateOnly(2026, 3, 1), State(fixture, word.Id).LastResetDay);
        Assert.False(fixture.Words.RecordPracticeAnswer(word.Id, false));
        Assert.False(fixture.Words.RecordPracticeAnswer(word.Id, true));
        Assert.Equal(0, State(fixture, word.Id).CorrectDays);

        clock.Set(clock.GetUtcNow().AddDays(1));
        fixture = Open(workspace, "known-retry", clock);
        Assert.False(fixture.Words.RecordPracticeAnswer(word.Id, true));
        Assert.Equal(1, State(fixture, word.Id).CorrectDays);
        Assert.Contains(word.Id, fixture.Words.RehearsingWordIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Older_saved_pending_known_miss_still_demotes_on_the_next_answer(bool correct)
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var fixture = Open(workspace, "legacy-retry", clock);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToVocab(word);
        var path = Path.Combine(fixture.Paths.ProgressRoot, "Progress.json");
        var saved = JsonNode.Parse(File.ReadAllBytes(path))!;
        saved["Data"]!["Words"]!["Rehearsal"]![word.Id.ToString()] = JsonSerializer.SerializeToNode(
            new RehearsalProgress { LastResetDay = new DateOnly(2026, 3, 1), AwaitingRetry = true });
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(saved));

        fixture = Open(workspace, "legacy-retry", clock);
        Assert.True(fixture.Words.RecordPracticeAnswer(word.Id, correct));
        fixture = Open(workspace, "legacy-retry", clock);
        Assert.DoesNotContain(word.Id, fixture.Words.VocabWordIds);
        Assert.Contains(word.Id, fixture.Words.RehearsingWordIds);
        Assert.False(State(fixture, word.Id).AwaitingRetry);
        Assert.Equal(0, State(fixture, word.Id).CorrectDays);
    }

    [Fact]
    public void Training_unselected_and_unknown_answers_do_not_change_progress()
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var fixture = Open(workspace, "eligibility", clock);
        var words = fixture.Words.Words.Take(3).ToArray();
        fixture.Words.AddToTraining(words[0]);
        fixture.Words.AddToRehearsing(words[1]);
        var path = Path.Combine(fixture.Paths.ProgressRoot, "Progress.json");
        var before = File.ReadAllBytes(path);
        fixture.Words.RecordPracticeAnswer(words[0].Id, true);
        fixture.Words.RecordPracticeAnswer(words[0].Id, false);
        fixture.Words.RecordPracticeAnswer(words[2].Id, false);
        fixture.Words.RecordPracticeAnswer(words[2].Id, true);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Throws<ArgumentException>(() => fixture.Words.RecordPracticeAnswer(int.MaxValue, true));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Empty(StateMap(fixture));
    }

    [Fact]
    public void Category_assignment_preserves_only_same_category_and_promoted_item_demotes_immediately_on_miss()
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var fixture = Open(workspace, "category-history", clock);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToRehearsing(word);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        fixture.Words.AddToRehearsing(word);
        Assert.Equal(1, State(fixture, word.Id).CorrectDays);
        fixture.Words.AddToTraining(word);
        Assert.False(StateMap(fixture).ContainsKey(word.Id));
        fixture.Words.AddToRehearsing(word);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        fixture.Words.RemoveFromRehearsing(word);
        Assert.False(StateMap(fixture).ContainsKey(word.Id));
        fixture.Words.AddToRehearsing(word);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        for (var day = 1; day < 7; day++)
        {
            clock.Set(clock.GetUtcNow().AddDays(1));
            fixture.Words.RecordPracticeAnswer(word.Id, true);
        }
        Assert.Contains(word.Id, fixture.Words.VocabWordIds);
        fixture.Words.RecordPracticeAnswer(word.Id, false);
        fixture = Open(workspace, "category-history", clock);
        Assert.DoesNotContain(word.Id, fixture.Words.VocabWordIds);
        Assert.Contains(word.Id, fixture.Words.RehearsingWordIds);
        Assert.False(State(fixture, word.Id).AwaitingRetry);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(0, State(fixture, word.Id).CorrectDays);
    }

    [Fact]
    public void Rehearsal_day_uses_provider_local_timezone_across_midnight()
    {
        using var workspace = new TestWorkspace();
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestPlusTwo", TimeSpan.FromHours(2), "TestPlusTwo", "TestPlusTwo");
        var clock = new MutableClock(new DateTimeOffset(2026, 6, 1, 21, 30, 0, TimeSpan.Zero), zone);
        var fixture = Open(workspace, "local-midnight", clock);
        var word = fixture.Words.Words.First();
        fixture.Words.AddToRehearsing(word);
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(new DateOnly(2026, 6, 1), State(fixture, word.Id).LastCorrectDay);
        clock.Set(clock.GetUtcNow().AddHours(3));
        fixture.Words.RecordPracticeAnswer(word.Id, true);
        Assert.Equal(2, State(fixture, word.Id).CorrectDays);
    }

    [Fact]
    public void Sync_replace_recovery_and_merges_keep_rehearsal_history_with_its_category()
    {
        using var workspace = new TestWorkspace();
        var clock = new MutableClock(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var incoming = Open(workspace, "sync-incoming", clock);
        var word = incoming.Words.Words.First();
        incoming.Words.AddToRehearsing(word);
        incoming.Words.RecordPracticeAnswer(word.Id, false);
        clock.Set(clock.GetUtcNow().AddDays(1));
        incoming.Words.RecordPracticeAnswer(word.Id, true);
        var local = Open(workspace, "sync-local", clock);
        var localWord = local.Words.Words.First();
        local.Words.AddToRehearsing(localWord);
        local.Words.RecordPracticeAnswer(localWord.Id, true);
        clock.Set(clock.GetUtcNow().AddDays(1));
        local.Words.RecordPracticeAnswer(localWord.Id, true);

        local.Service.Apply(local.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), ImportMode.Replace));
        local = Open(workspace, "sync-local", clock);
        Assert.Equal(1, State(local, word.Id).CorrectDays);
        Assert.Equal(new DateOnly(2026, 7, 1), State(local, word.Id).LastResetDay);
        local.Service.Apply(local.Service.PreviewRecovery());
        local = Open(workspace, "sync-local", clock);
        Assert.Equal(2, State(local, word.Id).CorrectDays);
        Assert.Equal(new DateOnly(2026, 7, 3), State(local, word.Id).LastCorrectDay);

        foreach (var mode in new[] { ImportMode.MergeKeepLocal, ImportMode.MergeUseIncoming })
        {
            var target = Open(workspace, "sync-local", clock);
            var preview = target.Service.PreviewSnapshot(incoming.Service.ExportSnapshot(), mode);
            var row = Assert.Single(preview.Summary, item => item.Dataset == "Words");
            Assert.Equal(mode == ImportMode.MergeKeepLocal ? 0 : 1, row.Changed);
            Assert.Equal(1, row.Conflicts);
            target.Service.Apply(preview);
            target = Open(workspace, "sync-local", clock);
            Assert.Equal(mode == ImportMode.MergeKeepLocal ? 2 : 1, State(target, word.Id).CorrectDays);
            Assert.Contains(word.Id, target.Words.RehearsingWordIds);
        }
    }

    [Fact]
    public void Version_three_progress_and_recovery_upgrade_without_rehearsal_credit()
    {
        using var workspace = new TestWorkspace();
        var path = Path.Combine(workspace.Root, "v3", "Progress.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var data = EmptyData();
        data["Words"].RehearsingIds.Add(1000);
        var v3 = JsonSerializer.SerializeToNode(new
        {
            Version = 3,
            Revision = Guid.NewGuid(),
            Data = data,
            Recovery = new { Version = 3, CreatedUtc = DateTimeOffset.UtcNow, CatalogHashes = new Dictionary<string, string>(), Data = data }
        })!.AsObject();
        foreach (var dataset in ProgressStore.DatasetNames)
        {
            v3["Data"]![dataset]!.AsObject().Remove("Rehearsal");
            v3["Recovery"]!["Data"]![dataset]!.AsObject().Remove("Rehearsal");
        }
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(v3));

        var fixture = OpenAt(workspace, Path.GetDirectoryName(path)!, new MutableClock(DateTimeOffset.UtcNow, TimeZoneInfo.Utc));
        Assert.Empty(StateMap(fixture));
        fixture.Words.RecordPracticeAnswer(1000, true);
        var upgraded = JsonNode.Parse(File.ReadAllBytes(path))!;
        Assert.Equal(4, (int)upgraded["Version"]!);
        Assert.Equal(1, (int)upgraded["Data"]!["Words"]!["Rehearsal"]!["1000"]!["CorrectDays"]!);
        Assert.Equal(4, (int)upgraded["Recovery"]!["Version"]!);
    }

    [Theory]
    [InlineData("orphan")]
    [InlineData("day-seven")]
    [InlineData("missing-field")]
    public void Invalid_incoming_rehearsal_history_is_rejected_without_write(string scenario)
    {
        using var workspace = new TestWorkspace();
        var local = Open(workspace, "invalid-history-local", new MutableClock(DateTimeOffset.UtcNow, TimeZoneInfo.Utc));
        local.Words.AddToRehearsing(local.Words.Words.First());
        local.Words.RecordPracticeAnswer(local.Words.Words.First().Id, true);
        var bytes = local.Service.ExportSnapshot();
        var root = JsonNode.Parse(bytes)!.AsObject();
        var words = root["Data"]!["Words"]!.AsObject();
        switch (scenario)
        {
            case "orphan": words["Rehearsal"]!["999999"] = JsonSerializer.SerializeToNode(new RehearsalProgress { CorrectDays = 1, LastCorrectDay = new DateOnly(2026, 1, 1) }); break;
            case "day-seven": words["Rehearsal"]!["1000"] = JsonSerializer.SerializeToNode(new RehearsalProgress { CorrectDays = 7, LastCorrectDay = new DateOnly(2026, 1, 1) }); break;
            case "missing-field": words["Rehearsal"]!["1000"]!.AsObject().Remove("AwaitingRetry"); break;
        }
        var before = File.ReadAllBytes(Path.Combine(local.Paths.ProgressRoot, "Progress.json"));
        Assert.Throws<InvalidDataException>(() => local.Service.PreviewSnapshot(JsonSerializer.SerializeToUtf8Bytes(root), ImportMode.Replace));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(local.Paths.ProgressRoot, "Progress.json")));
    }

    private static Fixture Open(TestWorkspace workspace, string name, TimeProvider clock) => OpenAt(workspace, workspace.CreatePaths(name).ProgressRoot, clock);
    private static Fixture OpenAt(TestWorkspace workspace, string progressRoot, TimeProvider clock)
    {
        var paths = new RepositoryPaths(workspace.CatalogRoot, progressRoot, clock);
        var words = new WordData(paths); var verbs = new VerbData(paths); var adjectives = new AdjectiveData(paths);
        var kanji = new KanjiData(paths); var nouns = new NounData(paths);
        return new(paths, words, new ProgressSyncService(paths, words, verbs, adjectives, kanji, nouns));
    }

    private static Dictionary<int, RehearsalProgress> StateMap(Fixture fixture) =>
        JsonSerializer.Deserialize<ProgressSnapshot>(fixture.Service.ExportSnapshot())!.Data["Words"].Rehearsal;
    private static RehearsalProgress State(Fixture fixture, int id) => StateMap(fixture)[id];
    private static Dictionary<string, VocabSaveFile> EmptyData() => ProgressStore.DatasetNames.ToDictionary(name => name, _ => new VocabSaveFile());

    private sealed record Fixture(RepositoryPaths Paths, WordData Words, ProgressSyncService Service);

    private sealed class MutableClock(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
    {
        private DateTimeOffset now = utcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => zone;
        public void Set(DateTimeOffset value) => now = value;
    }
}
