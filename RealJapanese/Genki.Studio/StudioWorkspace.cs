using System.Text.Json;
using Genki.Generation;
using Genki.Inference;
using Repositories.Genki;

namespace Genki.Studio;

public interface IStudioModelFactory
{
    bool IsAvailable { get; }
    IGenkiModelSession Create(AiLibraryOptions options);
}
public sealed class StudioModelFactory : IStudioModelFactory
{
    public bool IsAvailable => AiLibraryModels.IsAvailable;
    public IGenkiModelSession Create(AiLibraryOptions options) => new AiLibraryModels(options);
}

public sealed class StudioWorkspace(string configurationFile, string repositoryRoot, GenkiCatalog? catalog = null)
{
    public string ConfigurationFile { get; } = Path.GetFullPath(configurationFile);
    public GenkiCatalog Catalog { get; } = catalog ?? new();
    public GenkiConfiguration Defaults => new()
    {
        DataRoot = Path.Combine(repositoryRoot, "RealJapanese", "Data"),
        StateRoot = Path.Combine(repositoryRoot, ".tooling", "genki-state"),
        Models = new() { ModelsDirectory = Environment.GetEnvironmentVariable("AI_MODELS_DIRECTORY") ?? Environment.GetEnvironmentVariable("AI_MODELS_PATH") ?? "", MaxTokens = 2048 },
        Batch = new() { TagGroupSize = 8 }
    };
    public GenkiConfiguration Load() => (BatchStore.ReadFile<GenkiConfiguration>(ConfigurationFile) ?? Defaults)
        .Resolve(Path.GetDirectoryName(ConfigurationFile)!);
    public void Save(GenkiConfiguration config) => BatchStore.WriteAtomic(ConfigurationFile, config);

    public GenkiVocabulary Validate(GenkiConfiguration config, bool inference)
    {
        config.Batch.Validate();
        if (string.IsNullOrWhiteSpace(config.DataRoot) || string.IsNullOrWhiteSpace(config.StateRoot))
            throw new InvalidDataException("Choose both the source data and generated state folders.");
        config.Resolve(Path.GetDirectoryName(ConfigurationFile)!);
        var vocabulary = GenkiVocabulary.Load(config.DataRoot);
        Catalog.ValidateVocabularyReferences(vocabulary);
        if (inference)
        {
            AiLibraryModels.ValidateOptions(config.Models);
            foreach (var model in new[] { config.Models.QwenModelFile ?? "Qwen2.5-14B-Instruct-Q4_K_M.gguf",
                config.Models.TranslationModelFile ?? "LFM2-350M-ENJP-MT-F16.gguf" })
            {
                var path = Path.GetFullPath(model, config.Models.ModelsDirectory);
                if (!File.Exists(path)) throw new InvalidDataException($"Model file was not found: {path}");
            }
            if (config.Models.ServerExecutable is { Length: > 0 } executable &&
                (Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar)) && !File.Exists(executable))
                throw new InvalidDataException("The llama-server executable was not found.");
        }
        return vocabulary;
    }
    public static void ValidatePublication(GenkiConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.PublishPath) || !config.PublishPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) ||
            GenkiConfiguration.IsWithin(config.PublishPath, config.StateRoot))
            throw new InvalidDataException("Choose a .jsonl question bank outside the generated state folder.");
    }
    public static string Fingerprint(GenkiCatalog catalog, GenkiVocabulary vocabulary, object annotations) =>
        GenkiJson.Fingerprint(new { catalog.Schemas, words = vocabulary.Entries.Select(w => w.Fingerprint), annotations });
    public static GenkiConfiguration FromSettings(StudioSettings s) => new()
    {
        DataRoot = s.DataRoot, StateRoot = s.StateRoot, PublishPath = s.PublishPath,
        Models = new()
        {
            ModelsDirectory = s.ModelsDirectory, ServerExecutable = Blank(s.ServerExecutable), QwenModelId = s.QwenModelId,
            TranslationModelId = s.TranslationModelId, QwenModelFile = Blank(s.QwenModelFile), TranslationModelFile = Blank(s.TranslationModelFile),
            QwenContextSize = s.QwenContextSize, TranslationContextSize = s.TranslationContextSize, Temperature = s.Temperature,
            MaxTokens = s.MaxTokens, TimeoutSeconds = s.TimeoutSeconds, GpuLayers = s.GpuLayers
        },
        Batch = new() { MaxAttempts = s.MaxAttempts, TagGroupSize = s.TagGroupSize, AlternativeLimit = s.AlternativeLimit, DelayMilliseconds = s.DelayMilliseconds }
    };
    public static StudioSettings ToSettings(GenkiConfiguration c) => new()
    {
        DataRoot = c.DataRoot, StateRoot = c.StateRoot, PublishPath = c.PublishPath ?? "", ModelsDirectory = c.Models.ModelsDirectory,
        ServerExecutable = c.Models.ServerExecutable ?? "", QwenModelId = c.Models.QwenModelId, TranslationModelId = c.Models.TranslationModelId,
        QwenModelFile = c.Models.QwenModelFile ?? "", TranslationModelFile = c.Models.TranslationModelFile ?? "",
        QwenContextSize = c.Models.QwenContextSize, TranslationContextSize = c.Models.TranslationContextSize, Temperature = c.Models.Temperature,
        MaxTokens = c.Models.MaxTokens, TimeoutSeconds = c.Models.TimeoutSeconds, GpuLayers = c.Models.GpuLayers,
        MaxAttempts = c.Batch.MaxAttempts, TagGroupSize = c.Batch.TagGroupSize, AlternativeLimit = c.Batch.AlternativeLimit, DelayMilliseconds = c.Batch.DelayMilliseconds
    };
    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
