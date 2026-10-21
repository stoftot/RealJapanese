using System.Text.Json;
using AiLibrary;
using AiLibrary.StyleBertVits2;

namespace AudioStudio;

public sealed record WordPitchProfile(CatalogWord Word, JapanesePronunciationProfile? Profile, string? Error, bool HumanVerified = false);
public sealed record PitchCollection(int Version, string Description, IReadOnlyList<WordPitchProfile> Words);

/// <summary>Consumer-owned word linkage and review state; the library owns the generic profile format.</summary>
public sealed class PitchProfiles
{
    private readonly Dictionary<string, WordPitchProfile> profiles;
    public PitchProfiles(string root)
    {
        string file = Path.Combine(root, "profiles.json");
        var collection = File.Exists(file) ? JsonSerializer.Deserialize<PitchCollection>(File.ReadAllText(file), StudioJson.Options) : null;
        if (collection is not null && collection.Version != 1) throw new InvalidDataException("Unsupported word pitch collection version.");
        profiles = collection?.Words.ToDictionary(p => p.Word.Key) ?? [];
    }
    public WordPitchProfile? For(CatalogWord word)
    {
        if (!profiles.TryGetValue(word.Key, out var value) || value.Word != word) return null;
        return value;
    }
    public static async Task PrepareAsync(StudioOptions options, WordCatalog catalog, CancellationToken token)
    {
        // Generate all frontend candidates without invoking a TTS voice or GPU.
        await using var generator = new JapanesePronunciationGenerator(new()
        {
            PythonExecutablePath = options.Speech.BertPython,
            BertDirectory = Path.Combine(options.Speech.ModelsDirectory, "style-bert-vits2-jp-extra", "bert"),
            RuntimeDirectory = Path.Combine(options.RuntimeRoot, "pitch-frontend")
        });
        var previous = new PitchProfiles(options.PitchRoot);
        var prepared = new List<WordPitchProfile>();
        foreach (var word in catalog.Words)
        {
            token.ThrowIfCancellationRequested();
            // Explicitly preserve reviewed/user-edited profiles during routine preparation.
            var existing = previous.For(word);
            if (existing?.Profile is { } prior && (existing.HumanVerified || prior.Provenance.Origin == JapanesePronunciationOrigin.UserEdited))
                prepared.Add(existing);
            else
            {
                try
                {
                    var profile = await generator.GenerateAsync(new() { Text = word.SpokenJapanese, KanaReading = word.SpokenKana }, token);
                    prepared.Add(new(word, profile, null));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    prepared.Add(new(word, null, e.Message));
                    Console.Error.WriteLine($"{word.Key}: {e.Message}");
                }
            }
            if (prepared.Count % 50 == 0) Console.WriteLine($"Prepared {prepared.Count}/{catalog.Words.Count} pitch candidates");
        }
        var collection = new PitchCollection(1,
            "Machine-estimated JP-Extra phone/tone profiles. Consumer word linkage, separate from study catalogs. Not native-verified; inspect pronunciation and accent before using for learning.", prepared);
        StudioStore.AtomicWrite(Path.Combine(options.PitchRoot, "profiles.json"), collection);
        int successful = prepared.Count(p => p.Profile is not null);
        Console.WriteLine($"Saved {successful}/{prepared.Count} usable profiles to {options.PitchRoot}");
        if (successful != prepared.Count) throw new InvalidDataException($"{prepared.Count - successful} entries need reading review; see each stored error.");
    }
}
