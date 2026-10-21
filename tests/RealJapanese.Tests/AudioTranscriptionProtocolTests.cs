using AudioStudio;

namespace RealJapanese.Tests;

public sealed class AudioTranscriptionProtocolTests
{
    [Fact]
    public async Task PumpTranscriptionOutputAsync_AcceptsReorderedResultsAndPerClipErrors()
    {
        const string output = """
            {"id":"fish:word-2","text":null,"error":"decode failed"}
            {"id":"bert:word-1","text":"こんにちは","error":null}
            """;
        var results = new List<TranscriptionOutput>();

        await MediaTools.PumpTranscriptionOutputAsync(new StringReader(output), ["bert:word-1", "fish:word-2"],
            result => { results.Add(result); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(["fish:word-2", "bert:word-1"], results.Select(result => result.Id));
        Assert.Equal("こんにちは", results[1].Text);
        Assert.Equal("decode failed", results[0].Error);
    }

    [Fact]
    public async Task PumpTranscriptionOutputAsync_RejectsMissingResults()
    {
        const string output = "{\"id\":\"word-1\",\"text\":\"こんにちは\",\"error\":null}";

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            MediaTools.PumpTranscriptionOutputAsync(new StringReader(output), ["word-1", "word-2"],
                _ => Task.CompletedTask, CancellationToken.None));

        Assert.Contains("omitted 1 result", error.Message);
        Assert.Contains("word-2", error.Message);
    }

    [Fact]
    public async Task PumpTranscriptionOutputAsync_RejectsDuplicateResults()
    {
        const string line = "{\"id\":\"word-1\",\"text\":\"こんにちは\",\"error\":null}";

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            MediaTools.PumpTranscriptionOutputAsync(new StringReader(line + "\n" + line), ["word-1"],
                _ => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task PumpTranscriptionOutputAsync_RejectsUnexpectedResults()
    {
        const string output = "{\"id\":\"other\",\"text\":\"こんにちは\",\"error\":null}";

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            MediaTools.PumpTranscriptionOutputAsync(new StringReader(output), ["word-1"],
                _ => Task.CompletedTask, CancellationToken.None));
    }
}
