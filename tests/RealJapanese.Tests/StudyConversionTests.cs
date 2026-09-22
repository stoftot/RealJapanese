using DataLoaders.Models;
using Repositories.Exstensions;

namespace RealJapanese.Tests;

/// <summary>Verifies stable query values and conversion of study records into question-answer pairs.</summary>
public sealed class StudyConversionTests
{
    [Theory]
    [InlineData(WordPracticeCategory.Known, "known")]
    [InlineData(WordPracticeCategory.Rehearsing, "rehearsing")]
    [InlineData(WordPracticeCategory.Training, "training")]
    public void PracticeCategory_RoundTripsCanonicalQueryValue(WordPracticeCategory category, string queryValue)
    {
        Assert.Equal(queryValue, category.ToQueryValue());
        Assert.Equal(category, WordPracticeCategoryExtensions.ParseQueryValue(queryValue));
        Assert.Equal(category, WordPracticeCategoryExtensions.ParseQueryValue($"  {queryValue.ToUpperInvariant()}  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    public void PracticeCategory_RejectsMissingOrUnknownQueryValue(string? queryValue)
    {
        Assert.Throws<ArgumentException>(() => WordPracticeCategoryExtensions.ParseQueryValue(queryValue));
    }

    [Fact]
    public void WordQuestionConversions_PreserveEachRequestedDirection()
    {
        var words = new[] { new Word { Japanese = "猫", Kana = "ねこ", English = "cat" } };

        var englishToRomaji = Assert.Single(words.EnglishToRomajiQuestions());
        Assert.Equal("cat", englishToRomaji.Question);
        Assert.Equal("neko", englishToRomaji.Answer);

        var japaneseToEnglish = Assert.Single(words.JapaneseToEnglishQuestions());
        Assert.Equal("猫", japaneseToEnglish.Question);
        Assert.Equal("cat", japaneseToEnglish.Answer);

        var kanaToEnglish = Assert.Single(words.KanaToEnglishQuestions());
        Assert.Equal("ねこ", kanaToEnglish.Question);
        Assert.Equal("cat", kanaToEnglish.Answer);
    }

    [Fact]
    public void IrregularConjugatable_UsesShortTypeInGeneratedQuestions()
    {
        Conjugatabel conjugatabel = new Verb
        {
            Japanese = "する",
            Kana = "する",
            English = "do",
            Type = "irregular"
        };

        Assert.Equal("ir", conjugatabel.TypeShortForm());
        var kanaToType = Assert.Single(new[] { conjugatabel }.KanaToTypeQuestion());
        Assert.Equal("する", kanaToType.Question);
        Assert.Equal("ir", kanaToType.Answer);
        var englishToRomajiAndType = Assert.Single(new[] { conjugatabel }.EnglishToRomajiAndTypeQuestion());
        Assert.Equal("do", englishToRomajiAndType.Question);
        Assert.Equal("suru;ir", englishToRomajiAndType.Answer);
    }

    [Theory]
    [InlineData("コーヒー", "koohii")]
    [InlineData("んあ", "na")]
    [InlineData("かな(注)", "kana")]
    [InlineData("かな~", "kana")]
    public void ToRomaji_NormalizesApplicationAnnotations(string input, string expected)
    {
        Assert.Equal(expected, input.ToRomaji());
    }
}
