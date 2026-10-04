using System.Text;
using System.Globalization;
using Genki.Generation;
#if AILIBRARY
using AiLibrary.LlamaServer;
#endif

namespace Genki.Tools;

public sealed record AiLibraryOptions
{
    public required string ModelsDirectory { get; init; }
    public string? ServerExecutable { get; init; }
    public string QwenModelId { get; init; } = "qwen2.5-14b-instruct";
    public string TranslationModelId { get; init; } = "lfm2-enjp-translation";
    public string? QwenModelFile { get; init; }
    public string? TranslationModelFile { get; init; }
    public int QwenContextSize { get; init; } = 16384;
    public int TranslationContextSize { get; init; } = 4096;
    public double Temperature { get; init; }
    public int MaxTokens { get; init; } = 1024;
    public int TimeoutSeconds { get; init; } = 300;
    public int? GpuLayers { get; init; }
    public int MaxConcurrentRequests { get; init; } = 1;
}

public sealed class AiLibraryModels : IGenkiModels, IAsyncDisposable
{
    private readonly AiLibraryOptions _options;
    private readonly TimeSpan _timeout;

#if AILIBRARY
    private readonly AiLibrary.IAiModelService? _service;
#endif

    public AiLibraryModels(AiLibraryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);
        _timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

#if AILIBRARY
        var builder = AiLibrary.AiLibraryBuilder.Create()
            .UseModelsDirectory(options.ModelsDirectory)
            .UseLlamaServer(server => server.ExecutablePath = options.ServerExecutable)
            .AddQwenInstructModel(model => model with
            {
                Id = options.QwenModelId,
                FilePath = options.QwenModelFile is null ? model.FilePath : Path.GetFullPath(options.QwenModelFile, options.ModelsDirectory),
                ContextSize = options.QwenContextSize,
                GpuLayers = options.GpuLayers,
                RequestTimeout = _timeout,
            })
            .AddLfmEnglishJapaneseTranslationModel(model => model with
            {
                Id = options.TranslationModelId,
                FilePath = options.TranslationModelFile is null ? model.FilePath : Path.GetFullPath(options.TranslationModelFile, options.ModelsDirectory),
                ContextSize = options.TranslationContextSize,
                GpuLayers = options.GpuLayers,
                RequestTimeout = _timeout,
            })
            .Configure(library =>
            {
                library.EnablePromptLogging = false;
                library.Lifecycle.MaxConcurrentModels = 1;
                library.Lifecycle.MaxConcurrentRequests = options.MaxConcurrentRequests;
                library.Lifecycle.ShutdownWhenQueueIsEmpty = false;
            });

        _service = builder.Build();
#endif
    }

    public string Provenance => string.Join(
        "; ",
        "provider=AiLibrary.LlamaServer",
        $"qwen-model={_options.QwenModelId}",
        $"translation-model={_options.TranslationModelId}",
        $"qwen-file={_options.QwenModelFile ?? "AiLibrary profile default"}",
        $"translation-file={_options.TranslationModelFile ?? "AiLibrary profile default"}",
        $"qwen-context={_options.QwenContextSize}; translation-context={_options.TranslationContextSize}",
        $"models-directory={Path.GetFullPath(_options.ModelsDirectory)}",
        $"server-executable={(_options.ServerExecutable is { Length: > 0 } path ? path : "PATH lookup")}",
        $"temperature={_options.Temperature.ToString(CultureInfo.InvariantCulture)}",
        $"max-tokens={_options.MaxTokens}",
        $"timeout-seconds={_options.TimeoutSeconds}",
        $"gpu-layers={(_options.GpuLayers?.ToString(CultureInfo.InvariantCulture) ?? "backend default")}");

    public async Task<string> QwenAsync(
        string stage,
        string systemPrompt,
        string inputJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputJson);
        cancellationToken.ThrowIfCancellationRequested();

#if AILIBRARY
        var service = GetService();
        var result = await service.GenerateAsync(new AiLibrary.GenerationRequest
        {
            ModelId = _options.QwenModelId,
            Messages =
            [
                new AiLibrary.ChatMessage(
                    AiLibrary.ChatRole.System,
                    $"Workflow stage: {stage}{Environment.NewLine}{systemPrompt}"),
                new AiLibrary.ChatMessage(AiLibrary.ChatRole.User, inputJson),
            ],
            Options = new AiLibrary.GenerationOptions
            {
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens,
                Timeout = _timeout,
            },
        }, cancellationToken).ConfigureAwait(false);

        return result.Text;
#else
        throw MissingAiLibrary();
#endif
    }

    public async Task<string> TranslateAsync(
        string japanese,
        string? setting,
        string register,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(japanese);
        ArgumentException.ThrowIfNullOrWhiteSpace(register);
        cancellationToken.ThrowIfCancellationRequested();

#if AILIBRARY
        var service = GetService();
        var systemPrompt = new StringBuilder()
            .AppendLine("Translate the Japanese sentence into natural English. Return only the translation.")
            .AppendLine("Preserve the sentence's meaning. Do not add facts, explanations, or details absent from the sentence.")
            .AppendLine($"Register: {register}");
        if (!string.IsNullOrWhiteSpace(setting))
        {
            systemPrompt.AppendLine($"Setting for resolving references and context: {setting}");
        }

        // TranslationRequest exposes no per-request context prompt, so pass the
        // context as system guidance and keep the sentence as the only user text.
        var result = await service.GenerateAsync(new AiLibrary.GenerationRequest
        {
            ModelId = _options.TranslationModelId,
            Messages =
            [
                new AiLibrary.ChatMessage(AiLibrary.ChatRole.System, systemPrompt.ToString()),
                new AiLibrary.ChatMessage(AiLibrary.ChatRole.User, japanese),
            ],
            Options = new AiLibrary.GenerationOptions
            {
                Temperature = 0,
                MaxTokens = _options.MaxTokens,
                Timeout = _timeout,
            },
        }, cancellationToken).ConfigureAwait(false);

        return result.Text;
#else
        throw MissingAiLibrary();
#endif
    }

    public async ValueTask DisposeAsync()
    {
#if AILIBRARY
        if (_service is not null)
        {
            await _service.DisposeAsync().ConfigureAwait(false);
        }
#endif
    }

    private static void ValidateOptions(AiLibraryOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelsDirectory);
        if (!Path.IsPathFullyQualified(options.ModelsDirectory))
        {
            throw new ArgumentException("ModelsDirectory must be an absolute path.", nameof(options));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(options.QwenModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TranslationModelId);
        if (!double.IsFinite(options.Temperature) || options.Temperature < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Temperature must be finite and nonnegative.");
        }
        if (options.MaxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxTokens must be positive.");
        }
        if (options.QwenContextSize <= options.MaxTokens || options.TranslationContextSize <= options.MaxTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Model context sizes must leave room beyond MaxTokens for the input prompt.");
        }
        if (options.TimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "TimeoutSeconds must be positive.");
        }
        if (options.GpuLayers is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "GpuLayers must be nonnegative when provided.");
        }
        if (options.MaxConcurrentRequests != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "AiLibrary currently supports one concurrent model request; MaxConcurrentRequests must be 1.");
        }
    }

    private static NotSupportedException MissingAiLibrary()
        => new(
            "Live model support is unavailable because AiLibrary was not included at build time. " +
            "Set AiLibraryRoot (or AI_LIBRARY_ROOT) to the Ai library checkout and rebuild Genki.Tools.");

#if AILIBRARY
    private AiLibrary.IAiModelService GetService()
    {
        return _service ?? throw new InvalidOperationException("AiLibrary service was not initialized.");
    }
#endif
}
