using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Genki;
using GenkiPage = RealJapanese.Components.Pages.Genki.Genki;

namespace RealJapanese.ComponentTests;

/// <summary>Checks lesson selection, model comparison, finite practice rounds and retry isolation through rendered Genki controls.</summary>
public sealed class GenkiComponentTests
{
    [Fact]
    public void Lesson_index_offers_all_twelve_lessons_in_order()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>();

        var links = cut.FindAll(".lesson-grid a");
        Assert.Equal(Enumerable.Range(1, 12).Select(number => $"genki/{number}"),
            links.Select(link => link.GetAttribute("href")));
        Assert.All(links, link => Assert.Contains("grammar points", link.TextContent));
        Assert.Empty(cut.FindAll(".sentence-practice"));
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(12, true, false)]
    public void Valid_lesson_shows_recap_and_bounded_lesson_navigation(int lesson, bool previous, bool next)
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, lesson));

        Assert.StartsWith($"Lesson {lesson} ·", cut.Find(".genki-module > h2").TextContent);
        Assert.NotEmpty(cut.FindAll(".grammar-point[open]"));
        Assert.Equal(previous, cut.FindAll(".lesson-nav a").Any(link => link.TextContent == "Previous lesson"));
        Assert.Equal(next, cut.FindAll(".lesson-nav a").Any(link => link.TextContent == "Next lesson"));
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Missing_lesson_explains_the_problem_and_offers_recovery(int lesson)
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, lesson));

        Assert.Contains("This lesson does not exist", cut.Find("[role=alert]").TextContent);
        Assert.Equal("genki", cut.Find("[role=alert] a").GetAttribute("href"));
        Assert.Empty(cut.FindAll(".grammar-point, .sentence-practice"));
    }

    [Fact]
    public void Point_specific_practice_allows_blank_reveal_but_requires_comparison_before_advancing()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 9));
        var title = cut.Find("#g09-01 summary").TextContent;

        cut.Find("#g09-01 button").Click();

        Assert.Equal(title, cut.Find(".sentence-practice h3").TextContent);
        Assert.NotEmpty(CurrentQuestion(cut));
        Assert.True(Button(cut, "I understand · next").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".sentence-practice [role=alert]"));

        Button(cut, "Compare with model").Click();

        Assert.NotEmpty(cut.Find(".sentence-practice [role=alert]").TextContent);
        Assert.False(Button(cut, "I understand · next").HasAttribute("disabled"));
        Assert.Contains("different sentence may also be valid", Feedback(cut));
        Button(cut, "I understand · next").Click();
        Assert.StartsWith("1 reviewed", Progress(cut));
        Assert.Empty(cut.FindAll(".review-note, .sentence-practice [role=alert]"));
        Assert.True(Button(cut, "I understand · next").HasAttribute("disabled"));
    }

    [Fact]
    public void Comparison_is_neutral_for_other_answers_and_recognizes_visible_models_and_kana()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 9));
        cut.Find("#g09-01 button").Click();
        cut.Find("input[aria-label='Your Japanese sentence']").Input("別の言い方です。");

        Button(cut, "Compare with model").Click();

        Assert.Contains("different sentence may also be valid", Feedback(cut));
        Assert.DoesNotContain("wrong", Feedback(cut), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("incorrect", Feedback(cut), StringComparison.OrdinalIgnoreCase);
        var models = VisibleModels(cut);
        Assert.True(models.Length >= 2, "This past-form practice should offer both Japanese and kana models.");
        foreach (var model in models)
        {
            cut.Find("input[aria-label='Your Japanese sentence']").Input(model);
            Assert.Contains("matches a supplied model", Feedback(cut));
        }
    }

    [Fact]
    public void Retry_replays_the_exact_exercise_once_and_the_round_finishes()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 1));
        var patternCount = test.Context.Services.GetRequiredService<GenkiCatalog>().FindLesson(1)!
            .GrammarPoints.Single(point => point.Id == "g01-03").Schemas.Count;
        cut.Find("#g01-03 button").Click();
        var originalQuestion = CurrentQuestion(cut);
        var originalContext = cut.Find(".sentence-practice > p").TextContent;
        Assert.Equal($"0 reviewed · {patternCount} remaining", Progress(cut));
        Button(cut, "Compare with model").Click();
        var originalModels = VisibleModels(cut);

        Button(cut, "Practise this again").Click();

        Assert.Equal($"1 reviewed · {patternCount} remaining", Progress(cut));
        Assert.Empty(cut.FindAll(".review-note"));
        Assert.Equal("", cut.Find("input[aria-label='Your Japanese sentence']").GetAttribute("value"));
        for (var index = 1; index < patternCount; index++)
        {
            Button(cut, "Compare with model").Click();
            Button(cut, "I understand · next").Click();
        }
        Assert.Equal(originalQuestion, CurrentQuestion(cut));
        Assert.Equal(originalContext, cut.Find(".sentence-practice > p").TextContent);
        Assert.Equal($"{patternCount} reviewed · 1 remaining", Progress(cut));
        Button(cut, "Compare with model").Click();
        Assert.Equal(originalModels, VisibleModels(cut));
        Button(cut, "I understand · next").Click();

        Assert.Equal("Round complete", cut.Find("[role=status] h3").TextContent);
        Assert.Contains($"You reviewed {patternCount + 1} sentence prompts", cut.Find("[role=status]").TextContent);
        Assert.Empty(cut.FindAll(".sentence-practice"));
    }

    [Fact]
    public void Changing_lesson_discards_previous_input_and_queued_retry()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 9));
        cut.Find("#g09-01 button").Click();
        cut.Find("input[aria-label='Your Japanese sentence']").Input("前の答えです。");
        Button(cut, "Compare with model").Click();
        Button(cut, "Practise this again").Click();
        cut.Find("input[aria-label='Your Japanese sentence']").Input("残さない答えです。");

        cut.Render(parameters => parameters.Add(page => page.LessonNumber, 1));

        Assert.Empty(cut.FindAll(".sentence-practice, .review-note"));
        var patternCount = test.Context.Services.GetRequiredService<GenkiCatalog>().FindLesson(1)!
            .GrammarPoints.Single(point => point.Id == "g01-03").Schemas.Count;
        cut.Find("#g01-03 button").Click();
        Assert.Equal("", cut.Find("input[aria-label='Your Japanese sentence']").GetAttribute("value"));
        Assert.Equal($"0 reviewed · {patternCount} remaining", Progress(cut));
        for (var index = 0; index < patternCount; index++)
        {
            Button(cut, "Compare with model").Click();
            Button(cut, "I understand · next").Click();
        }
        Assert.Contains($"You reviewed {patternCount} sentence prompts", cut.Find("[role=status]").TextContent);
        Assert.Empty(cut.FindAll(".sentence-practice"));
    }

    private static ComponentTestContext CreateTest()
    {
        var test = new ComponentTestContext();
        test.Context.Services.AddSingleton<GenkiCatalog>();
        test.Context.Services.AddSingleton<GenkiGenerator>();
        return test;
    }

    private static IElement Button(IRenderedComponent<GenkiPage> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);

    private static string CurrentQuestion(IRenderedComponent<GenkiPage> cut) =>
        cut.Find(".sentence-practice p.lead").TextContent.Trim();

    private static string Progress(IRenderedComponent<GenkiPage> cut) =>
        cut.Find(".sentence-practice .card-body small").TextContent.Trim();

    private static string Feedback(IRenderedComponent<GenkiPage> cut) =>
        cut.Find(".review-note p").TextContent.Trim();

    private static string[] VisibleModels(IRenderedComponent<GenkiPage> cut) =>
        cut.Find(".sentence-practice [role=alert]").TextContent.Trim()["Answers:".Length..]
            .Split(" / ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
