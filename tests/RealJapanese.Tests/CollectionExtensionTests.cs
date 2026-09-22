using Repositories.Exstensions;

namespace RealJapanese.Tests;

/// <summary>Verifies the collection helpers used to divide and randomize practice rounds.</summary>
public sealed class CollectionExtensionTests
{
    public static TheoryData<int[], int, int, int[]> ChunkCases => new()
    {
        { Enumerable.Range(0, 10).ToArray(), 3, 0, [0, 1, 2, 3] },
        { Enumerable.Range(0, 10).ToArray(), 3, 1, [4, 5, 6] },
        { Enumerable.Range(0, 10).ToArray(), 3, 2, [7, 8, 9] },
        { [], 3, 1, [] },
        { [0, 1], 4, 0, [0] },
        { [0, 1], 4, 1, [1] },
        { [0, 1], 4, 2, [] },
        { [0, 1], 4, 3, [] }
    };

    [Theory]
    [MemberData(nameof(ChunkCases))]
    public void GetChunk_PartitionsSourceWithoutOverlap(
        int[] source,
        int chunkCount,
        int chunkIndex,
        int[] expected)
    {
        Assert.Equal(expected, source.GetChunk(chunkCount, chunkIndex));
    }

    [Fact]
    public void GetChunk_RejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>(() =>
            IEnumerableExstension.GetChunk<int>(null!, chunkCount: 1, chunkIndex: 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetChunk_RejectsNonPositiveChunkCount(int chunkCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Enumerable.Range(0, 10).GetChunk(chunkCount, chunkIndex: 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void GetChunk_RejectsIndexOutsideChunkCount(int chunkIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Enumerable.Range(0, 10).GetChunk(chunkCount: 3, chunkIndex));
    }

    [Fact]
    public void Shuffle_PreservesDuplicateMultiset()
    {
        var values = new List<int> { 3, 1, 3, 2, 1, 3 };
        var expectedCounts = values.CountBy(value => value).OrderBy(pair => pair.Key).ToArray();

        values.Shuffle();

        Assert.Equal(expectedCounts, values.CountBy(value => value).OrderBy(pair => pair.Key));
    }

    [Fact]
    public void Shuffle_HandlesEmptyList()
    {
        var values = new List<int>();

        values.Shuffle();

        Assert.Empty(values);
    }

    [Fact]
    public void Shuffle_LeavesSingletonIntact()
    {
        var values = new List<string> { "only" };

        values.Shuffle();

        Assert.Equal(["only"], values);
    }
}
