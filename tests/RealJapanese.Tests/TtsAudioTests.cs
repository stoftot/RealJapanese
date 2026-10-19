using System.Buffers.Binary;
using System.Text;
using GenerateAudioUsingTTS;

namespace RealJapanese.Tests;

/// <summary>Checks WAV I/O, speech trimming, normalization, and malformed audio handling.</summary>
public sealed class TtsAudioTests
{
    [Fact]
    public void WriteAndRead_RoundTripPcm16AndRefuseToOverwrite()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "sample.wav");
        var source = new Wave(24_000, [-32768, -1234, 0, 2345, 32767]);

        source.Write(path);
        var result = Wave.Read(path);

        Assert.Equal(source.SampleRate, result.SampleRate);
        Assert.Equal(source.Samples, result.Samples);
        Assert.Equal(source.Samples.Length / (double)source.SampleRate, result.Seconds);
        Assert.Throws<IOException>(() => source.Write(path));
    }

    [Fact]
    public void Read_SkipsUnknownOddSizedChunksAndTheirPadding()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "chunks.wav");
        WriteWave(path, channels: 1, sampleRate: 8_000, [12, -34], includeOddJunkChunk: true);

        var result = Wave.Read(path);

        Assert.Equal(new short[] { 12, -34 }, result.Samples);
    }

    [Fact]
    public void PrepareWord_TrimsOnlyOuterSilenceAndKeepsQuietOnsetNearTheActivityBoundary()
    {
        const int rate = 1_000;
        var samples = new short[1_000];
        samples[400] = 90; // Below the activity threshold, but inside the preserved 100 ms lead.
        Array.Fill(samples, (short)8_000, 450, 35);
        Array.Fill(samples, (short)-6_000, 650, 35);
        var source = new Wave(rate, samples);

        var (audio, stats) = Audio.PrepareWord(source);

        Assert.InRange(audio.Seconds, 0.485, 0.495);
        Assert.Equal(1, stats.RawSeconds);
        Assert.InRange(stats.Seconds, 0.485, 0.495);
        // Allow one PCM rounding step, rather than rounding a ratio at a decimal midpoint.
        Assert.InRange(Math.Abs(90d / 8_000 - audio.Samples[50] / (double)audio.Samples[100]), 0, 0.000025);
        Assert.All(audio.Samples.Skip(135).Take(165), sample => Assert.Equal((short)0, sample));
        Assert.Equal(-3, 20 * Math.Log10(audio.Samples.Max(s => Math.Abs((int)s)) / 32768d), 1);
    }

    [Fact]
    public void PrepareWord_LongActiveSpeechWarnsWithoutChoppingIt()
    {
        var samples = Enumerable.Repeat((short)5_000, 4_000).ToArray();

        var (audio, stats) = Audio.PrepareWord(new Wave(1_000, samples));

        Assert.Equal(4, audio.Seconds);
        Assert.Equal(4, stats.RawSeconds);
        Assert.Contains(stats.Warnings, warning => warning.Contains("longer than 3", StringComparison.Ordinal));
    }

    [Fact]
    public void PrepareWord_RejectsSilence()
    {
        Assert.Throws<InvalidDataException>(() => Audio.PrepareWord(new Wave(16_000, new short[1_600])));
    }

    [Fact]
    public void Read_RejectsEmptyDataChunk()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "empty.wav");
        WriteWave(path, channels: 1, sampleRate: 8_000, []);

        Assert.Throws<InvalidDataException>(() => Wave.Read(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_RejectsTruncatedOrStereoWave(bool stereo)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bad.wav");
        if (stereo)
            WriteWave(path, channels: 2, sampleRate: 8_000, [12, -34]);
        else
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("RIFF\x10\0\0\0WAVEfmt "));

        Assert.Throws<InvalidDataException>(() => Wave.Read(path));
    }

    private static void WriteWave(string path, ushort channels, int sampleRate, short[] samples,
        bool includeOddJunkChunk = false)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var junkSize = includeOddJunkChunk ? 10 : 0; // Header, one byte and its RIFF pad.
        var dataBytes = samples.Length * 2;
        var fmtBytes = 16;
        writer.Write("RIFF"u8);
        writer.Write((uint)(4 + (includeOddJunkChunk ? junkSize : 0) + 8 + fmtBytes + 8 + dataBytes));
        writer.Write("WAVE"u8);
        if (includeOddJunkChunk)
        {
            writer.Write("JUNK"u8);
            writer.Write(1u);
            writer.Write((byte)42);
            writer.Write((byte)0);
        }
        writer.Write("fmt "u8);
        writer.Write(16u);
        writer.Write((ushort)1);
        writer.Write(channels);
        writer.Write((uint)sampleRate);
        writer.Write((uint)(sampleRate * channels * 2));
        writer.Write((ushort)(channels * 2));
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write((uint)dataBytes);
        foreach (var sample in samples) writer.Write(sample);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
