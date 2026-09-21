using System.Text.Json;
using DataLoaders;
using DataLoaders.Models;

internal static class JsonChecks
{
    public static void Run(string temporaryRoot)
    {
        var root = Path.Combine(temporaryRoot, "json-contracts");
        Directory.CreateDirectory(root);
        const string first = """{"id":"12","japanese":"猫","kana":"ねこ","english":"cat"}""";
        const string second = """{"japanese":"犬","kana":"いぬ","english":"dog"}""";

        Write("words.json", $"[{first},{second}]");
        var words = new JsonLoader<Word>(root, "words.json").Load().ToArray();
        Assert(words.Length == 2 && words[0].Id == 12 && words[1].Id == -1,
            "JSON arrays must preserve order, string IDs and missing-ID defaults.");
        Assert(words[0].Japanese == "猫" && words[1].English == "dog", "Catalog text changed while loading.");

        Write("single.json", first);
        Assert(new JsonLoader<Word>(root, "single.json").Load().Single() == words[0],
            "Single-object JSON must load as one record.");
        foreach (var empty in new[] { "", " \r\n\t", "[]", "null" })
        {
            Write("empty.json", empty);
            Assert(!new JsonLoader<Word>(root, "empty.json").Load().Any(), "Empty JSON input must yield no records.");
        }

        Write("words.jsonl", first + "\n\n  \nnull\n" + second + "\n");
        Assert(new JsonLoader<Word>(root, "words.jsonl").Load().SequenceEqual(words),
            "JSONL must preserve records while ignoring blank lines and null records.");

        var output = Path.Combine(root, "new", "nested");
        new JsonSaver<Word>(output, "words.json").Save((IEnumerable<Word>)words);
        Assert(new JsonLoader<Word>(output, "words.json").Load().SequenceEqual(words),
            "Saving an array must round-trip all fields and create missing directories.");
        Assert(File.ReadAllText(Path.Combine(output, "words.json")).Contains("猫"),
            "Saved Japanese text must remain readable.");
        new JsonSaver<Word>(output, "single.json").Save(words[0]);
        Assert(new JsonLoader<Word>(output, "single.json").Load().Single() == words[0],
            "Saving a single object must round-trip its string ID and vocabulary fields.");
        new JsonSaver<Word>(output, "words.json").Save(Array.Empty<Word>().AsEnumerable());
        Assert(!new JsonLoader<Word>(output, "words.json").Load().Any(),
            "Saving an empty collection must replace previous records.");

        Throws<FileNotFoundException>(() => new JsonLoader<Word>(root, "missing.json").Load().ToArray(),
            "A missing catalog must not silently become an empty collection.");
        foreach (var malformed in new[] { "{", first.Replace("\"12\"", "\"bad\""), first.Replace("\"12\"", "\"2147483648\""),
                     """{"id":"1","japanese":"猫"}""" })
        {
            Write("invalid.json", malformed);
            Throws<JsonException>(() => new JsonLoader<Word>(root, "invalid.json").Load().ToArray(),
                "Malformed JSON, invalid IDs and missing required vocabulary fields must fail explicitly.");
        }
        Write("invalid.jsonl", first + "\n{\n");
        Throws<JsonException>(() => new JsonLoader<Word>(root, "invalid.jsonl").Load().ToArray(),
            "A malformed later JSONL record must fail when the sequence is enumerated.");

        void Write(string name, string content) => File.WriteAllText(Path.Combine(root, name), content);
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
