using AudioStudio;
using GenerateAudioUsingTTS;

namespace RealJapanese.Tests;

/// <summary>Checks model reference audio/transcript pairing, persisted snapshots and legacy run compatibility without loading models.</summary>
public sealed class RunReferenceTests
{
    [Fact]
    public void Prepare_SavesDistinctAudioAndTranscriptsAndRetainsThemAfterRestart()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(temp.Path);
        var store = new StudioStore(Path.Combine(temp.Path, "work"), Path.Combine(temp.Path, "exports"), catalog);
        var fish = SaveReference(store, "Fish voice", "魚の参照", "fish audio");
        var irodori = SaveReference(store, "Irodori voice", "彩りの参照", "irodori audio");
        var run = Run(new() { ["fish"] = fish, ["irodori"] = irodori });
        RunReferences.Prepare(run, store);
        store.AddRun(run);

        File.WriteAllText(store.ReferenceAudio(fish.Id), "changed source");
        store.SaveReference(fish with { Transcript = "changed transcript" });
        File.Delete(store.ReferenceAudio(irodori.Id));
        var reopened = new StudioStore(Path.Combine(temp.Path, "work"), Path.Combine(temp.Path, "exports"), catalog);
        var saved = reopened.GetRun(run.Id);
        RunReferences.Prepare(saved, reopened);

        Assert.Equal("fish audio", ReadAudio(saved, reopened, "fish"));
        Assert.Equal("irodori audio", ReadAudio(saved, reopened, "irodori"));
        Assert.Equal(fish, saved.ReferenceFor("fish"));
        Assert.Equal(irodori, saved.ReferenceFor("irodori"));
        Assert.Equal(fish.Transcript, RunReferences.SettingsFor(saved, "fish", Settings()).ReferenceTranscript);
        Assert.Equal(irodori.Transcript, RunReferences.SettingsFor(saved, "irodori", Settings()).ReferenceTranscript);
        Assert.Null(RunReferences.AudioFile(saved, "bert"));
        Assert.Null(RunReferences.SettingsFor(saved, "bert", Settings()).ReferenceTranscript);
    }

    [Fact]
    public void ExplicitNoReference_DoesNotInheritLegacyOrConfiguredTranscript()
    {
        var legacy = Reference("Legacy", "legacy transcript");
        var run = new StudioRun
        {
            Words = [], Engines = ["fish", "irodori"], OutputQuality = "OriginalWav",
            ReferenceId = legacy.Id, ReferenceSnapshot = legacy,
            EngineReferences = new() { ["fish"] = null, ["irodori"] = Reference("Irodori", "") },
            SpeechSettings = Settings() with { Seed = 123 }
        };

        Assert.Null(run.ReferenceFor("fish"));
        Assert.Null(RunReferences.AudioFile(run, "fish"));
        Assert.Null(RunReferences.SettingsFor(run, "fish", Settings()).ReferenceTranscript);
        Assert.Equal(123, RunReferences.SettingsFor(run, "fish", Settings()).Seed);
        Assert.Equal("", RunReferences.SettingsFor(run, "irodori", Settings()).ReferenceTranscript);
        Assert.Null(run.ReferenceFor("bert"));
    }

    [Fact]
    public void LegacyRun_UsesExistingSharedAudioAndSnapshotForBothConditionedModels()
    {
        using var temp = new TempDirectory();
        var store = new StudioStore(Path.Combine(temp.Path, "work"), Path.Combine(temp.Path, "exports"),
            CreateCatalog(temp.Path));
        var reference = Reference("Original shared reference", "保存した参照");
        var run = new StudioRun
        {
            Words = [], Engines = ["bert", "fish", "irodori"], OutputQuality = "OriginalWav",
            ReferenceId = reference.Id, ReferenceName = reference.Name, ReferenceSnapshot = reference,
            SpeechSettings = Settings()
        };
        Directory.CreateDirectory(store.RunFolder(run.Id));
        File.WriteAllText(Path.Combine(store.RunFolder(run.Id), "reference.wav"), "original shared audio");
        store.AddRun(run);
        var saved = store.GetRun(run.Id);
        RunReferences.Prepare(saved, store);

        foreach (string engine in new[] { "fish", "irodori" })
        {
            Assert.Equal("reference.wav", RunReferences.AudioFile(saved, engine));
            Assert.Equal("original shared audio", ReadAudio(saved, store, engine));
            Assert.Equal(reference.Transcript, RunReferences.SettingsFor(saved, engine, Settings()).ReferenceTranscript);
        }
        Assert.Null(RunReferences.AudioFile(saved, "bert"));
        Assert.Null(saved.ReferenceFor("bert"));
    }

    private static StudioRun Run(Dictionary<string, ReferenceItem?> references) => new()
    {
        Words = [], Engines = ["bert", "fish", "irodori"], OutputQuality = "OriginalWav",
        EngineReferences = references, SpeechSettings = Settings()
    };

    private static WordCatalog CreateCatalog(string root)
    {
        string data = Path.Combine(root, "data");
        foreach (string dataset in WordCatalog.Datasets)
        {
            string folder = Path.Combine(data, dataset);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, dataset + ".json"), "[]");
        }
        return new WordCatalog(data);
    }

    private static ReferenceItem Reference(string name, string transcript) =>
        new(Guid.NewGuid().ToString("N"), name, transcript, "User edited", 2, "source.wav", DateTimeOffset.UtcNow);

    private static ReferenceItem SaveReference(StudioStore store, string name, string transcript, string audio)
    {
        var reference = Reference(name, transcript);
        store.SaveReference(reference);
        File.WriteAllText(store.ReferenceAudio(reference.Id), audio);
        return reference;
    }

    private static string ReadAudio(StudioRun run, StudioStore store, string engine) =>
        File.ReadAllText(StudioStore.SafePath(store.RunFolder(run.Id), RunReferences.AudioFile(run, engine)!));

    private static Settings Settings() => new()
    {
        LibraryRoot = "unused", ModelsDirectory = "unused", BertPython = "unused", FishPython = "unused",
        IrodoriPython = "unused", Ffmpeg = "unused", ReferenceAudio = "unused", ReferenceTranscript = "configured transcript"
    };

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RealJapanese-RunReferences-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
