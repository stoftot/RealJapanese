using Repositories;

namespace RealJapanese.Mobile;

/// <summary>Refreshes packaged vocabulary without touching private study progress.</summary>
public sealed class StudyDataInstaller(RepositoryPaths paths)
{
    private static readonly string[] CatalogFiles =
    [
        "Words/Words.json", "Verbs/Verbs.json", "Adjectives/Adjectives.json",
        "Kanji/Singel/Singel.json", "Kanji/Combined/Combined.json"
    ];

    public async Task InstallAsync()
    {
        foreach (var relativePath in CatalogFiles)
        {
            var destination = Path.Combine(paths.CatalogRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + ".tmp";
            await using (var source = await FileSystem.OpenAppPackageFileAsync("Data/" + relativePath))
            await using (var target = File.Create(temporary))
                await source.CopyToAsync(target);
            File.Move(temporary, destination, overwrite: true);
        }
    }
}
