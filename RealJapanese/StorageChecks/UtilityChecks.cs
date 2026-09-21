using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DataLoaders.Models;

internal static class UtilityChecks
{
    private const int MaximumCapturedCharacters = 16 * 1024;
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);

    public static void Run(string temporaryRoot)
    {
        _ = FindUtilityAssembly();
        var storageChecksAssembly = typeof(UtilityChecks).Assembly.Location;
        VerifyUnifiedProgressGuard(temporaryRoot, storageChecksAssembly);
        VerifyDuplicateCleanup(temporaryRoot, storageChecksAssembly);
        VerifyStaleLegacyIdFailsBeforeWriting(temporaryRoot, storageChecksAssembly);
    }

    public static int InvokeCleanupEntryPoint()
    {
        var entryPoint = Assembly.LoadFrom(FindUtilityAssembly()).EntryPoint
            ?? throw new InvalidOperationException("The duplicate-cleanup utility has no entry point.");
        var arguments = entryPoint.GetParameters().Length == 0 ? null : new object?[] { Array.Empty<string>() };
        try
        {
            return entryPoint.Invoke(null, arguments) switch
            {
                int exitCode => exitCode,
                null => 0,
                _ => throw new InvalidOperationException("The duplicate-cleanup utility returned an unsupported result.")
            };
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            Console.Error.WriteLine(exception.InnerException);
            return 1;
        }
    }

    private static void VerifyUnifiedProgressGuard(string temporaryRoot, string storageChecksAssembly)
    {
        var fixture = CreateFixture(temporaryRoot, "unified-guard",
            """
            [
              { "id": "10", "japanese": "一", "kana": "いち", "english": "one" }
            ]
            """,
            """
            { "KnownIds": [10], "TrainingIds": [], "RehearsingIds": [] }
            """);
        File.WriteAllText(Path.Combine(fixture.DataRoot, "Progress.json"), "unified progress must block legacy cleanup");
        var before = CaptureFiles(fixture.DataRoot);

        var result = RunUtility(storageChecksAssembly, fixture.WorkingDirectory);

        Assert(result.ExitCode != 0, "Duplicate cleanup succeeded despite unified Progress.json.");
        Assert(result.StandardError.Contains("cannot remap unified Progress.json", StringComparison.Ordinal),
            "Duplicate cleanup did not report its unified-progress safety guard.");
        AssertFilesUnchanged(fixture.DataRoot, before, "Unified-progress rejection changed input data.");
    }

    private static void VerifyDuplicateCleanup(string temporaryRoot, string storageChecksAssembly)
    {
        var fixture = CreateFixture(temporaryRoot, "success",
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

        var result = RunUtility(storageChecksAssembly, fixture.WorkingDirectory);

        Assert(result.ExitCode == 0,
            $"Duplicate cleanup failed. stdout: {result.StandardOutput} stderr: {result.StandardError}");
        var words = JsonSerializer.Deserialize<List<Word>>(File.ReadAllBytes(fixture.WordsPath))
            ?? throw new InvalidOperationException("Duplicate cleanup produced an empty word catalog.");
        Assert(words.Count == 3, "Duplicate cleanup did not remove the repeated word.");
        Assert(words.Select(word => word.Id).SequenceEqual([0, 1, 2]),
            "Duplicate cleanup did not assign contiguous replacement IDs in catalog order.");
        Assert(words.Select(word => (word.Japanese, word.Kana, word.English, word.Category)).SequenceEqual(new[]
        {
            ("一", "いち", "one", "number"),
            ("二", "に", "two", "number"),
            ("三", "さん", "three", "number")
        }), "Duplicate cleanup changed distinct vocabulary fields or retained the duplicate's fields.");

        var saved = JsonSerializer.Deserialize<VocabSaveFile>(File.ReadAllBytes(fixture.SavedDataPath))
            ?? throw new InvalidOperationException("Duplicate cleanup produced empty legacy progress.");
        Assert(saved.KnownIds.SequenceEqual([0]) && saved.RehearsingIds.SequenceEqual([1]) &&
            saved.TrainingIds.SequenceEqual([2]),
            "Duplicate cleanup did not remap progress with Known > Rehearsing > Training priority.");
    }

    private static void VerifyStaleLegacyIdFailsBeforeWriting(string temporaryRoot, string storageChecksAssembly)
    {
        var fixture = CreateFixture(temporaryRoot, "stale-id",
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

        var result = RunUtility(storageChecksAssembly, fixture.WorkingDirectory);

        Assert(result.ExitCode != 0, "Duplicate cleanup accepted a stale legacy progress ID.");
        AssertFilesUnchanged(fixture.DataRoot, before, "Stale-ID rejection changed input data.");
    }

    private static UtilityFixture CreateFixture(string temporaryRoot, string name, string wordsJson, string savedDataJson)
    {
        var caseRoot = Path.GetFullPath(Path.Combine(temporaryRoot, "cleanup", name));
        var dataRoot = Path.Combine(caseRoot, "Data");
        var wordsRoot = Path.Combine(dataRoot, "Words");
        var workingDirectory = Path.Combine(caseRoot, "run", "a", "b", "c");
        Directory.CreateDirectory(wordsRoot);
        Directory.CreateDirectory(workingDirectory);

        var resolvedDataRoot = Path.GetFullPath(Path.Combine(workingDirectory, "..", "..", "..", "..", "Data"));
        Assert(Path.TrimEndingDirectorySeparator(resolvedDataRoot) == Path.TrimEndingDirectorySeparator(dataRoot),
            "Disposable cleanup working directory did not resolve ../../../../Data to its isolated fixture.");

        var wordsPath = Path.Combine(wordsRoot, "Words.json");
        var savedDataPath = Path.Combine(wordsRoot, "SavedData.json");
        File.WriteAllText(wordsPath, wordsJson);
        File.WriteAllText(savedDataPath, savedDataJson);
        return new(dataRoot, workingDirectory, wordsPath, savedDataPath);
    }

    private static string FindUtilityAssembly()
    {
        var targetFrameworkDirectory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var configurationDirectory = targetFrameworkDirectory.Parent
            ?? throw new DirectoryNotFoundException("Could not determine the StorageChecks build configuration.");
        var storageProjectDirectory = configurationDirectory.Parent?.Parent
            ?? throw new DirectoryNotFoundException("Could not determine the StorageChecks project directory.");
        var projectsRoot = storageProjectDirectory.Parent
            ?? throw new DirectoryNotFoundException("Could not determine the repository projects directory.");
        var utilityAssembly = Path.Combine(projectsRoot.FullName, "CheckDataForDuplicates", "bin",
            configurationDirectory.Name, targetFrameworkDirectory.Name, "CheckDataForDuplicates.dll");
        if (!File.Exists(utilityAssembly))
            throw new FileNotFoundException("The duplicate-cleanup utility was not built with StorageChecks.", utilityAssembly);
        return utilityAssembly;
    }

    private static ProcessResult RunUtility(string storageChecksAssembly, string workingDirectory)
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
        process.StartInfo.ArgumentList.Add(storageChecksAssembly);
        process.StartInfo.ArgumentList.Add("--invoke-cleanup");
        if (!process.Start()) throw new InvalidOperationException("Could not start the duplicate-cleanup utility.");
        var outputTask = DrainAsync(process.StandardOutput);
        var errorTask = DrainAsync(process.StandardError);
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        try
        {
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process.WaitForExit();
            Task.WaitAll(outputTask, errorTask);
            throw new TimeoutException($"Duplicate cleanup did not exit within {ProcessTimeout.TotalSeconds:0} seconds. " +
                $"stdout: {outputTask.Result} stderr: {errorTask.Result}");
        }
        Task.WaitAll(outputTask, errorTask);
        return new(process.ExitCode, outputTask.Result, errorTask.Result);
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var captured = new StringBuilder();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            var remaining = MaximumCapturedCharacters - captured.Length;
            if (remaining > 0) captured.Append(buffer, 0, Math.Min(read, remaining));
        }
        return captured.ToString();
    }

    private static Dictionary<string, byte[]> CaptureFiles(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertFilesUnchanged(string root, IReadOnlyDictionary<string, byte[]> expected, string message)
    {
        var actual = CaptureFiles(root);
        Assert(actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var bytes) &&
            bytes.SequenceEqual(pair.Value)), message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record UtilityFixture(string DataRoot, string WorkingDirectory, string WordsPath, string SavedDataPath);
    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
