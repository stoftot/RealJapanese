namespace Repositories.Exstensions;

public static class IEnumerableExstension
{
    public static IList<T> GetChunk<T>(
        this IEnumerable<T> source,
        int chunkCount,
        int chunkIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkCount);
        
        if(chunkCount == 0) return source as IList<T> ?? source.ToList();
        
        if (chunkIndex < 0 || chunkIndex >= chunkCount)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex));

        var list = source as IList<T> ?? source.ToList();
        int total = list.Count;

        int baseSize = total / chunkCount;
        int remainder = total % chunkCount;

        // Size of this specific chunk
        int size = baseSize + (chunkIndex < remainder ? 1 : 0);

        if (size == 0)
            return new List<T>();

        // Compute starting index
        int startIndex =
            (chunkIndex < remainder)
                ? chunkIndex * (baseSize + 1)
                : (remainder * (baseSize + 1)) + ((chunkIndex - remainder) * baseSize);

        var result = new List<T>(size);

        for (int i = 0; i < size; i++)
        {
            result.Add(list[startIndex + i]);
        }

        return result;
    }
}