using System.Diagnostics;
using System.Globalization;
using System.Text;
using GenerateAudioUsingTTS;

namespace AudioStudio;

/// <summary>Runs bounded local FFmpeg media operations.</summary>
public sealed class MediaTools
{
    private static readonly TimeSpan MediaTimeout = TimeSpan.FromMinutes(10);
    private const int MaxDiagnosticCharacters = 16 * 1024;

    private readonly string _ffmpeg;

    public MediaTools(string ffmpeg) => _ffmpeg = RequirePath(ffmpeg, nameof(ffmpeg));

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

}
