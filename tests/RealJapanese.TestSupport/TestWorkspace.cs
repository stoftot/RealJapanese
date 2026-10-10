using Repositories;
using Repositories.Sync;
using DataLoaders.Models;
using System.Text.Json;

namespace RealJapanese.TestSupport;

/// <summary>Gives each test private catalogs and progress, leaving real user data untouched.</summary>
public sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "RealJapanese.Tests", Guid.NewGuid().ToString("N"));
    public string CatalogRoot => Path.Combine(Root, "Catalog");
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public TestWorkspace(bool seedGeneralWords = true)
    {
        try
        {
            foreach (var dataset in ProgressStore.DatasetNames)
            {
                var relative = Path.Combine(dataset, dataset.Split('/')[^1] + ".json");
                var destination = Path.Combine(CatalogRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(RepositoryRoot, "RealJapanese", "Data", relative), destination);
            }
            // The canonical general catalog is empty after grammatical classification.
            // Practice tests use explicit self-added data, independent of book contents.
            if (seedGeneralWords)
                File.WriteAllText(Path.Combine(CatalogRoot, "Words", "Words.json"), JsonSerializer.Serialize(new Word[]
                {
                    new() { Id = 1000, Japanese = "あお", Kana = "あお", English = "blue" },
                    new() { Id = 1001, Japanese = "あか", Kana = "あか", English = "red" },
                    new() { Id = 1002, Japanese = "しろ", Kana = "しろ", English = "white" },
                    new() { Id = 1003, Japanese = "くろ", Kana = "くろ", English = "black" },
                    new() { Id = 1004, Japanese = "みどり", Kana = "みどり", English = "green" },
                    new() { Id = 1005, Japanese = "むらさき", Kana = "むらさき", English = "purple" },
                    new() { Id = 1006, Japanese = "ちゃいろ", Kana = "ちゃいろ", English = "brown" },
                    new() { Id = 1007, Japanese = "きいろ", Kana = "きいろ", English = "yellow" }
                }));
        }
        catch { Dispose(); throw; }
    }

    public RepositoryPaths CreatePaths(string name = "progress") => new(CatalogRoot, Path.Combine(Root, name));

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RealJapanese", "Data", "Words", "Words.json")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Run tests from a built checkout containing RealJapanese/Data.");
    }
}
