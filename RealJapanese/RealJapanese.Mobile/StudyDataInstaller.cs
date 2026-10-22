using Repositories;
using Repositories.Audio;

namespace RealJapanese.Mobile;

/// <summary>Refreshes packaged vocabulary without touching private study progress.</summary>
public sealed class StudyDataInstaller(RepositoryPaths paths)
{
    private static readonly string[] CatalogFiles = Repositories.Sync.ProgressStore.DatasetNames
        .Select(name => name + "/" + name.Split('/')[^1] + ".json").ToArray();

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
        await InstallAudioAsync();
    }

    private async Task InstallAudioAsync()
    {
        var audioRoot = Path.Combine(paths.CatalogRoot, "Audio");
        Directory.CreateDirectory(audioRoot);
        var json = "[]";
        try
        {
            await using var manifest = await FileSystem.OpenAppPackageFileAsync("Data/Audio/manifest.json");
            using var reader = new StreamReader(manifest);
            json = await reader.ReadToEndAsync();
        }
        catch (FileNotFoundException) { } // Audio exports are optional.

        foreach (var clip in StudyAudioCatalog.ReadClips(json))
        {
            var destination = Path.Combine(audioRoot, clip.File);
            // Export files are immutable. Reuse installed payloads on subsequent launches.
            if (File.Exists(destination) && new FileInfo(destination).Length > 0) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + ".tmp";
            await using (var source = await FileSystem.OpenAppPackageFileAsync("Data/Audio/" + clip.File))
            await using (var target = File.Create(temporary))
                await source.CopyToAsync(target);
            File.Move(temporary, destination, overwrite: true);
        }
        // Publish the manifest after its payloads; never touch the separate progress root.
        var manifestPath = Path.Combine(audioRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath + ".tmp", json);
        File.Move(manifestPath + ".tmp", manifestPath, overwrite: true);
    }
}
