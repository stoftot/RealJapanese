using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models.Genki;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Genki;
using GenkiPage = RealJapanese.Components.Pages.Genki.Genki;

namespace RealJapanese.ComponentTests;

/// <summary>Exercises Genki recap and finite practice using a private, test-owned question bank.</summary>
public sealed class GenkiComponentTests
{
    [Fact]
    public void Lesson_index_shows_all_twelve_lessons_without_example_banks()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>();

        var links = cut.FindAll(".lesson-grid a");
        Assert.Equal(Enumerable.Range(1, 12).Select(number => $"genki/{number}"),
            links.Select(link => link.GetAttribute("href")));
        Assert.All(links, link => Assert.Contains("grammar points", link.TextContent));
        Assert.Empty(cut.FindAll(".sentence-practice, .grammar-example"));
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(12, true, false)]
    public void Valid_lesson_shows_recap_and_bounded_lesson_navigation(int lesson, bool previous, bool next)
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, lesson));

        Assert.StartsWith($"Lesson {lesson} ·", cut.Find(".genki-module > .lesson-title").TextContent);
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
    public void Empty_bank_explains_unavailable_practice_and_links_to_word_study()
    {
        using var test = CreateTest(withQuestionBank: false);
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 1));

        Assert.Contains("no published questions yet", cut.Find("[role=status]").TextContent);
        Assert.Equal("words", cut.Find("[role=status] a").GetAttribute("href"));
        Assert.True(Button(cut, "Practise whole lesson").HasAttribute("disabled"));
        Assert.All(cut.FindAll(".grammar-point button"), button => Assert.True(button.HasAttribute("disabled")));
    }

    [Fact]
    public void Point_practice_uses_published_question_and_requires_neutral_comparison_before_advancing()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 1));
        var title = cut.Find("#g01-01 summary").TextContent;

        cut.Find("#g01-01 button").Click();

        Assert.Equal(title, cut.Find(".sentence-practice h3").TextContent);
        Assert.Equal("A university is a university.", CurrentQuestion(cut));
        Assert.True(Button(cut, "I understand · next").HasAttribute("disabled"));

        cut.Find("input[aria-label='Your Japanese sentence']").Input("別の言い方です。");
        Button(cut, "Compare with model").Click();

        Assert.Contains("different sentence may also be valid", Feedback(cut));
        Assert.DoesNotContain("wrong", Feedback(cut), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("incorrect", Feedback(cut), StringComparison.OrdinalIgnoreCase);
        Assert.False(Button(cut, "I understand · next").HasAttribute("disabled"));
        Assert.Contains("大学は大学です。", VisibleModels(cut));
        Assert.Contains("だいがくはだいがくです。", VisibleModels(cut));

        Button(cut, "I understand · next").Click();

        Assert.Equal("Round complete", cut.Find("[role=status] h3").TextContent);
        Assert.Contains("You reviewed 1 sentence prompts", cut.Find("[role=status]").TextContent);
        Assert.Empty(cut.FindAll(".sentence-practice"));
    }

    [Fact]
    public void Retry_replays_the_same_question_once_and_round_finishes()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 1));
        cut.Find("#g01-01 button").Click();
        var originalQuestion = CurrentQuestion(cut);
        var originalContext = cut.Find(".sentence-practice > p").TextContent;
        Assert.Equal("0 reviewed · 1 remaining", Progress(cut));
        Button(cut, "Compare with model").Click();
        var originalModels = VisibleModels(cut);

        Button(cut, "Practise this again").Click();

        Assert.Equal("1 reviewed · 1 remaining", Progress(cut));
        Assert.Empty(cut.FindAll(".review-note"));
        Assert.Equal("", cut.Find("input[aria-label='Your Japanese sentence']").GetAttribute("value"));
        Assert.Equal(originalQuestion, CurrentQuestion(cut));
        Assert.Equal(originalContext, cut.Find(".sentence-practice > p").TextContent);
        Button(cut, "Compare with model").Click();
        Assert.Equal(originalModels, VisibleModels(cut));
        Button(cut, "I understand · next").Click();

        Assert.Equal("Round complete", cut.Find("[role=status] h3").TextContent);
        Assert.Empty(cut.FindAll(".sentence-practice"));
    }

    [Fact]
    public void Changing_lesson_discards_previous_input_and_queued_retry()
    {
        using var test = CreateTest();
        var cut = test.Context.Render<GenkiPage>(parameters => parameters.Add(page => page.LessonNumber, 1));
        cut.Find("#g01-01 button").Click();
        cut.Find("input[aria-label='Your Japanese sentence']").Input("前の答えです。");
        Button(cut, "Compare with model").Click();
        Button(cut, "Practise this again").Click();
        cut.Find("input[aria-label='Your Japanese sentence']").Input("残さない答えです。");

        cut.Render(parameters => parameters.Add(page => page.LessonNumber, 2));

        Assert.Empty(cut.FindAll(".sentence-practice, .review-note"));
        cut.Render(parameters => parameters.Add(page => page.LessonNumber, 1));
        cut.Find("#g01-01 button").Click();
        Assert.Equal("", cut.Find("input[aria-label='Your Japanese sentence']").GetAttribute("value"));
        Assert.Equal("0 reviewed · 1 remaining", Progress(cut));
    }

    private static ComponentTestContext CreateTest(bool withQuestionBank = true)
    {
        var test = new ComponentTestContext();
        var catalog = new GenkiCatalog();
        var vocabulary = GenkiVocabulary.Load(test.Paths.CatalogRoot);
        test.Context.Services.AddSingleton(catalog);
        test.Context.Services.AddSingleton(vocabulary);
        test.Context.Services.AddSingleton(new GenkiPracticeService(test.Paths, catalog));
        if (withQuestionBank)
        {
            var noun = test.Nouns.Words.Single(word => word.Id == 0);
            test.Nouns.AddToVocab(noun);
            WriteQuestionBank(test.Paths.CatalogRoot, catalog);
        }
        return test;
    }

    private static void WriteQuestionBank(string catalogRoot, GenkiCatalog catalog)
    {
        var schema = catalog.Schemas.Single(item => item.Id == "g01-01-noun-predicate");
        var question = new GenkiQuestion
        {
            Id = "component-g01-01-noun-predicate",
            SchemaId = schema.Id,
            GrammarPointId = schema.GrammarPointId,
            English = "A university is a university.",
            Setting = "A simple identification.",
            Register = schema.Register,
            RequiredGrammar = catalog.AllowedGrammar(schema).Order(StringComparer.Ordinal).ToArray(),
            Answers =
            [
                new GenkiAnswer
                {
                    Japanese = "大学は大学です。",
                    Kana = "だいがくはだいがくです。",
                    RequiredWords = [new WordRef("noun", "0")]
                }
            ]
        };
        var path = Path.Combine(catalogRoot, "Genki", "questions.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(question, GenkiJson.Compact));
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
