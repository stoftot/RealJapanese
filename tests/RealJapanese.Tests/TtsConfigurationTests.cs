using System.Text.Json;
using GenerateAudioUsingTTS;

namespace RealJapanese.Tests;

/// <summary>Checks standalone TTS catalog selection and command-line safety validation.</summary>
public sealed class TtsConfigurationTests
{
    [Fact]
    public void VerbLoad_SelectsInclusiveRangeInIdOrderFromMixedIdTypes()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "verbs.json");
        WriteCatalog(path, Enumerable.Range(0, 11).Reverse().Select(id => new
        {
            id = id % 2 == 0 ? JsonSerializer.SerializeToElement(id) : JsonSerializer.SerializeToElement(id.ToString()),
            japanese = $"verb-{id}", kana = $"kana-{id}", english = $"meaning-{id}"
        }));

        var result = Verb.Load(path, 0, 10);

        Assert.Equal(Enumerable.Range(0, 11), result.Select(verb => verb.Id));
        Assert.Equal("verb-0", result[0].Japanese);
        Assert.Equal("verb-10", result[^1].Japanese);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerbLoad_RejectsMissingOrDuplicateRequestedIds(bool duplicate)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "verbs.json");
        var ids = duplicate ? new[] { 2, 3, 3, 4 } : new[] { 2, 4 };
        WriteCatalog(path, ids.Select(id => new { id, japanese = $"verb-{id}", kana = $"kana-{id}", english = "meaning" }));

        Assert.Throws<InvalidDataException>(() => Verb.Load(path, 2, 4));
    }

    [Theory]
    [InlineData("--engines|unknown")]
    [InlineData("--engines|bert,bert")]
    [InlineData("--first-id|5|--last-id|4")]
    public void OptionsParse_RejectsInvalidEnginesAndReversedRanges(string encodedArgs)
    {
        using var temp = new TempDirectory();
        var args = encodedArgs.Split('|');

        Assert.Throws<ArgumentException>(() => Options.Parse(args, temp.Path));
    }

    [Theory]
    [InlineData("Data", "audio-run")]
    [InlineData("AI library", "audio-run")]
    [InlineData(".tooling", "other-output")]
    public void OptionsParse_RejectsOutputOutsideTtsRunsWithoutCreatingIt(string parent, string leaf)
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, parent, leaf);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        Assert.Throws<ArgumentException>(() => Options.Parse(["--output", output], temp.Path));
        Assert.False(Directory.Exists(output));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void OptionsParse_DefaultInclusiveRangeSelectsElevenVerbs()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "verbs.json");
        WriteCatalog(path, Enumerable.Range(0, 11).Select(id => new
        {
            id, japanese = $"verb-{id}", kana = $"kana-{id}", english = $"meaning-{id}"
        }));

        var options = Options.Parse([], temp.Path);
        var selected = Verb.Load(path, options.FirstId, options.LastId);

        Assert.Equal(0, options.FirstId);
        Assert.Equal(10, options.LastId);
        Assert.Equal(11, selected.Count);
    }

    private static void WriteCatalog<T>(string path, IEnumerable<T> entries) =>
        File.WriteAllText(path, JsonSerializer.Serialize(entries));

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
