using System.Security.Cryptography;
using System.Text.Json;
using AudioStudio;

namespace RealJapanese.Tests;

/// <summary>Verifies standalone audio catalog identity and export persistence without loading a TTS engine.</summary>
public sealed class AudioStudioTests
{
    [Theory]
    [InlineData("～か～", "～か～", "か", "か")]
    [InlineData("〜時間", "〜じかん", "時間", "じかん")]
    [InlineData("Tシャツ", "Tシャツ", "Tシャツ", "ティーシャツ")]
    public void SpeechReadings_AdaptPlaceholdersWithoutChangingCatalog(string text, string kana, string spokenText, string spokenKana)
    {
        var word = new CatalogWord("Words", 0, "Words & expressions", text, kana, "example");
        Assert.Equal(spokenText, word.SpokenJapanese);
        Assert.Equal(spokenKana, word.SpokenKana);
        Assert.Equal(text, word.Japanese);
        Assert.Equal(kana, word.Kana);
        Assert.Equal("Text matches", WordCatalog.ValidateTranscript(word, spokenKana, null));
    }

    [Fact]
    public void Catalog_MapsEveryDatasetAndKeepsIdentityCompositeAcrossGroups()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(temp.Path);

        Assert.Equal(12, WordCatalog.Datasets.Length);
        Assert.Equal(4, catalog.Words.Select(w => w.Group).Distinct().Count());
        Assert.Equal("Nouns", catalog.Get("Nouns/7").Group);
        Assert.Equal("Verbs", catalog.Get("Verbs/7").Group);
        Assert.Equal("Words & expressions", catalog.Get("Expressions/7").Group);
        Assert.NotEqual(catalog.Get("Nouns/7"), catalog.Get("Verbs/7"));
        Assert.Equal(WordCatalog.Datasets.Length, catalog.Words.Select(w => w.Dataset).Distinct().Count());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("duplicate")]
    public void Catalog_RejectsMissingNegativeOrDuplicateIds(string failure)
    {
        using var temp = new TempDirectory();
        var rows = failure switch
        {
            "missing" => "[{\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"}]",
            "negative" => "[{\"id\":-1,\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"}]",
            _ => "[{\"id\":4,\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"},{\"id\":4,\"japanese\":\"犬\",\"kana\":\"いぬ\",\"english\":\"dog\"}]"
        };
        WriteCatalogFiles(temp.Path, nounsJson: rows);

        if (failure == "missing")
            Assert.Throws<KeyNotFoundException>(() => new WordCatalog(temp.Path));
        else if (failure == "negative")
            Assert.Throws<InvalidDataException>(() => new WordCatalog(temp.Path));
        else
            Assert.Throws<ArgumentException>(() => new WordCatalog(temp.Path));
    }

    [Theory]
    [InlineData("コーヒー。", "こーひー")]
    [InlineData("ねこ、", "ねこ")]
    public void NormalizeReading_EquatesKatakanaAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, WordCatalog.NormalizeReading(input));
    }

    [Fact]
    public void ValidateTranscript_PreservesProlongedVowelDifference()
    {
        var word = new CatalogWord("Nouns", 7, "Nouns", "コーヒー", "こーひー", "coffee");

        Assert.Equal("Text matches", WordCatalog.ValidateTranscript(word, "コーヒー。", null));
        Assert.Equal("Review reading", WordCatalog.ValidateTranscript(word, "こひ", null));
    }

    [Fact]
    public void Selection_PersistsAfterStoreReconstruction()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7")], ["bert"]);

        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });
        var reopened = CreateStore(temp.Path, catalog);

        Assert.Equal("bert", reopened.GetRun(run.Id).Selections["Nouns/7"]);
    }

    [Fact]
    public void DontUse_IsAChoiceAndDoesNotRequireUnselectedAcknowledgement()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7")], ["bert"]);
        store.Select(run.Id, new() { ["Nouns/7"] = "dont-use" });

        var result = store.Export(run.Id, acknowledgeUnselected: false);

        Assert.Equal(new ExportResult(0, 0), result);
        Assert.Empty(store.Exports());
        Assert.False(File.Exists(Path.Combine(temp.Path, "exports", "manifest.json")));
    }

    [Fact]
    public void Export_RequiresAcknowledgementForUnselectedWordsAndWritesNothing()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert"]);
        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });

        var error = Assert.Throws<StudioException>(() => store.Export(run.Id, acknowledgeUnselected: false));

        Assert.Equal(409, error.Status);
        Assert.Equal(1, error.UnselectedCount);
        Assert.False(File.Exists(Path.Combine(temp.Path, "exports", "manifest.json")));
        string exportRoot = Path.Combine(temp.Path, "exports");
        Assert.True(!Directory.Exists(exportRoot) || !Directory.EnumerateFiles(exportRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void Export_WithAcknowledgementCopiesSelectedBytesAndManifestWithoutChangingData()
    {
        using var temp = new TempDirectory();
        var data = Path.Combine(temp.Path, "data");
        var catalog = CreateCatalog(data);
        var catalogBefore = File.ReadAllBytes(Path.Combine(data, "Nouns", "Nouns.json"));
        var progress = Path.Combine(data, "Progress.json");
        File.WriteAllText(progress, "progress sentinel");
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert"]);
        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });

        var result = store.Export(run.Id, acknowledgeUnselected: true);

        var exported = Assert.Single(store.Exports());
        Assert.Equal(new ExportResult(1, 1), result);
        Assert.Equal("Nouns/7", exported.Word.Key);
        Assert.Equal("bert", exported.Engine);
        Assert.Equal(run.Id, exported.RunId);
        Assert.Equal("OriginalWav", exported.OutputQuality);
        Assert.Equal("recognized", exported.Transcript);
        Assert.Equal("Text matches", exported.Validation);
        Assert.Equal("audio-Nouns-7", File.ReadAllText(store.ExportAudio("Nouns/7")));
        Assert.Equal(catalogBefore, File.ReadAllBytes(Path.Combine(data, "Nouns", "Nouns.json")));
        Assert.Equal("progress sentinel", File.ReadAllText(progress));
    }

    [Fact]
    public void Export_SeparatesSameNumericIdFromDifferentDatasets()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert"]);
        store.Select(run.Id, new() { ["Nouns/7"] = "bert", ["Verbs/7"] = "bert" });

        var result = store.Export(run.Id, acknowledgeUnselected: false);

        Assert.Equal(2, result.ExportedCount);
        Assert.Equal(new[] { "Nouns/7", "Verbs/7" }, store.Exports().Select(x => x.Word.Key).Order().ToArray());
        Assert.Equal("audio-Nouns-7", File.ReadAllText(store.ExportAudio("Nouns/7")));
        Assert.Equal("audio-Verbs-7", File.ReadAllText(store.ExportAudio("Verbs/7")));
        Assert.NotEqual(store.Exports()[0].File, store.Exports()[1].File);
    }

    [Fact]
    public void Export_ReplacesSelectedVoiceAndPreservesOtherWords()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var first = CreateRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert"]);
        store.Select(first.Id, new() { ["Nouns/7"] = "bert", ["Verbs/7"] = "bert" });
        store.Export(first.Id, acknowledgeUnselected: false);
        var second = CreateRun(store, [catalog.Get("Nouns/7")], ["fish"]);
        store.Select(second.Id, new() { ["Nouns/7"] = "fish" });

        store.Export(second.Id, acknowledgeUnselected: false);

        var exports = store.Exports().ToDictionary(e => e.Word.Key);
        Assert.Equal(2, exports.Count);
        Assert.Equal("fish", exports["Nouns/7"].Engine);
        Assert.Equal(second.Id, exports["Nouns/7"].RunId);
        Assert.Equal("bert", exports["Verbs/7"].Engine);
        Assert.Equal(first.Id, exports["Verbs/7"].RunId);
        Assert.Equal("audio-Nouns-7-fish", File.ReadAllText(store.ExportAudio("Nouns/7")));
        Assert.Equal("audio-Verbs-7", File.ReadAllText(store.ExportAudio("Verbs/7")));
    }

    [Fact]
    public void SetQuality_PersistsIndependentlyForEachWordAndVoice()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateMultiFormatRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert", "fish"]);

        store.SetQuality(run.Id, "Nouns/7", "bert", "Mp3Mono64Kbps");
        store.SetQuality(run.Id, "Nouns/7", "fish", "OpusMono32Kbps");
        store.SetQuality(run.Id, "Verbs/7", "bert", "Wav24000Hz16BitMono");

        var reopened = CreateStore(temp.Path, catalog).GetRun(run.Id);
        Assert.Equal("Mp3Mono64Kbps", reopened.Clips.Single(c => c.Key == "Nouns/7" && c.Engine == "bert").SelectedQuality);
        Assert.Equal("OpusMono32Kbps", reopened.Clips.Single(c => c.Key == "Nouns/7" && c.Engine == "fish").SelectedQuality);
        Assert.Equal("Wav24000Hz16BitMono", reopened.Clips.Single(c => c.Key == "Verbs/7" && c.Engine == "bert").SelectedQuality);
        Assert.Null(reopened.Clips.Single(c => c.Key == "Verbs/7" && c.Engine == "fish").SelectedQuality);
    }

    [Fact]
    public void Export_UsesSelectedVoiceQualityBytesAndVariantMetadata()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateMultiFormatRun(store, [catalog.Get("Nouns/7")], ["bert", "fish"]);
        store.SetQuality(run.Id, "Nouns/7", "fish", "Mp3Mono64Kbps");
        store.Select(run.Id, new() { ["Nouns/7"] = "fish" });

        var result = store.Export(run.Id, acknowledgeUnselected: false);

        var exported = Assert.Single(store.Exports());
        Assert.Equal(1, result.ExportedCount);
        Assert.Equal("fish", exported.Engine);
        Assert.Equal("Mp3Mono64Kbps", exported.OutputQuality);
        Assert.Equal("transcript-fish-Mp3Mono64Kbps", exported.Transcript);
        Assert.Equal("Text matches", exported.Validation);
        Assert.Equal("bytes-fish-Nouns-7-Mp3Mono64Kbps", File.ReadAllText(store.ExportAudio("Nouns/7")));
    }

    [Fact]
    public void SetQuality_RejectsUnknownOrUnavailableQualityWithoutChangingSelection()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7")], ["bert"]); // Legacy run has only its original quality.

        Assert.Throws<StudioException>(() => store.SetQuality(run.Id, "Nouns/7", "bert", "wav"));
        Assert.Throws<StudioException>(() => store.SetQuality(run.Id, "Nouns/7", "bert", "Mp3Mono64Kbps"));

        Assert.Null(store.GetRun(run.Id).Clips.Single().SelectedQuality);
    }

    [Fact]
    public void SetQuality_IgnoresTamperedNonselectedFormatAndExportUsesSelectedFormat()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateMultiFormatRun(store, [catalog.Get("Nouns/7")], ["bert"]);
        store.SetQuality(run.Id, "Nouns/7", "bert", "Mp3Mono64Kbps");
        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });
        var clip = store.GetRun(run.Id).Clips.Single();
        string nonselected = StudioStore.SafePath(store.RunFolder(run.Id), clip.Variants["OriginalWav"].File);
        File.WriteAllText(nonselected, "modified unselected format");

        var updated = store.SetQuality(run.Id, "Nouns/7", "bert", "Mp3Mono64Kbps");
        store.Export(run.Id, acknowledgeUnselected: false);

        Assert.Equal("Mp3Mono64Kbps", updated.Clips.Single().SelectedQuality);
        Assert.Equal("bytes-bert-Nouns-7-Mp3Mono64Kbps", File.ReadAllText(store.ExportAudio("Nouns/7")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_RejectsMissingOrTamperedSelectedQualityBeforeWritingAnything(bool tamper)
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateMultiFormatRun(store, [catalog.Get("Nouns/7")], ["bert"]);
        store.SetQuality(run.Id, "Nouns/7", "bert", "Mp3Mono64Kbps");
        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });
        var variant = store.GetRun(run.Id).Clips.Single().Variants["Mp3Mono64Kbps"];
        string path = StudioStore.SafePath(store.RunFolder(run.Id), variant.File);
        if (tamper) File.WriteAllText(path, "modified selected format"); else File.Delete(path);

        var error = Assert.Throws<StudioException>(() => store.Export(run.Id, acknowledgeUnselected: false));

        Assert.Equal(409, error.Status);
        Assert.False(File.Exists(Path.Combine(temp.Path, "exports", "manifest.json")));
        string exportRoot = Path.Combine(temp.Path, "exports");
        Assert.True(!Directory.Exists(exportRoot) || !Directory.EnumerateFiles(exportRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void Select_RejectsInvalidChoiceWithoutPartiallyChangingBatch()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert"]);

        Assert.Throws<StudioException>(() => store.Select(run.Id, new()
        {
            ["Nouns/7"] = "bert",
            ["Verbs/7"] = "fish"
        }));

        Assert.Empty(store.GetRun(run.Id).Selections);
        Assert.Empty(CreateStore(temp.Path, catalog).GetRun(run.Id).Selections);
    }

    [Fact]
    public void Export_RejectsTraversalAndWritesNothing()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7")], ["bert"]);
        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });
        store.UpdateRun(run.Id, current => current.Clips.Single().File = "../../outside.wav");

        Assert.Throws<ArgumentException>(() => store.Export(run.Id, acknowledgeUnselected: false));
        Assert.False(File.Exists(Path.Combine(temp.Path, "exports", "manifest.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_RejectsMissingOrTamperedAudioBeforeWritingAnything(bool tamper)
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7")], ["bert"]);
        store.Select(run.Id, new() { ["Nouns/7"] = "bert" });
        var source = Path.Combine(store.RunFolder(run.Id), run.Clips.Single().File!);
        if (tamper) File.WriteAllText(source, "modified audio"); else File.Delete(source);

        var error = Assert.Throws<StudioException>(() => store.Export(run.Id, acknowledgeUnselected: false));

        Assert.Equal(409, error.Status);
        Assert.False(File.Exists(Path.Combine(temp.Path, "exports", "manifest.json")));
        string exportRoot = Path.Combine(temp.Path, "exports");
        Assert.True(!Directory.Exists(exportRoot) || !Directory.EnumerateFiles(exportRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void Reopen_MarksActiveRunInterruptedAndKeepsCompletedClips()
    {
        using var temp = new TempDirectory();
        var catalog = CreateCatalog(Path.Combine(temp.Path, "data"));
        var store = CreateStore(temp.Path, catalog);
        var run = CreateRun(store, [catalog.Get("Nouns/7"), catalog.Get("Verbs/7")], ["bert"]);
        store.UpdateRun(run.Id, current =>
        {
            current.Status = "Generating";
            current.Clips[1].Status = "Generating";
        });

        var reopened = CreateStore(temp.Path, catalog).GetRun(run.Id);

        Assert.Equal("Interrupted", reopened.Status);
        Assert.Equal("Interrupted by application shutdown", reopened.Phase);
        Assert.Equal("Completed", reopened.Clips[0].Status);
        Assert.Equal("Pending", reopened.Clips[1].Status);
        Assert.Equal(run.Clips[0].Sha256, reopened.Clips[0].Sha256);
    }

    private static WordCatalog CreateCatalog(string dataRoot)
    {
        WriteCatalogFiles(dataRoot);
        return new WordCatalog(dataRoot);
    }

    private static void WriteCatalogFiles(string dataRoot, string? nounsJson = null)
    {
        foreach (string dataset in WordCatalog.Datasets)
        {
            string folder = Path.Combine(dataRoot, dataset);
            Directory.CreateDirectory(folder);
            string json = dataset switch
            {
                "Nouns" => nounsJson ?? "[{\"id\":7,\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"}]",
                "Verbs" => "[{\"id\":7,\"japanese\":\"見る\",\"kana\":\"みる\",\"english\":\"to see\"}]",
                "Expressions" => "[{\"id\":7,\"japanese\":\"ありがとう\",\"kana\":\"ありがとう\",\"english\":\"thank you\"}]",
                _ => "[{\"id\":7,\"japanese\":\"語\",\"kana\":\"ご\",\"english\":\"word\"}]"
            };
            File.WriteAllText(Path.Combine(folder, dataset + ".json"), json);
        }
    }

    private static StudioStore CreateStore(string root, WordCatalog catalog) =>
        new(Path.Combine(root, "work"), Path.Combine(root, "exports"), catalog);

    private static StudioRun CreateRun(StudioStore store, List<CatalogWord> words, string[] engines)
    {
        string id = Guid.NewGuid().ToString("N");
        string runFolder = store.RunFolder(id);
        var clips = new List<StudioClip>();
        foreach (var word in words)
        foreach (string engine in engines)
        {
            string relative = Path.Combine(engine, word.Dataset + "-" + word.Id + ".wav");
            string rawRelative = Path.Combine(engine, "raw", word.Dataset + "-" + word.Id + ".wav");
            string path = StudioStore.SafePath(runFolder, relative);
            string rawPath = StudioStore.SafePath(runFolder, rawRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!);
            string contents = $"audio-{word.Dataset}-{word.Id}{(engine == "fish" ? "-fish" : engine == "irodori" ? "-irodori" : "")}";
            File.WriteAllText(path, contents);
            File.WriteAllText(rawPath, "raw-" + contents);
            clips.Add(new StudioClip
            {
                Key = word.Key, Engine = engine, Status = "Completed", File = relative, RawFile = rawRelative,
                Sha256 = StudioStore.Hash(path), Transcript = "recognized", Validation = "Text matches"
            });
        }
        var run = new StudioRun
        {
            Id = id, Status = "Completed", Phase = "Complete", Words = words, Engines = engines,
            OutputQuality = "OriginalWav", Clips = clips
        };
        store.AddRun(run);
        return run;
    }

    private static StudioRun CreateMultiFormatRun(StudioStore store, List<CatalogWord> words, string[] engines)
    {
        var run = CreateRun(store, words, engines);
        store.UpdateRun(run.Id, saved =>
        {
            foreach (var clip in saved.Clips)
            {
                var variants = new Dictionary<string, AudioVariant>();
                foreach (string quality in ClipFormats.Qualities)
                {
                    string relative = Path.Combine("formats", clip.Engine, clip.Key.Replace('/', '-'), quality + ClipFormats.Extension(quality));
                    string path = StudioStore.SafePath(store.RunFolder(run.Id), relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    string bytes = $"bytes-{clip.Engine}-{clip.Key.Replace('/', '-')}-{quality}";
                    File.WriteAllText(path, bytes);
                    variants[quality] = new AudioVariant
                    {
                        File = relative,
                        Sha256 = StudioStore.Hash(path),
                        Seconds = 2.5,
                        Transcript = $"transcript-{clip.Engine}-{quality}",
                        Validation = "Text matches",
                        TranscriptSource = "fixture transcript"
                    };
                }
                clip.Variants = variants;
                clip.SelectedQuality = null;
            }
        });
        return store.GetRun(run.Id);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
    }
}
