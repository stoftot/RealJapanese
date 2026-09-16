using Repositories;

var catalogRoot = FindCatalogRoot(args.FirstOrDefault());
var temporaryRoot = Path.Combine(Path.GetTempPath(), $"RealJapanese.StorageChecks-{Guid.NewGuid():N}");

try
{
    Directory.CreateDirectory(temporaryRoot);
    var originalSourceFiles = Directory
        .EnumerateFiles(catalogRoot, "*.json", SearchOption.AllDirectories)
        .ToDictionary(path => path, File.ReadAllBytes);

    VerifyRealCatalogs(catalogRoot, temporaryRoot);
    VerifyIndependentProgressAndRestart(catalogRoot, temporaryRoot);
    VerifyStableMissingIds(temporaryRoot);
    VerifyFilesUnchanged(originalSourceFiles);

    Console.WriteLine("Storage checks passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
finally
{
    if (Directory.Exists(temporaryRoot))
    {
        Directory.Delete(temporaryRoot, recursive: true);
    }
}

static void VerifyRealCatalogs(string catalogRoot, string temporaryRoot)
{
    var catalogFiles = Directory
        .EnumerateFiles(catalogRoot, "*.json", SearchOption.AllDirectories)
        .Where(path => !path.EndsWith("SavedData.json", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(path => path, File.ReadAllBytes);

    var paths = new RepositoryPaths(catalogRoot, Path.Combine(temporaryRoot, "fresh-progress"));
    var wordData = new WordData(paths);
    var verbData = new VerbData(paths);
    var adjectiveData = new AdjectiveData(paths);
    var kanjiData = new KanjiData(paths);

    VerifyUniqueIds("words", wordData.Words.Select(word => word.Id));
    VerifyUniqueIds("verbs", verbData.Words.Select(word => word.Id));
    VerifyUniqueIds("adjectives", adjectiveData.Words.Select(word => word.Id));
    VerifyUniqueIds("single kanji", kanjiData.Single.Words.Select(word => word.Id));
    VerifyUniqueIds("combined kanji", kanjiData.Combined.Words.Select(word => word.Id));

    Assert(!wordData.VocabWordIds.Any(), "Fresh word progress should be empty.");
    Assert(!verbData.VocabWordIds.Any(), "Fresh verb progress should be empty.");
    Assert(!adjectiveData.VocabWordIds.Any(), "Fresh adjective progress should be empty.");
    Assert(!kanjiData.Single.VocabWordIds.Any(), "Fresh single-kanji progress should be empty.");
    Assert(!kanjiData.Combined.VocabWordIds.Any(), "Fresh combined-kanji progress should be empty.");
    Assert(!Directory.Exists(paths.ProgressRoot), "Constructing repositories should not create progress files.");

    foreach (var (path, originalBytes) in catalogFiles)
    {
        Assert(originalBytes.SequenceEqual(File.ReadAllBytes(path)), $"Repository construction changed catalog file '{path}'.");
    }
}

static void VerifyIndependentProgressAndRestart(string catalogRoot, string temporaryRoot)
{
    var firstProgressRoot = Path.Combine(temporaryRoot, "first-progress");
    var secondProgressRoot = Path.Combine(temporaryRoot, "second-progress");
    var firstPaths = new RepositoryPaths(catalogRoot, firstProgressRoot);
    var secondPaths = new RepositoryPaths(catalogRoot, secondProgressRoot);

    var first = new WordData(firstPaths);
    var second = new WordData(secondPaths);
    var firstWord = first.Words.First();
    var secondWord = second.Words.Skip(1).First();

    first.AddToTraining(firstWord);
    second.AddToVocab(secondWord);

    var restartedFirst = new WordData(firstPaths);
    var restartedSecond = new WordData(secondPaths);

    Assert(restartedFirst.TrainingWordIds.SequenceEqual([firstWord.Id]), "First progress did not survive restart.");
    Assert(!restartedFirst.VocabWordIds.Any(), "Second progress leaked into first progress root.");
    Assert(restartedSecond.VocabWordIds.SequenceEqual([secondWord.Id]), "Second progress did not survive restart.");
    Assert(!restartedSecond.TrainingWordIds.Any(), "First progress leaked into second progress root.");

    var staleProgressPath = Path.Combine(firstProgressRoot, "Words", "SavedData.json");
    File.WriteAllText(
        staleProgressPath,
        $$"""
        {
          "KnownIds": [{{firstWord.Id}}, 2147483647],
          "TrainingIds": [],
          "RehearsingIds": []
        }
        """);

    var withStaleId = new WordData(firstPaths);
    Assert(withStaleId.VocabWords.Select(word => word.Id).SequenceEqual([firstWord.Id]), "A stale progress ID was not ignored.");
    Assert(withStaleId.VocabWordIds.SequenceEqual([firstWord.Id]), "A stale progress ID remained in memory.");
}

static void VerifyStableMissingIds(string temporaryRoot)
{
    var catalogRoot = Path.Combine(temporaryRoot, "missing-id-catalog");
    var wordsFolder = Path.Combine(catalogRoot, "Words");
    Directory.CreateDirectory(wordsFolder);

    var catalogPath = Path.Combine(wordsFolder, "Words.json");
    File.WriteAllText(
        catalogPath,
        """
        [
          { "japanese": "一", "kana": "いち", "english": "one" },
          { "id": "5", "japanese": "二", "kana": "に", "english": "two" },
          { "japanese": "三", "kana": "さん", "english": "three" }
        ]
        """);
    var originalCatalog = File.ReadAllBytes(catalogPath);
    var paths = new RepositoryPaths(catalogRoot, Path.Combine(temporaryRoot, "missing-id-progress"));

    var firstIds = new WordData(paths).Words.Select(word => word.Id).ToArray();
    var secondIds = new WordData(paths).Words.Select(word => word.Id).ToArray();

    Assert(firstIds.SequenceEqual([6, 5, 7]), "Missing IDs were not assigned after the highest existing ID in catalog order.");
    Assert(secondIds.SequenceEqual(firstIds), "In-memory IDs changed after repository restart.");
    Assert(originalCatalog.SequenceEqual(File.ReadAllBytes(catalogPath)), "Assigning missing IDs rewrote the catalog.");
}

static void VerifyUniqueIds(string catalogName, IEnumerable<int> ids)
{
    var idList = ids.ToList();
    Assert(idList.Count > 0, $"The {catalogName} catalog was empty.");
    Assert(idList.All(id => id >= 0), $"The {catalogName} catalog contains an unassigned ID.");
    Assert(idList.Distinct().Count() == idList.Count, $"The {catalogName} catalog contains duplicate IDs.");
}

static void VerifyFilesUnchanged(IReadOnlyDictionary<string, byte[]> originalFiles)
{
    foreach (var (path, originalBytes) in originalFiles)
    {
        Assert(originalBytes.SequenceEqual(File.ReadAllBytes(path)), $"Storage checks changed source file '{path}'.");
    }
}

static string FindCatalogRoot(string? requestedRoot)
{
    var candidates = new[]
    {
        requestedRoot,
        Path.Combine(Environment.CurrentDirectory, "RealJapanese", "Data"),
        Path.Combine(Environment.CurrentDirectory, "Data"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Data"))
    };

    var root = candidates
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.GetFullPath(path!))
        .FirstOrDefault(path => File.Exists(Path.Combine(path, "Words", "Words.json")));

    return root ?? throw new DirectoryNotFoundException(
        "Could not find the RealJapanese catalog root. Pass it as the first argument.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
