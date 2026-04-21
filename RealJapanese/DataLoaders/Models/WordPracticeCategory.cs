namespace DataLoaders.Models;

public enum WordPracticeCategory
{
    Known = 0,
    Rehearsing = 1,
    Training = 2
}

public static class WordPracticeCategoryExtensions
{
    public static string ToQueryValue(this WordPracticeCategory category) =>
        category.ToString().ToLowerInvariant();

    public static WordPracticeCategory ParseQueryValue(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "training" => WordPracticeCategory.Training,
            "rehearsing" => WordPracticeCategory.Rehearsing,
            _ => WordPracticeCategory.Known
        };
}
