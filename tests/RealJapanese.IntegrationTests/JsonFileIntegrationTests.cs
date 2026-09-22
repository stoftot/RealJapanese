using System.Text.Json;
using DataLoaders;
using DataLoaders.Models;
using RealJapanese.TestSupport;

namespace RealJapanese.IntegrationTests;

/// <summary>Exercises JSON and JSONL loader/saver contracts against isolated real files.</summary>
public sealed class JsonFileIntegrationTests
{
    private const string First = """{"id":"12","japanese":"猫","kana":"ねこ","english":"cat"}""";
    private const string Second = """{"japanese":"犬","kana":"いぬ","english":"dog"}""";

    [Fact]
    public void JsonArray_LoadsInOrderWithStringAndMissingIds()
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);
        File.WriteAllText(Path.Combine(root, "words.json"), $"[{First},{Second}]");

        var words = new JsonLoader<Word>(root, "words.json").Load().ToArray();

        Assert.Equal(2, words.Length);
        Assert.Equal(12, words[0].Id);
        Assert.Equal(-1, words[1].Id);
        Assert.Equal("猫", words[0].Japanese);
        Assert.Equal("dog", words[1].English);
    }

    [Fact]
    public void SingleObjectJson_LoadsAsOneRecord()
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);
        File.WriteAllText(Path.Combine(root, "single.json"), First);

        var word = Assert.Single(new JsonLoader<Word>(root, "single.json").Load());

        Assert.Equal(new Word { Id = 12, Japanese = "猫", Kana = "ねこ", English = "cat" }, word);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    [InlineData("[]")]
    [InlineData("null")]
    public void EmptyJsonRepresentations_LoadNoRecords(string content)
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);
        File.WriteAllText(Path.Combine(root, "empty.json"), content);

        Assert.Empty(new JsonLoader<Word>(root, "empty.json").Load());
    }

    [Fact]
    public void JsonlLoader_IgnoresBlankAndNullLinesWhilePreservingRecords()
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);
        File.WriteAllText(Path.Combine(root, "words.jsonl"), First + "\n\n  \nnull\n" + Second + "\n");

        var words = new JsonLoader<Word>(root, "words.jsonl").Load().ToArray();

        Assert.Equal([12, -1], words.Select(word => word.Id));
        Assert.Equal(["猫", "犬"], words.Select(word => word.Japanese));
    }

    [Fact]
    public void JsonArraySaver_CreatesDirectoriesAndRoundTripsReadableJapanese()
    {
        using var workspace = new TestWorkspace();
        var root = Path.Combine(workspace.Root, "json-contracts", "new", "nested");
        var words = Words();

        new JsonSaver<Word>(root, "words.json").Save((IEnumerable<Word>)words);

        Assert.Equal(words, new JsonLoader<Word>(root, "words.json").Load());
        Assert.Contains("猫", File.ReadAllText(Path.Combine(root, "words.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void SingleObjectSaver_RoundTripsVocabularyFieldsAndStringId()
    {
        using var workspace = new TestWorkspace();
        var root = Path.Combine(workspace.Root, "json-contracts", "single");
        var expected = Words()[0];

        new JsonSaver<Word>(root, "single.json").Save(expected);

        Assert.Equal(expected, Assert.Single(new JsonLoader<Word>(root, "single.json").Load()));
        Assert.Contains("\"id\": \"12\"", File.ReadAllText(Path.Combine(root, "single.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void SavingEmptyCollection_ReplacesPreviousRecords()
    {
        using var workspace = new TestWorkspace();
        var root = Path.Combine(workspace.Root, "json-contracts", "replace");
        var saver = new JsonSaver<Word>(root, "words.json");
        saver.Save((IEnumerable<Word>)Words());

        saver.Save(Array.Empty<Word>().AsEnumerable());

        Assert.Empty(new JsonLoader<Word>(root, "words.json").Load());
    }

    [Fact]
    public void MissingJsonFile_ThrowsFileNotFoundException()
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);

        Assert.Throws<FileNotFoundException>(() => new JsonLoader<Word>(root, "missing.json").Load().ToArray());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"id\":\"bad\",\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"}")]
    [InlineData("{\"id\":\"2147483648\",\"japanese\":\"猫\",\"kana\":\"ねこ\",\"english\":\"cat\"}")]
    [InlineData("{\"id\":\"1\",\"japanese\":\"猫\"}")]
    public void InvalidJson_ThrowsJsonException(string content)
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);
        File.WriteAllText(Path.Combine(root, "invalid.json"), content);

        Assert.ThrowsAny<JsonException>(() => new JsonLoader<Word>(root, "invalid.json").Load().ToArray());
    }

    [Fact]
    public void MalformedLaterJsonlRecord_ThrowsWhenSequenceIsEnumerated()
    {
        using var workspace = new TestWorkspace();
        var root = CreateRoot(workspace);
        File.WriteAllText(Path.Combine(root, "invalid.jsonl"), First + "\n{\n");
        var records = new JsonLoader<Word>(root, "invalid.jsonl").Load();

        Assert.ThrowsAny<JsonException>(() => records.ToArray());
    }

    [Fact(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    // JsonSaver currently indents each JSONL record across lines, which the line-based loader cannot read.
    public void JsonlSaver_OutputRoundTripsThroughJsonlLoader()
    {
        using var workspace = new TestWorkspace();
        var root = Path.Combine(workspace.Root, "json-contracts", "jsonl-roundtrip");
        var expected = Words();

        new JsonSaver<Word>(root, "words.jsonl").Save((IEnumerable<Word>)expected);

        Assert.Equal(expected, new JsonLoader<Word>(root, "words.jsonl").Load());
    }

    private static string CreateRoot(TestWorkspace workspace)
    {
        var root = Path.Combine(workspace.Root, "json-contracts");
        Directory.CreateDirectory(root);
        return root;
    }

    private static Word[] Words() =>
    [
        new Word { Id = 12, Japanese = "猫", Kana = "ねこ", English = "cat" },
        new Word { Japanese = "犬", Kana = "いぬ", English = "dog" }
    ];
}
