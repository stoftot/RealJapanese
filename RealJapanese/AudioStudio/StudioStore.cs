using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using GenerateAudioUsingTTS;

namespace AudioStudio;

public sealed class StudioStore
{
    private readonly string workRoot;
    private readonly string exportRoot;
    private readonly WordCatalog catalog;
    private readonly object exportGate = new();
    private readonly object referenceGate = new();
    private readonly ConcurrentDictionary<string, StudioRun> runs = new();
    public string RunsRoot => Path.Combine(workRoot, "runs");
    public string ReferencesRoot => Path.Combine(workRoot, "references");

    public StudioStore(string workRoot, string exportRoot, WordCatalog catalog)
    {
        this.workRoot = workRoot;
        this.exportRoot = exportRoot;
        this.catalog = catalog;
        Paths.RejectLinks(workRoot);
        Paths.RejectLinks(exportRoot);
        Directory.CreateDirectory(RunsRoot);
        Directory.CreateDirectory(ReferencesRoot);
        foreach (string file in Directory.EnumerateFiles(RunsRoot, "run.json", SearchOption.AllDirectories))
        {
            var run = Read<StudioRun>(file);
            if (run is null) continue;
            ValidateId(run.Id);
            if (run.IsActive)
            {
                bool wasPausing = run.Status == "Pausing";
                run.Status = wasPausing ? "Paused" : "Interrupted";
                run.Phase = wasPausing ? "Paused; ready to continue" : "Interrupted by application shutdown";
                foreach (var clip in run.Clips.Where(c => c.Status == "Generating")) clip.Status = "Pending";
                AtomicWrite(file, run);
            }
            runs[run.Id] = run;
        }
    }

    public string RunFolder(string id) { ValidateId(id); return SafePath(RunsRoot, id); }
    public string ReferenceFolder(string id) { ValidateId(id); return SafePath(ReferencesRoot, id); }
    public string ReferenceAudio(string id) => SafePath(ReferenceFolder(id), "reference.wav");
    public static void ValidateId(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new StudioException("Unknown item.", 404);
    }
    public static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new StudioException("Invalid relative media path.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        Paths.RequireChild(full, root);
        if (File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            throw new StudioException("Linked media files are not supported.");
        return full;
    }
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static void AtomicWrite<T>(string path, T value)
    {
        Paths.RejectLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, StudioJson.Options), new System.Text.UTF8Encoding(false));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static T? Read<T>(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), StudioJson.Options) : default;

    public void AddRun(StudioRun run)
    {
        if (!runs.TryAdd(run.Id, run)) throw new StudioException("Run already exists.", 409);
        Directory.CreateDirectory(RunFolder(run.Id));
        lock (run) AtomicWrite(Path.Combine(RunFolder(run.Id), "run.json"), run);
    }
    private StudioRun Find(string id)
    {
        ValidateId(id);
        return runs.TryGetValue(id, out var run) ? run : throw new StudioException("Run not found.", 404);
    }
    public StudioRun GetRun(string id) { var run = Find(id); lock (run) return StudioJson.Clone(run); }
    public void UpdateRun(string id, Action<StudioRun> update)
    {
        var run = Find(id);
        lock (run)
        {
            update(run);
            AtomicWrite(Path.Combine(RunFolder(id), "run.json"), run);
        }
    }
    public IEnumerable<StudioRun> Runs() => runs.Keys.Select(GetRun).OrderByDescending(r => r.CreatedAt).ToArray();
    public StudioRun Select(string id, Dictionary<string, string?> choices)
    {
        UpdateRun(id, run =>
        {
            foreach (var (key, choice) in choices)
            {
                if (!run.Words.Any(w => w.Key == key)) throw new StudioException("Word is not in this run.");
                if (choice is not null && choice != "dont-use" && !run.Clips.Any(c => c.Key == key && c.Engine == choice && c.File is not null))
                    throw new StudioException("Choose a generated voice or Don't use.");
            }
            foreach (var (key, choice) in choices)
                if (choice is null) run.Selections.Remove(key); else run.Selections[key] = choice;
        });
        return GetRun(id);
    }
    public StudioRun SetQuality(string id, string key, string engine, string quality)
    {
        if (!ClipFormats.Qualities.Contains(quality)) throw new StudioException("Unknown output quality.");
        UpdateRun(id, run =>
        {
            var clip = run.Clips.SingleOrDefault(c => c.Key == key && c.Engine == engine)
                ?? throw new StudioException("Clip not found.", 404);
            var variant = ClipFormats.Resolve(run, clip, quality);
            string file = SafePath(RunFolder(id), variant.File);
            if (!File.Exists(file) || Hash(file) != variant.Sha256)
                throw new StudioException("The selected quality is missing or changed.", 409);
            clip.SelectedQuality = quality;
        });
        return GetRun(id);
    }
    public IReadOnlyList<ReferenceItem> References()
    {
        lock (referenceGate) return Directory.EnumerateFiles(ReferencesRoot, "reference.json", SearchOption.AllDirectories)
            .Select(Read<ReferenceItem>).Where(r => r is not null).Cast<ReferenceItem>().OrderBy(r => r.CreatedAt).ToArray();
    }
    public ReferenceItem Reference(string id)
    {
        lock (referenceGate) return Read<ReferenceItem>(Path.Combine(ReferenceFolder(id), "reference.json"))
            ?? throw new StudioException("Reference not found.", 404);
    }
    public void SaveReference(ReferenceItem item)
    {
        lock (referenceGate) AtomicWrite(Path.Combine(ReferenceFolder(item.Id), "reference.json"), item);
    }
    public IReadOnlyList<ExportedClip> Exports()
    {
        lock (exportGate) return Read<List<ExportedClip>>(Path.Combine(exportRoot, "manifest.json")) ?? [];
    }
    public bool HasAudio(CatalogWord word) => Exports().Any(e => e.Word == word && File.Exists(SafePath(exportRoot, e.File)));
    public string ExportAudio(string key)
    {
        var entry = Exports().SingleOrDefault(e => e.Word.Key == key) ?? throw new StudioException("Export not found.", 404);
        return SafePath(exportRoot, entry.File);
    }
    public ExportResult Export(string id, bool acknowledgeUnselected)
    {
        lock (exportGate)
        {
            StudioRun run = GetRun(id);
            if (run.IsActive || run.CanResume) throw new StudioException("Finish or cancel this run before exporting.", 409);
            int unselected = run.Words.Count(w => !run.Selections.ContainsKey(w.Key));
            if (unselected > 0 && !acknowledgeUnselected)
                throw new StudioException($"{unselected} words have no choice. Export only the selected clips?", 409, unselected);
            var selected = run.Words.Where(w => run.Selections.TryGetValue(w.Key, out var choice) && choice != "dont-use")
                .Select(w =>
                {
                    var clip = run.Clips.Single(c => c.Key == w.Key && c.Engine == run.Selections[w.Key]);
                    return (word: w, clip, quality: clip.SelectedQuality ?? run.OutputQuality, audio: ClipFormats.Resolve(run, clip));
                }).ToArray();
            foreach (var (word, clip, quality, audio) in selected)
            {
                catalog.VerifyCurrent(word);
                string source = SafePath(RunFolder(id), audio.File);
                if (!File.Exists(source) || Hash(source) != audio.Sha256) throw new StudioException("A selected clip is missing or changed. Generate it again.", 409);
            }
            var entries = Exports().ToDictionary(e => e.Word.Key);
            var created = new List<string>();
            try
            {
                foreach (var (word, clip, quality, audio) in selected)
                {
                    string source = SafePath(RunFolder(id), audio.File);
                    // Immutable payload names make the manifest replacement the commit point.
                    string relative = $"{word.Dataset}/{word.Id}/{Guid.NewGuid():N}{Path.GetExtension(source)}";
                    string target = SafePath(exportRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target, false);
                    created.Add(target);
                    entries[word.Key] = new(word, clip.Engine, id, quality, relative, audio.Sha256, DateTimeOffset.UtcNow, audio.Transcript, audio.Validation);
                }
                if (selected.Length > 0) AtomicWrite(Path.Combine(exportRoot, "manifest.json"), entries.Values.OrderBy(e => e.Word.Key).ToArray());
            }
            catch
            {
                foreach (string path in created) File.Delete(path);
                throw;
            }
            return new(selected.Length, unselected);
        }
    }
}
