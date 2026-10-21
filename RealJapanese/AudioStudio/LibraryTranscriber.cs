using AiLibrary;
using AiLibrary.Speech;

namespace AudioStudio;

public sealed record LibraryTranscriptionOutput(
    string Id,
    string? Text,
    string? Error,
    string? ModelId,
    string? Language,
    IReadOnlyDictionary<string, string>? Diagnostics);

/// <summary>Keeps one local Faster Whisper small model warm for sequential clip transcription.</summary>
public sealed class LibraryTranscriber : IAsyncDisposable
{
    public const string WhisperModelId = "whisper-small";

    private readonly IAiModelService _service;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private bool _disposed;

    private LibraryTranscriber(IAiModelService service) => _service = service;

    public static LibraryTranscriber Create(string pythonExecutablePath, string modelsDirectory, string runtimeRoot)
    {
        string python = RequireExistingFile(pythonExecutablePath, nameof(pythonExecutablePath));
        string models = RequireExistingDirectory(modelsDirectory, nameof(modelsDirectory));
        string runtime = RequireAbsolutePath(runtimeRoot, nameof(runtimeRoot));
        string whisperModel = Path.Combine(models, "whisper-small");
        if (!Directory.Exists(whisperModel))
            throw new DirectoryNotFoundException($"The existing Faster Whisper model folder was not found: {whisperModel}");

        Directory.CreateDirectory(runtime);
        // AiLibrary's transcription adapter stores its private worker/cache in
        // Path.GetTempPath(). Direct that framework-owned state to the app workspace.
        Environment.SetEnvironmentVariable("TEMP", runtime);
        Environment.SetEnvironmentVariable("TMP", runtime);
        Environment.SetEnvironmentVariable("TMPDIR", runtime);

        IAiModelService service = AiLibraryBuilder.Create()
            .UseModelsDirectory(models)
            .AddWhisperModel(options =>
            {
                options.ModelName = "small";
                options.ModelId = WhisperModelId;
                options.ModelDirectoryPath = whisperModel;
                options.PythonExecutablePath = python;
                options.Device = "cpu";
                options.ComputeType = "int8";
                options.CpuThreads = 6;
            })
            .Configure(options => options.Lifecycle.ShutdownWhenQueueIsEmpty = false)
            .Build();
        return new LibraryTranscriber(service);
    }

    public async Task<LibraryTranscriptionOutput> TranscribeOneAsync(string id, string path,
        bool isReference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A transcription ID is required.", nameof(id));
        cancellationToken.ThrowIfCancellationRequested();
        string input;
        try { input = RequireExistingFile(path, nameof(path)); }
        catch (Exception error)
        {
            return new LibraryTranscriptionOutput(id, null, error.Message, WhisperModelId, null, null);
        }
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var request = new TranscriptionRequest
            {
                ModelId = WhisperModelId,
                AudioFilePath = input,
                Options = new TranscriptionOptions
                {
                    Language = "ja",
                    BeamSize = 5,
                    Temperature = 0,
                    ConditionOnPreviousText = false,
                    WordTimestamps = false,
                    VadFilter = isReference,
                    // InitialPrompt intentionally remains null: do not bias ASR toward
                    // the catalog's expected word or the generated target.
                },
            };
            try
            {
                TranscriptionResult result = await _service.TranscribeAsync(request, cancellationToken).ConfigureAwait(false);
                return new LibraryTranscriptionOutput(id, result.Text, null, result.ModelId, result.Language,
                    result.Diagnostics.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                return new LibraryTranscriptionOutput(id, null, error.Message, WhisperModelId, null, null);
            }
        }
        finally { _lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await _service.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private static string RequireExistingFile(string path, string name)
    {
        string fullPath = RequireAbsolutePath(path, name);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Required local file was not found.", fullPath);
        return fullPath;
    }

    private static string RequireExistingDirectory(string path, string name)
    {
        string fullPath = RequireAbsolutePath(path, name);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"Required directory was not found: {fullPath}");
        return fullPath;
    }

    private static string RequireAbsolutePath(string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, name);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Path must be absolute.", name);
        return Path.GetFullPath(path);
    }
}
