namespace Repositories;

/// <summary>
/// Defines the independent roots used for read-only study catalogs and mutable progress.
/// </summary>
public sealed class RepositoryPaths
{
    public static RepositoryPaths Default { get; } = new("../Data", "../Data");

    public string CatalogRoot { get; }
    public string ProgressRoot { get; }

    public RepositoryPaths(string catalogRoot, string progressRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(progressRoot);

        CatalogRoot = catalogRoot;
        ProgressRoot = progressRoot;
    }

    internal string CatalogFolder(params string[] segments) =>
        Combine(CatalogRoot, segments);

    internal string ProgressFolder(params string[] segments) =>
        Combine(ProgressRoot, segments);

    private static string Combine(string root, string[] segments) =>
        segments.Aggregate(root, Path.Combine);
}
