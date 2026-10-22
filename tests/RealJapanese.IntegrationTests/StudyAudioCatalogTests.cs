using System.Text;
using System.Text.Json;
using Repositories;
using Repositories.Audio;

namespace RealJapanese.IntegrationTests;

/// <summary>Checks manifest parsing, snapshot matching and bounded local audio loading.</summary>
public sealed class StudyAudioCatalogTests
{
    [Fact]
    public void Missing_manifest_produces_empty_catalog_without_error()
    {
        using var fixture = new AudioFixture();

        var catalog = fixture.Load();

        Assert.Empty(catalog.Clips);
        Assert.Null(catalog.Error);
    }

    [Fact]
    public void Malformed_manifest_is_reported_without_throwing()
    {
        using var fixture = new AudioFixture();
        fixture.WriteManifest("not json");

        var catalog = fixture.Load();

        Assert.Empty(catalog.Clips);
        Assert.Equal("Audio manifest could not be loaded.", catalog.Error);
    }

    [Fact]
    public void Parses_nested_snapshot_and_uses_dataset_and_id_as_identity()
    {
        using var fixture = new AudioFixture();
        fixture.WriteManifest(Manifest(
            Entry("Adjectives", 3, "新しい", "あたらしい", "Adjectives/3/new.mp3"),
            Entry("Words", 3, "新しい", "あたらしい", "Words/3/new.opus")));
        fixture.WriteAudio("Adjectives/3/new.mp3", [0x01]);
        fixture.WriteAudio("Words/3/new.opus", [0x02]);

        var catalog = fixture.Load();

        Assert.Equal(2, catalog.Clips.Count);
        Assert.Contains(catalog.Clips, clip => clip.Dataset == "Adjectives" && clip.Id == 3 && clip.File == "Adjectives/3/new.mp3");
        Assert.Contains(catalog.Clips, clip => clip.Dataset == "Words" && clip.Id == 3);
        Assert.Null(catalog.Error);
    }

    [Fact]
    public void Find_requires_snapshot_text_to_match_after_trimming()
    {
        using var fixture = new AudioFixture();
        fixture.WriteManifest(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", "Adjectives/3/new.mp3")));
        fixture.WriteAudio("Adjectives/3/new.mp3", [0x01]);
        var catalog = fixture.Load();

        Assert.NotNull(catalog.Find(" Adjectives ", 3, " 新しい ", "あたらしい "));
        Assert.Null(catalog.Find("Words", 3, "新しい", "あたらしい"));
        Assert.Null(catalog.Find("Adjectives", 3, "新しい", "あたらし"));
    }

    [Fact]
    public void Duplicate_identity_uses_last_valid_entry_and_invalid_entry_does_not_replace_it()
    {
        using var fixture = new AudioFixture();
        fixture.WriteManifest(Manifest(
            Entry("Adjectives", 3, "古い", "ふるい", "Adjectives/3/old.mp3"),
            Entry("Adjectives", 3, "新しい", "あたらしい", "Adjectives/3/new.opus"),
            Entry("Adjectives", 3, "changed", "changed", "../outside.mp3")));
        fixture.WriteAudio("Adjectives/3/new.opus", [0x01]);

        var catalog = fixture.Load();

        var clip = Assert.Single(catalog.Clips);
        Assert.Equal("新しい", clip.Japanese);
        Assert.Equal("Adjectives/3/new.opus", clip.File);
    }

    [Theory]
    [InlineData("../outside.mp3")]
    [InlineData("/rooted.mp3")]
    [InlineData("C:\\rooted.mp3")]
    [InlineData("Adjectives/3/../../outside.wav")]
    [InlineData("Adjectives/3/file.ogg")]
    public void ReadClips_rejects_unsafe_or_unsupported_payload_paths(string path)
    {
        Assert.Empty(StudyAudioCatalog.ReadClips(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", path))));
    }

    [Fact]
    public void ReadClips_requires_manifest_array_but_skips_bad_entries()
    {
        Assert.Throws<FormatException>(() => StudyAudioCatalog.ReadClips("{}"));
        var clips = StudyAudioCatalog.ReadClips("[null," + Entry("Words", 2, "語", "ご", "Words/2/a.mp3")
            + ",{\"word\":{\"dataset\":\"Words\",\"id\":\"2\",\"japanese\":\"語\",\"kana\":\"ご\"},\"file\":\"Words/2/b.mp3\"}]");
        Assert.Single(clips);
        Assert.Equal("Words/2/a.mp3", clips[0].File);
    }

    [Fact]
    public async Task Missing_payload_and_clip_from_another_catalog_return_null()
    {
        using var fixture = new AudioFixture();
        fixture.WriteManifest(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", "Adjectives/3/missing.mp3")));
        var catalog = fixture.Load();
        using var otherFixture = new AudioFixture();
        otherFixture.WriteAudio("Adjectives/3/missing.mp3", [0x01]);
        otherFixture.WriteManifest(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", "Adjectives/3/missing.mp3")));

        Assert.Empty(catalog.Clips);
        Assert.Null(catalog.Find("Adjectives", 3, "新しい", "あたらしい"));
        Assert.Null(await catalog.GetDataUrlAsync(Assert.Single(otherFixture.Load().Clips)));
    }

    [Theory]
    [InlineData("sample.opus", "audio/ogg")]
    [InlineData("sample.mp3", "audio/mpeg")]
    [InlineData("sample.wav", "audio/wav")]
    public async Task Loaded_clip_returns_data_url_with_expected_mime(string filename, string mime)
    {
        using var fixture = new AudioFixture();
        string relative = "Adjectives/3/" + filename;
        byte[] bytes = [0x01, 0x02, 0xFE, 0xFF];
        fixture.WriteAudio(relative, bytes);
        fixture.WriteManifest(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", relative)));

        var catalog = fixture.Load();
        string? dataUrl = await catalog.GetDataUrlAsync(Assert.Single(catalog.Clips), TestContext.Current.CancellationToken);

        Assert.Equal($"data:{mime};base64,{Convert.ToBase64String(bytes)}", dataUrl);
    }

    [Fact]
    public void Payload_larger_than_16_MiB_is_not_available_to_find()
    {
        using var fixture = new AudioFixture();
        const string relative = "Adjectives/3/large.mp3";
        fixture.WriteAudio(relative, new byte[16 * 1024 * 1024 + 1]);
        fixture.WriteManifest(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", relative)));
        var catalog = fixture.Load();

        Assert.Empty(catalog.Clips);
        Assert.Null(catalog.Find("Adjectives", 3, "新しい", "あたらしい"));
    }

    [Fact]
    public void Zero_length_payload_is_not_available_to_find()
    {
        using var fixture = new AudioFixture();
        const string relative = "Adjectives/3/empty.mp3";
        fixture.WriteAudio(relative, []);
        fixture.WriteManifest(Manifest(Entry("Adjectives", 3, "新しい", "あたらしい", relative)));
        var catalog = fixture.Load();

        Assert.Empty(catalog.Clips);
        Assert.Null(catalog.Find("Adjectives", 3, "新しい", "あたらしい"));
    }

    private static string Manifest(params string[] entries) => "[" + string.Join(',', entries) + "]";

    private static string Entry(string dataset, int id, string japanese, string kana, string file) =>
        JsonSerializer.Serialize(new { word = new { dataset, id, japanese, kana }, file });

    private sealed class AudioFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "realjapanese-audio-" + Guid.NewGuid().ToString("N"));
        private string AudioRoot => Path.Combine(root, "Audio");

        public void WriteManifest(string json)
        {
            Directory.CreateDirectory(AudioRoot);
            File.WriteAllText(Path.Combine(AudioRoot, "manifest.json"), json, new UTF8Encoding(false));
        }

        public void WriteAudio(string relative, byte[] bytes)
        {
            string path = Path.Combine(AudioRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        public StudyAudioCatalog Load() => new(new RepositoryPaths(root, root));

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
