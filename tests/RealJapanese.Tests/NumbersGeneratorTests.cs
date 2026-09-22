using Repositories;
using Repositories.DTOs;

namespace RealJapanese.Tests;

/// <summary>Checks literal number, age, time and kanji questions produced by the public generators.</summary>
public sealed class NumbersGeneratorTests
{
    public static TheoryData<int, string> CountingCases => new()
    {
        { 0, "zero" },
        { 1, "ichi" },
        { 10, "juu" },
        { 11, "juuichi" },
        { 20, "nijuu" },
        { 99, "kyuujuukyuu" },
        { 100, "hyaku" },
        { 300, "sanbyaku" },
        { 600, "roppyaku" },
        { 800, "happyaku" },
        { 1_000, "sen" },
        { 3_000, "sanzen" },
        { 8_000, "hassen" },
        { 10_000, "ichiman" },
        { 10_001, "ichimanichi" },
        { 99_999, "kyuumankyuusenkyuuhyakukyuujuukyuu" },
        { 100_000, "juuman" }
    };

    public static TheoryData<int, string> DefectiveCountingCases => new()
    {
        { 301, "sanbyakuichi" },
        { 310, "sanbyakujuu" },
        { 123_456, "juunimansanzenyonhyakugojuuroku" }
    };

    public static TheoryData<int, string[]> AgeCases => new()
    {
        { 1, ["issai"] },
        { 8, ["hassai"] },
        { 10, ["jussai", "jissai"] },
        { 20, ["hatachi", "nijussai", "nijissai"] }
    };

    public static TheoryData<int, string> KanjiCases => new()
    {
        { 1, "一" },
        { 10, "十" },
        { 100, "百" },
        { 1_000, "千" },
        { 10_000, "一万" },
        { 12_345, "一万二千三百四十五" }
    };

    [Theory]
    [MemberData(nameof(CountingCases))]
    public void GenerateCounting_ReturnsLiteralReading(int number, string expected)
    {
        var result = new NumbersGenerator().GenerateCounting(number);

        Assert.Equal(number.ToString(), result.Question);
        Assert.Equal(expected, result.Answer);
    }

    [Theory(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    [MemberData(nameof(DefectiveCountingCases))]
    public void GenerateCounting_ComposesIrregularEmbeddedGroups(int number, string expected)
    {
        var result = new NumbersGenerator().GenerateCounting(number);

        Assert.Equal(number.ToString(), result.Question);
        Assert.Equal(expected, result.Answer);
    }

    [Theory(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    [MemberData(nameof(AgeCases))]
    public void GenerateAge_UsesAcceptedJapaneseCounterReading(int age, string[] acceptedAnswers)
    {
        var result = new NumbersGenerator().GenerateAge(age);

        Assert.Equal($"{age} years old", result.Question);
        Assert.Contains(result.Answer, acceptedAnswers);
    }

    [Theory]
    [MemberData(nameof(KanjiCases))]
    public void GenerateKanji_ComposesLiteralJapaneseNumeral(int number, string expected)
    {
        var result = new NumbersGenerator().GenerateKanji(number);

        Assert.Equal(expected, result.Question);
        Assert.Equal(number.ToString(), result.Answer);
    }

    [Theory]
    [InlineData(1, "ichi")]
    [InlineData(2, "ni")]
    [InlineData(3, "san")]
    [InlineData(4, "yo")]
    [InlineData(5, "go")]
    [InlineData(6, "roku")]
    [InlineData(7, "shichi")]
    [InlineData(8, "hachi")]
    [InlineData(9, "ku")]
    [InlineData(10, "juu")]
    [InlineData(11, "juuichi")]
    [InlineData(12, "juuni")]
    public void GenerateTime_KeepsRandomPresentationInternallyConsistent(int hour, string reading)
    {
        AssertTime(new NumbersGenerator().GenerateTime(hour), hour, reading);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void GenerateTime_RejectsHourOutsideClockRange(int hour)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumbersGenerator().GenerateTime(hour));
    }

    [Fact]
    public void GenerateRandomTime_RejectsLowerBoundBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumbersGenerator().GenerateRandomTime(0, 12));
    }

    [Fact]
    public void GenerateRandomTime_RejectsUpperBoundAboveTwelve()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumbersGenerator().GenerateRandomTime(1, 13));
    }

    [Fact]
    public void GenerateRandomTime_RejectsReversedRange()
    {
        Assert.Throws<ArgumentException>(() => new NumbersGenerator().GenerateRandomTime(8, 7));
    }

    [Fact]
    public void GenerateTimeRange_ValidatesBoundsWhenEnumerated()
    {
        var generator = new NumbersGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateTimeRange(0, 12).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateTimeRange(1, 13).ToArray());
        Assert.Throws<ArgumentException>(() => generator.GenerateTimeRange(8, 7).ToArray());
    }

    [Fact]
    public void GenerateRandomTime_SingletonRangeReturnsRequestedHour()
    {
        AssertTime(new NumbersGenerator().GenerateRandomTime(5, 5), 5, "go");
    }

    [Fact]
    public void GenerateTimeRange_SingletonContainsOneRequestedHour()
    {
        var result = Assert.Single(new NumbersGenerator().GenerateTimeRange(5, 5));

        AssertTime(result, 5, "go");
    }

    [Fact(Explicit = true)]
    [Trait("Category", "KnownDefect")]
    public void GenerateRandomTimeQuestion_DefaultRangeProducesValidTime()
    {
        var result = new NumbersQuestionGenerator().GenerateRandomTimeQuestion();
        var hour = int.Parse(result.Question.Split(':', ' ')[0]);
        var readings = new[]
        {
            "", "ichi", "ni", "san", "yo", "go", "roku", "shichi", "hachi", "ku", "juu", "juuichi", "juuni"
        };

        Assert.InRange(hour, 1, 12);
        AssertTime(result, hour, readings[hour]);
    }

    private static void AssertTime(QuestionAnswerDto result, int hour, string reading)
    {
        var questionPrefix = result.Question.StartsWith($"{hour}:30", StringComparison.Ordinal)
            ? $"{hour}:30"
            : hour.ToString();
        var halfPast = questionPrefix.EndsWith(":30", StringComparison.Ordinal);
        var isAm = result.Question.EndsWith(" am", StringComparison.Ordinal);
        var isPm = result.Question.EndsWith(" pm", StringComparison.Ordinal);
        Assert.True(isAm || isPm, $"Question '{result.Question}' has no AM/PM suffix.");
        var period = isAm ? (Question: " am", Answer: "gozen") : (Question: " pm", Answer: "gogo");

        Assert.Equal(questionPrefix + period.Question, result.Question);
        Assert.Equal(period.Answer + reading + "ji" + (halfPast ? "han" : ""), result.Answer);
    }
}
