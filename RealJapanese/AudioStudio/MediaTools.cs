using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GenerateAudioUsingTTS;

namespace AudioStudio;

public sealed record TranscriptionInput(string Id, string Path, bool IsReference = false);

public sealed record TranscriptionOutput(string Id, string? Text, string? Error);

/// <summary>Runs bounded FFmpeg media operations and a single local Faster Whisper batch worker.</summary>
public sealed class MediaTools
{
    private static readonly TimeSpan MediaTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AsrStartupTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AsrPerInputTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumAsrTimeout = TimeSpan.FromDays(30);
    private const int MaxDiagnosticCharacters = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _ffmpeg;
    private readonly string _asrPython;
    private readonly string _asrModels;
    private readonly string _asrCache;
    private readonly string _transcriptionScript;
    private readonly string _runtimeRoot;

    public MediaTools(string ffmpeg, string asrPython, string asrModels, string asrCache,
        string transcriptionScript, string runtimeRoot)
    {
        _ffmpeg = RequirePath(ffmpeg, nameof(ffmpeg));
        _asrPython = RequirePath(asrPython, nameof(asrPython));
        _asrModels = RequirePath(asrModels, nameof(asrModels));
        _asrCache = RequirePath(asrCache, nameof(asrCache));
        _transcriptionScript = RequirePath(transcriptionScript, nameof(transcriptionScript));
        _runtimeRoot = RequirePath(runtimeRoot, nameof(runtimeRoot));
    }

    public async Task<double> CreateReferenceAsync(string source, string destination, double startSeconds,
        double durationSeconds, CancellationToken cancellationToken)
    {
        string input = ExistingFile(source, nameof(source));
        string output = DestinationPath(destination);
        if (!double.IsFinite(startSeconds) || startSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(startSeconds), "Start time must be finite and nonnegative.");
        if (!double.IsFinite(durationSeconds) || durationSeconds < 1 || durationSeconds > 30)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Reference duration must be between 1 and 30 seconds.");
        EnsureDoesNotExist(output);

        string temporary = TemporarySibling(output, ".wav");
        try
        {
            var info = NewProcess(_ffmpeg, Path.GetDirectoryName(output)!);
            info.RedirectStandardInput = false;
            AddArguments(info, "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-n",
                "-ss", Number(startSeconds), "-protocol_whitelist", "file,pipe", "-i", input,
                "-t", Number(durationSeconds), "-map", "0:a:0",
                "-vn", "-ac", "1", "-ar", "24000", "-c:a", "pcm_s16le", "-f", "wav", temporary);
            await RunProcessAsync(info, MediaTimeout, cancellationToken).ConfigureAwait(false);

            Wave wave = Wave.Read(temporary);
            if (wave.SampleRate != 24000)
                throw new InvalidDataException("FFmpeg did not create a 24 kHz reference WAV.");
            // FFmpeg can report a successful decode for a crop ending at EOF. Allow only
            // a small codec/frame-boundary tolerance; do not silently accept a short crop.
            if (wave.Seconds < durationSeconds - 0.1)
                throw new InvalidDataException("Reference excerpt extends past the end of the source audio.");
            _ = Audio.PrepareWord(wave); // Validate non-silent content without changing transcript alignment.
            File.Move(temporary, output);
            return wave.Seconds;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public async Task<double> ProcessWordAsync(string rawWav, string destination, string outputQuality,
        CancellationToken cancellationToken)
    {
        string input = ExistingFile(rawWav, nameof(rawWav));
        string output = DestinationPath(destination);
        EnsureDoesNotExist(output);
        string quality = outputQuality?.Trim() ?? "";
        string extension = quality switch
        {
            "OriginalWav" or "Wav24000Hz16BitMono" => ".wav",
            "Mp3Mono64Kbps" => ".mp3",
            "OpusMono32Kbps" => ".opus",
            _ => throw new ArgumentException("Unknown speech output quality.", nameof(outputQuality)),
        };
        if (!string.Equals(Path.GetExtension(output), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Destination extension must be {extension} for {quality}.", nameof(destination));

        string intermediate = TemporarySibling(output, ".wav");
        string encoded = TemporarySibling(output, extension);
        try
        {
            Wave raw = Wave.Read(input);
            (Wave prepared, _) = Audio.PrepareWord(raw);
            prepared.Write(intermediate);

            if (quality == "OriginalWav")
            {
                File.Move(intermediate, output);
                return prepared.Seconds;
            }

            var info = NewProcess(_ffmpeg, Path.GetDirectoryName(output)!);
            info.RedirectStandardInput = false;
            if (quality == "Wav24000Hz16BitMono")
                AddArguments(info, "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-n",
                    "-protocol_whitelist", "file,pipe", "-i", intermediate, "-map", "0:a:0", "-vn", "-ac", "1", "-ar", "24000",
                    "-c:a", "pcm_s16le", "-f", "wav", encoded);
            else if (quality == "Mp3Mono64Kbps")
                AddArguments(info, "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-n",
                    "-protocol_whitelist", "file,pipe", "-i", intermediate, "-map", "0:a:0", "-vn", "-ac", "1", "-ar", "24000",
                    "-c:a", "libmp3lame", "-b:a", "64k", "-f", "mp3", encoded);
            else
                AddArguments(info, "-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-n",
                    "-protocol_whitelist", "file,pipe", "-i", intermediate, "-map", "0:a:0", "-vn", "-ac", "1", "-ar", "48000",
                    "-c:a", "libopus", "-b:a", "32k", "-vbr", "off", "-f", "ogg", encoded);

            await RunProcessAsync(info, MediaTimeout, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(encoded) || new FileInfo(encoded).Length == 0)
                throw new InvalidDataException("FFmpeg produced no encoded audio.");
            File.Move(encoded, output);
            return prepared.Seconds;
        }
        finally
        {
            TryDelete(intermediate);
            TryDelete(encoded);
        }
    }

    public async Task TranscribeAsync(IReadOnlyList<TranscriptionInput> inputs,
        Func<TranscriptionOutput, Task> onResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(onResult);
        if (inputs.Count == 0) return;
        foreach (TranscriptionInput input in inputs)
        {
            if (input is null) throw new ArgumentException("Transcription inputs cannot contain null.", nameof(inputs));
            if (string.IsNullOrWhiteSpace(input.Id)) throw new ArgumentException("Each transcription input needs an ID.", nameof(inputs));
            _ = RequirePath(input.Path, nameof(inputs));
        }

        Directory.CreateDirectory(_runtimeRoot);
        Directory.CreateDirectory(_asrCache);
        var info = NewProcess(_asrPython, _runtimeRoot);
        AddArguments(info, _transcriptionScript, "--models", _asrModels, "--cache", _asrCache, "--runtime", _runtimeRoot);
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["HF_HOME"] = _asrCache;
        info.Environment["HF_HUB_OFFLINE"] = "1";
        info.Environment["TRANSFORMERS_OFFLINE"] = "1";
        info.Environment["TEMP"] = _runtimeRoot;
        info.Environment["TMP"] = _runtimeRoot;
        info.Environment["TMPDIR"] = _runtimeRoot;

        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the local transcription worker.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan asrTimeout = AsrTimeoutFor(inputs.Count);
        timeout.CancelAfter(asrTimeout);
        Task<string> diagnosticTask = ReadBoundedAsync(process.StandardError, MaxDiagnosticCharacters);
        Task outputTask = PumpTranscriptionOutputAsync(process.StandardOutput, inputs.Select(input => input.Id).ToArray(), onResult, timeout.Token);
        try
        {
            foreach (TranscriptionInput input in inputs)
            {
                string line = JsonSerializer.Serialize(new WorkerInput(input.Id, Path.GetFullPath(input.Path), input.IsReference), JsonOptions);
                await process.StandardInput.WriteLineAsync(line.AsMemory(), timeout.Token).ConfigureAwait(false);
            }
            process.StandardInput.Close();
            Task waitTask = process.WaitForExitAsync(timeout.Token);
            Task first = await Task.WhenAny(waitTask, outputTask).ConfigureAwait(false);
            if (first == outputTask) await outputTask.ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);
            await outputTask.ConfigureAwait(false);
            string diagnostics = await diagnosticTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Local transcription worker failed to initialize or finish: " + diagnostics.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await ObserveAsync(outputTask).ConfigureAwait(false);
            string diagnostics = await diagnosticTask.ConfigureAwait(false);
            throw new TimeoutException($"Local transcription batch exceeded its {asrTimeout.TotalMinutes:0}-minute timeout " + diagnostics.Trim());
        }
        catch
        {
            Kill(process);
            await ObserveAsync(outputTask).ConfigureAwait(false);
            await ObserveAsync(diagnosticTask).ConfigureAwait(false);
            throw;
        }
    }

    internal static async Task PumpTranscriptionOutputAsync(TextReader reader, IReadOnlyCollection<string> expectedIds,
        Func<TranscriptionOutput, Task> onResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(expectedIds);
        ArgumentNullException.ThrowIfNull(onResult);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? id in expectedIds)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Expected transcription IDs must not be empty.", nameof(expectedIds));
            if (!expected.Add(id)) throw new ArgumentException($"Expected transcription ID '{id}' is duplicated.", nameof(expectedIds));
        }

        var received = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            TranscriptionOutput output = JsonSerializer.Deserialize<TranscriptionOutput>(line, JsonOptions)
                ?? throw new InvalidDataException("Transcription worker emitted an empty JSON result.");
            if (string.IsNullOrWhiteSpace(output.Id))
                throw new InvalidDataException("Transcription worker emitted a result with an empty ID.");
            if (!expected.Contains(output.Id))
                throw new InvalidDataException($"Transcription worker emitted unexpected ID '{output.Id}'.");
            if (!received.Add(output.Id))
                throw new InvalidDataException($"Transcription worker emitted duplicate ID '{output.Id}'.");
            if (output.Text is null && output.Error is null)
                throw new InvalidDataException($"Transcription worker emitted neither text nor an error for '{output.Id}'.");
            await onResult(output).ConfigureAwait(false);
        }

        string[] missing = expected.Except(received, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"Transcription worker omitted {missing.Length} result(s): {string.Join(", ", missing)}.");
    }

    private static async Task RunProcessAsync(ProcessStartInfo info, TimeSpan duration,
        CancellationToken cancellationToken)
    {
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not start FFmpeg.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        Task<string> standardOutput = ReadBoundedAsync(process.StandardOutput, MaxDiagnosticCharacters);
        Task<string> diagnostics = ReadBoundedAsync(process.StandardError, MaxDiagnosticCharacters);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await standardOutput.ConfigureAwait(false);
            string message = await diagnostics.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}: {message.Trim()}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await ObserveAsync(standardOutput).ConfigureAwait(false);
            string message = await diagnostics.ConfigureAwait(false);
            throw new TimeoutException($"FFmpeg exceeded its {duration.TotalMinutes:0}-minute timeout: {message.Trim()}");
        }
        catch
        {
            Kill(process);
            await ObserveAsync(standardOutput).ConfigureAwait(false);
            await ObserveAsync(diagnostics).ConfigureAwait(false);
            throw;
        }
    }

    private static ProcessStartInfo NewProcess(string executable, string workingDirectory) => new(executable)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        WorkingDirectory = workingDirectory,
    };

    private static void AddArguments(ProcessStartInfo info, params string[] arguments)
    {
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximumCharacters)
    {
        var buffer = new char[1024];
        var tail = new StringBuilder(maximumCharacters);
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) != 0)
        {
            tail.Append(buffer, 0, read);
            if (tail.Length > maximumCharacters) tail.Remove(0, tail.Length - maximumCharacters);
        }
        return tail.ToString();
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task.ConfigureAwait(false); } catch { }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static string RequirePath(string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, name);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Path must be absolute.", name);
        return Path.GetFullPath(path);
    }

    private static string ExistingFile(string path, string name)
    {
        string fullPath = RequirePath(path, name);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Input file was not found.", fullPath);
        return fullPath;
    }

    private static string DestinationPath(string path)
    {
        string fullPath = RequirePath(path, nameof(path));
        string? parent = Path.GetDirectoryName(fullPath);
        if (parent is null) throw new ArgumentException("Destination must have a parent directory.", nameof(path));
        Directory.CreateDirectory(parent);
        return fullPath;
    }

    private static void EnsureDoesNotExist(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Destination already exists.");
    }

    private static string TemporarySibling(string destination, string extension)
    {
        return Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileNameWithoutExtension(destination)}.{Guid.NewGuid():N}.partial{extension}");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static string Number(double value) => value.ToString("0.#########", CultureInfo.InvariantCulture);

    private static TimeSpan AsrTimeoutFor(int inputCount)
    {
        double requestedMinutes = AsrStartupTimeout.TotalMinutes + inputCount * AsrPerInputTimeout.TotalMinutes;
        return TimeSpan.FromMinutes(Math.Min(requestedMinutes, MaximumAsrTimeout.TotalMinutes));
    }

    private sealed record WorkerInput(string Id, string Path, bool IsReference);
}
