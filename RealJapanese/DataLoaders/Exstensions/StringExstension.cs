namespace DataLoaders.Exstensions;

public static class StringExstension
{
    public static (string str, string last) LastChar(this string str)
    {
        if (string.IsNullOrEmpty(str))
            throw new ArgumentException("String cannot be null or empty", nameof(str));

        return (str[..^1], str[^1..]);
    }

    public static (string str, string last) LastTwoChars(this string str)
    {
        if (string.IsNullOrEmpty(str) || str.Length < 2)
            throw new ArgumentException("String must have at least two characters", nameof(str));

        return (str[..^2], str[^2..]);
    }
}