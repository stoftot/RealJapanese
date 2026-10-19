using System.Diagnostics;
using System.Globalization;

namespace GenerateAudioUsingTTS;

public static class ReferenceClip
{
    public static async Task<string> PrepareAsync(Settings settings, string output, CancellationToken cancellation)
    {
        string path = Path.Combine(output, "reference.wav");
        var start = new ProcessStartInfo(settings.Ffmpeg)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
            RedirectStandardOutput = true, WorkingDirectory = output
        };
        foreach (string arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-ss",
            settings.ReferenceStartSeconds.ToString(CultureInfo.InvariantCulture), "-i", settings.ReferenceAudio,
            "-t", settings.ReferenceDurationSeconds.ToString(CultureInfo.InvariantCulture), "-vn", "-ac", "1", "-ar", "24000", "-c:a", "pcm_s16le", path })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start FFmpeg.");
        var errorTask = process.StandardError.ReadToEndAsync(cancellation);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellation);
        try { await process.WaitForExitAsync(cancellation); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        string error = await errorTask;
        await outputTask;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Reference conversion failed: {error}");
        Wave wave = Wave.Read(path);
        if (wave.Seconds < settings.ReferenceDurationSeconds - 0.1)
            throw new InvalidDataException("Reference excerpt extends past the end of the input.");
        _ = Audio.PrepareWord(wave); // Detect silence without changing transcript alignment.
        return path;
    }
}
