using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DataLoaders.Models;
using RealJapanese.TestSupport;

namespace RealJapanese.IntegrationTests;

/// <summary>Runs the real duplicate-cleanup entry point in isolated child processes and disposable data trees.</summary>
public sealed class UtilityCleanupIntegrationTests
{
    private const int MaximumCapturedCharacters = 16 * 1024;
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UnifiedProgressGuard_RejectsCleanupWithoutChangingFiles()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateFixture(workspace, "unified-guard",
            """
            [
              { "id": "10", "japanese": "一", "kana": "いち", "english": "one" }
            ]
            """,
            """
            { "KnownIds": [10], "TrainingIds": [], "RehearsingIds": [] }
            """);
        File.WriteAllText(Path.Combine(fixture.DataRoot, "Progress.json"),
            "unified progress must block legacy cleanup");
        var before = CaptureFiles(fixture.DataRoot);

        var result = await RunUtilityAsync(fixture.WorkingDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("cannot remap unified Progress.json", result.StandardError, StringComparison.Ordinal);
        AssertFilesEqual(before, CaptureFiles(fixture.DataRoot));
    }

    [Fact]
    public async Task DuplicateCleanup_CollapsesWordsAndRemapsExclusiveProgress()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateFixture(workspace, "success",
            """
            [
              { "id": "10", "japanese": "一", "kana": "いち", "english": "one", "category": "number" },
              { "id": "11", "japanese": "一", "kana": "いち", "english": "one", "category": "duplicate" },
              { "id": "20", "japanese": "二", "kana": "に", "english": "two", "category": "number" },
              { "id": "21", "japanese": "三", "kana": "さん", "english": "three", "category": "number" }
            ]
            """,
            """
            {
              "KnownIds": [11],
              "TrainingIds": [10, 11, 20, 21, 21],
              "RehearsingIds": [10, 20, 20]
            }
            """);

        var result = await RunUtilityAsync(fixture.WorkingDirectory);

        Assert.True(result.ExitCode == 0,
            $"Cleanup failed. stdout: {result.StandardOutput} stderr: {result.StandardError}");
        var words = Deserialize<List<Word>>(fixture.WordsPath);
        Assert.Equal([0, 1, 2], words.Select(word => word.Id));
        Assert.Equal(
            [("一", "いち", "one", "number"), ("二", "に", "two", "number"), ("三", "さん", "three", "number")],
            words.Select(word => (word.Japanese, word.Kana, word.English, word.Category)));

        var saved = Deserialize<VocabSaveFile>(fixture.SavedDataPath);
        Assert.Equal([0], saved.KnownIds);
        Assert.Equal([1], saved.RehearsingIds);
        Assert.Equal([2], saved.TrainingIds);
    }

    [Fact]
    public async Task StaleLegacyId_RejectsCleanupBeforeAnyWrite()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateFixture(workspace, "stale-id",
            """
            [
              { "id": "10", "japanese": "一", "kana": "いち", "english": "one" },
              { "id": "20", "japanese": "二", "kana": "に", "english": "two" }
            ]
            """,
            """
            { "KnownIds": [999], "TrainingIds": [10], "RehearsingIds": [] }
            """);
        var before = CaptureFiles(fixture.DataRoot);

        var result = await RunUtilityAsync(fixture.WorkingDirectory);

        Assert.NotEqual(0, result.ExitCode);
        AssertFilesEqual(before, CaptureFiles(fixture.DataRoot));
    }

    [Fact(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    // The utility writes the catalog before the save. A locked save must not strand existing progress IDs.
    public async Task SaveWriteFailure_PreservesUsableCatalogProgressPairAndAllowsCleanRetry()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This regression uses Windows read-sharing semantics.");
        using var workspace = new TestWorkspace();
        var fixture = CreateFixture(workspace, "locked-save",
            """
            [
              { "id": "10", "japanese": "一", "kana": "いち", "english": "one" },
              { "id": "11", "japanese": "一", "kana": "いち", "english": "one" },
              { "id": "20", "japanese": "二", "kana": "に", "english": "two" }
            ]
            """,
            """
            { "KnownIds": [11], "TrainingIds": [], "RehearsingIds": [] }
            """);
        var originalWords = File.ReadAllBytes(fixture.WordsPath);
        var originalSave = File.ReadAllBytes(fixture.SavedDataPath);

        ProcessResult failedResult;
        using (new FileStream(fixture.SavedDataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            failedResult = await RunUtilityAsync(fixture.WorkingDirectory);
        }

        Assert.NotEqual(0, failedResult.ExitCode);
        var failedWords = File.ReadAllBytes(fixture.WordsPath);
        var failedSave = File.ReadAllBytes(fixture.SavedDataPath);
        var bothUnchanged = failedWords.SequenceEqual(originalWords) && failedSave.SequenceEqual(originalSave);
        var savedIdsStillResolve = SavedIdsResolve(fixture.WordsPath, fixture.SavedDataPath);

        File.WriteAllBytes(fixture.WordsPath, originalWords);
        File.WriteAllBytes(fixture.SavedDataPath, originalSave);
        var retry = await RunUtilityAsync(fixture.WorkingDirectory);
        Assert.Equal(0, retry.ExitCode);
        Assert.Equal([0], Deserialize<VocabSaveFile>(fixture.SavedDataPath).KnownIds);
        Assert.Contains(Deserialize<List<Word>>(fixture.WordsPath), word => word.Id == 0 && word.Japanese == "一");

        Assert.True(bothUnchanged || savedIdsStillResolve,
            "Failed cleanup rewrote the catalog while leaving legacy progress IDs that no longer resolve.");
    }

    private static UtilityFixture CreateFixture(
        TestWorkspace workspace,
        string name,
        string wordsJson,
        string savedDataJson)
    {
        var caseRoot = Path.GetFullPath(Path.Combine(workspace.Root, "cleanup", name));
        var dataRoot = Path.Combine(caseRoot, "Data");
        var wordsRoot = Path.Combine(dataRoot, "Words");
        var workingDirectory = Path.Combine(caseRoot, "run", "a", "b", "c");
        Directory.CreateDirectory(wordsRoot);
        Directory.CreateDirectory(workingDirectory);
        var resolvedDataRoot = Path.GetFullPath(Path.Combine(workingDirectory, "..", "..", "..", "..", "Data"));
        Assert.Equal(Path.TrimEndingDirectorySeparator(dataRoot), Path.TrimEndingDirectorySeparator(resolvedDataRoot));

        var wordsPath = Path.Combine(wordsRoot, "Words.json");
        var savedDataPath = Path.Combine(wordsRoot, "SavedData.json");
        File.WriteAllText(wordsPath, wordsJson);
        File.WriteAllText(savedDataPath, savedDataJson);
        return new(dataRoot, workingDirectory, wordsPath, savedDataPath);
    }

    private static async Task<ProcessResult> RunUtilityAsync(string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(FindUtilityHostAssembly());
        Assert.True(process.Start(), "Could not start the duplicate-cleanup utility host.");
        var outputTask = DrainAsync(process.StandardOutput);
        var errorTask = DrainAsync(process.StandardError);
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            throw new TimeoutException($"Duplicate cleanup did not exit within {ProcessTimeout.TotalSeconds:0} seconds.");
        }

        return new(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindUtilityHostAssembly()
    {
        var targetFramework = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var configuration = targetFramework.Parent
            ?? throw new DirectoryNotFoundException("Could not determine the integration-test configuration.");
        var path = Path.Combine(TestWorkspace.RepositoryRoot, "tests", "RealJapanese.UtilityHost", "bin",
            configuration.Name, targetFramework.Name, "RealJapanese.UtilityHost.dll");
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException("The utility host was not built with the integration tests.", path);
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var captured = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            var remaining = MaximumCapturedCharacters - captured.Length;
            if (remaining > 0) captured.Append(buffer, 0, Math.Min(read, remaining));
        }
        return captured.ToString();
    }

    private static bool SavedIdsResolve(string wordsPath, string savedDataPath)
    {
        var knownIds = Deserialize<List<Word>>(wordsPath).Select(word => word.Id).ToHashSet();
        var save = Deserialize<VocabSaveFile>(savedDataPath);
        return save.KnownIds.Concat(save.RehearsingIds).Concat(save.TrainingIds).All(knownIds.Contains);
    }

    private static T Deserialize<T>(string path) where T : notnull =>
        JsonSerializer.Deserialize<T>(File.ReadAllBytes(path))
        ?? throw new InvalidDataException($"'{path}' contained JSON null.");

    private static Dictionary<string, byte[]> CaptureFiles(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertFilesEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private sealed record UtilityFixture(string DataRoot, string WorkingDirectory, string WordsPath, string SavedDataPath);
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
