using Repositories;
using Repositories.Sync;

namespace RealJapanese.TestSupport;

/// <summary>Gives each test private catalogs and progress, leaving real user data untouched.</summary>
public sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "RealJapanese.Tests", Guid.NewGuid().ToString("N"));
    public string CatalogRoot => Path.Combine(Root, "Catalog");
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public TestWorkspace()
    {
        foreach (var dataset in ProgressStore.DatasetNames)
        {
            var relative = Path.Combine(dataset, dataset.Split('/')[^1] + ".json");
            var destination = Path.Combine(CatalogRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(RepositoryRoot, "RealJapanese", "Data", relative), destination);
        }
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
