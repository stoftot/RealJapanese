using System.Buffers.Binary;

namespace GenerateAudioUsingTTS;

public sealed record Wave(int SampleRate, short[] Samples)
{
    private const int MaxWaveBytes = 100 * 1024 * 1024;

    public double Seconds => (double)Samples.Length / SampleRate;

    public static Wave Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("WAV file was not found.", path);
        if (info.Length > MaxWaveBytes) throw new InvalidDataException("WAV files are limited to 100 MB.");
        if (info.Length < 12) throw new InvalidDataException("WAV file is shorter than a RIFF header.");

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (ReadFourCc(reader) != "RIFF") throw new InvalidDataException("Expected a RIFF file.");
        var riffSize = reader.ReadUInt32();
        if (ReadFourCc(reader) != "WAVE") throw new InvalidDataException("Expected a WAVE form.");
        var riffEnd = checked(8L + riffSize);
        if (riffEnd < 12 || riffEnd > stream.Length) throw new InvalidDataException("RIFF length is truncated or invalid.");

        int? sampleRate = null;
        bool? validFormat = null;
        byte[]? data = null;
        while (stream.Position + 8 <= riffEnd)
        {
            var id = ReadFourCc(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkEnd = checked(stream.Position + chunkSize);
            if (chunkEnd > riffEnd) throw new InvalidDataException($"The {id} chunk is truncated.");

            if (id == "fmt ")
            {
                if (chunkSize < 16) throw new InvalidDataException("The fmt chunk is too short.");
                var format = reader.ReadUInt16();
                var channels = reader.ReadUInt16();
                var rate = reader.ReadUInt32();
                var byteRate = reader.ReadUInt32();
                var blockAlign = reader.ReadUInt16();
                var bitsPerSample = reader.ReadUInt16();
                if (rate is 0 or > int.MaxValue || format != 1 || channels != 1 || bitsPerSample != 16
                    || blockAlign != 2 || byteRate != rate * 2)
                    throw new InvalidDataException("Only mono PCM16 WAV files are supported.");
                sampleRate = (int)rate;
                validFormat = true;
            }
            else if (id == "data")
            {
                if (chunkSize > MaxWaveBytes) throw new InvalidDataException("WAV audio is limited to 100 MB.");
                if ((chunkSize & 1) != 0) throw new InvalidDataException("PCM16 data must contain complete samples.");
                data = reader.ReadBytes((int)chunkSize);
                if (data.Length != chunkSize) throw new InvalidDataException("The data chunk is truncated.");
            }

            stream.Position = chunkEnd;
            if ((chunkSize & 1) != 0)
            {
                if (stream.Position >= riffEnd) throw new InvalidDataException("A padded chunk is truncated.");
                stream.Position++;
            }
        }

        if (stream.Position != riffEnd) throw new InvalidDataException("RIFF contains an incomplete chunk header.");
        if (validFormat != true || sampleRate is null) throw new InvalidDataException("WAV is missing a PCM16 fmt chunk.");
        if (data is null || data.Length == 0) throw new InvalidDataException("WAV contains no audio samples.");

        var samples = new short[data.Length / 2];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2));
        return new Wave(sampleRate.Value, samples);
    }

    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Validate();
        var dataBytes = checked(Samples.Length * 2);
        var riffBytes = checked(36L + dataBytes);
        if (riffBytes + 8 > MaxWaveBytes) throw new InvalidDataException("WAV files are limited to 100 MB.");

        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write((uint)riffBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16u);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((uint)SampleRate);
        writer.Write((uint)(SampleRate * 2));
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write((uint)dataBytes);
        Span<byte> buffer = stackalloc byte[2];
        foreach (var sample in Samples)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer, sample);
            writer.Write(buffer);
        }
    }

    private void Validate()
    {
        if (SampleRate <= 0 || SampleRate > int.MaxValue / 2)
            throw new InvalidDataException("Sample rate is invalid.");
        if (Samples is null || Samples.Length == 0)
            throw new InvalidDataException("WAV must contain audio samples.");
    }

    private static string ReadFourCc(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length != 4) throw new InvalidDataException("RIFF chunk header is truncated.");
        return System.Text.Encoding.ASCII.GetString(bytes);
    }
}

public sealed record AudioStats(double RawSeconds, double Seconds, int SampleRate, double SourcePeak, double Gain,
    List<string> Warnings);

public static class Audio
{
    private const double ActivityThreshold = 0.0031622776601683794; // -50 dBFS
    private const double TargetPeak = 0.7079457843841379; // -3 dBFS
    private const int MaxWaveBytes = 100 * 1024 * 1024;

    public static (Wave Audio, AudioStats Stats) PrepareWord(Wave source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SampleRate <= 0 || source.SampleRate > int.MaxValue / 2)
            throw new InvalidDataException("Sample rate is invalid.");
        if (source.Samples is null || source.Samples.Length == 0)
            throw new InvalidDataException("Audio contains no samples.");
        if ((long)source.Samples.Length * 2 + 44 > MaxWaveBytes)
            throw new InvalidDataException("WAV audio is limited to 100 MB.");

        var rawSeconds = source.Seconds;
        var sourcePeakSample = source.Samples.Max(sample => Math.Abs((int)sample));
        var sourcePeak = sourcePeakSample / 32768d;
        var samplesPerWindow = Math.Max(1, (source.SampleRate + 99) / 100);
        var firstActive = -1;
        var lastActive = -1;
        for (var windowStart = 0; windowStart < source.Samples.Length; windowStart += samplesPerWindow)
        {
            var windowEnd = Math.Min(source.Samples.Length, windowStart + samplesPerWindow);
            double squares = 0;
            for (var i = windowStart; i < windowEnd; i++)
            {
                var normalized = source.Samples[i] / 32768d;
                squares += normalized * normalized;
            }
            var rms = Math.Sqrt(squares / (windowEnd - windowStart));
            if (rms > ActivityThreshold)
            {
                if (firstActive < 0) firstActive = windowStart;
                lastActive = windowEnd;
            }
        }

        if (firstActive < 0) throw new InvalidDataException("Audio is silent.");
        var leading = (int)Math.Ceiling(source.SampleRate * 0.1);
        var trailing = (int)Math.Ceiling(source.SampleRate * 0.15);
        var start = Math.Max(0, firstActive - leading);
        var end = Math.Min(source.Samples.Length, lastActive + trailing);
        var trimmed = source.Samples.AsSpan(start, end - start).ToArray();
        var trimPeak = trimmed.Max(sample => Math.Abs((int)sample)) / 32768d;
        var gain = trimPeak == 0 ? 1 : Math.Min(3, TargetPeak / trimPeak);
        var output = new short[trimmed.Length];
        for (var i = 0; i < trimmed.Length; i++)
            output[i] = (short)Math.Clamp((int)Math.Round(trimmed[i] * gain), short.MinValue, short.MaxValue);

        var warnings = new List<string>();
        if (rawSeconds < 0.2) warnings.Add("Source audio is shorter than 0.2 seconds.");
        if (rawSeconds > 3) warnings.Add("Source audio is longer than 3 seconds.");
        var nearFullScale = source.Samples.Count(sample => Math.Abs((int)sample) >= 32700);
        if (nearFullScale / (double)source.Samples.Length > 0.001)
            warnings.Add("More than 0.1% of source samples are near full scale and may be clipped.");

        var wave = new Wave(source.SampleRate, output);
        return (wave, new AudioStats(rawSeconds, wave.Seconds, source.SampleRate, sourcePeak, gain, warnings));
    }
}
