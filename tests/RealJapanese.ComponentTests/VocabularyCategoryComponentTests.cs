using AngleSharp.Dom;
using Bunit;
using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RealJapanese.Components.Pages.Vocabulary;
using RealJapanese.Components.Pages.Words;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.Bases;
using LinqExpression = System.Linq.Expressions.Expression;

namespace RealJapanese.ComponentTests;

/// <summary>Checks access, persisted selection, and category-bound exercises for the additional vocabulary types.</summary>
public sealed class VocabularyCategoryComponentTests
{
    [Fact]
    public void WordsHubLinksToEveryVocabularyCategory()
    {
        using var test = new ComponentTestContext();
        var cut = test.Context.Render<WordsSelector>();
        var links = cut.FindAll("nav[aria-label='Vocabulary categories'] a");

        Assert.Equal(new[]
        {
            "nouns", "verbs", "adjectives", "adverbs", "expressions", "pronouns", "determiners", "counters",
            "conjunctions", "particles", "suffixes"
        }, links.Select(link => link.GetAttribute("href")));
        Assert.Equal(3, cut.FindAll(".category-column").Count);
    }

    [Theory]
    [InlineData("adverbs")]
    [InlineData("expressions")]
    [InlineData("pronouns")]
    [InlineData("determiners")]
    [InlineData("counters")]
    [InlineData("conjunctions")]
    [InlineData("particles")]
    [InlineData("suffixes")]
    public void CategoryPagesPersistSelectionsAndOfferExercisesForTheirTrainingList(string category)
    {
        using var test = new ComponentTestContext();
        switch (category)
        {
            case "adverbs": AssertCategory<Adverb, AdverbsStudy>(test, new AdverbData(test.Paths), category); break;
            case "expressions": AssertCategory<Expression, ExpressionsStudy>(test, new ExpressionData(test.Paths), category); break;
            case "pronouns": AssertCategory<Pronoun, PronounsStudy>(test, new PronounData(test.Paths), category); break;
            case "determiners": AssertCategory<Determiner, DeterminersStudy>(test, new DeterminerData(test.Paths), category); break;
            case "counters": AssertCategory<Counter, CountersStudy>(test, new CounterData(test.Paths), category); break;
            case "conjunctions": AssertCategory<Conjunction, ConjunctionsStudy>(test, new ConjunctionData(test.Paths), category); break;
            case "particles": AssertCategory<Particle, ParticlesStudy>(test, new ParticleData(test.Paths), category); break;
            case "suffixes": AssertCategory<Suffix, SuffixesStudy>(test, new SuffixData(test.Paths), category); break;
        }
    }

    [Fact]
    public void ChangingPracticeCategoryLoadsTheNewListAndEmptyListsKeepControls()
    {
        using var test = new ComponentTestContext();
        var repository = new AdverbData(test.Paths);
        var selected = repository.Words.Take(2).ToArray();
        Assert.Equal(2, selected.Length);
        repository.AddToVocab(selected[0]);
        repository.AddToTraining(selected[1]);

        var cut = test.Context.Render<VocabularyStudy<Adverb>>(parameters => parameters
            .Add(component => component.Repository, repository)
            .Add(component => component.Title, "Adverbs")
            .Add(component => component.RoutePrefix, "adverbs")
            .Add(component => component.Exercise, "spelling")
            .Add(component => component.Category, "known"));
        Assert.Equal(selected[0].English, cut.Find("p.lead").TextContent.Trim());

        cut.Render(parameters => parameters.Add(component => component.Category, "training"));
        Assert.Equal(selected[1].English, cut.Find("p.lead").TextContent.Trim());

        cut.Render(parameters => parameters.Add(component => component.Category, "rehearsing"));
        Assert.Contains("Nothing to practise yet", cut.Markup);
        Assert.NotEmpty(cut.FindAll("input[type=range]"));
    }

    private static void AssertCategory<TItem, TPage>(
        ComponentTestContext test, WordDataBase<TItem> repository, string route)
        where TItem : Word
        where TPage : IComponent
    {
        test.Context.Services.AddSingleton(repository.GetType(), repository);
        var target = repository.Words.First();
        var cut = test.Context.Render<TPage>();

        FindWord(cut, 0, target).Click();
        Assert.Contains(target.Id, repository.VocabWordIds);
        FindWord(cut, 0, target).Click();
        FindWord(cut, 2, target).Click();
        Assert.Contains(target.Id, repository.TrainingWordIds);
        Assert.DoesNotContain(target.Id, repository.VocabWordIds);

        cut.FindAll("button.toggle-slider-option").Single(button => button.TextContent.Trim() == "Training").Click();
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Start practice").Click();
        Assert.EndsWith($"/{route}/spelling?category=training",
            test.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);

        cut.FindAll("button").Last(button => button.TextContent.Trim() == "Start practice").Click();
        Assert.EndsWith($"/{route}/flashcards?category=training",
            test.Context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);

        var component = LinqExpression.Parameter(typeof(TPage), "component");
        var exercise = LinqExpression.Lambda<Func<TPage, string?>>(LinqExpression.Property(component, "Exercise"), component);
        var spelling = test.Context.Render<TPage>(parameters => parameters.Add(exercise, "spelling"));
        Assert.Equal(target.English, spelling.Find("p.lead").TextContent.Trim());
        var flashcards = test.Context.Render<TPage>(parameters => parameters.Add(exercise, "flashcards"));
        Assert.Equal(target.Kana, flashcards.Find("p.flash-card-question").TextContent.Trim());
    }

    private static IElement FindWord<TPage>(IRenderedComponent<TPage> cut, int column, Word target)
        where TPage : IComponent =>
        cut.FindAll(".category-column")[column].QuerySelectorAll("button.list-group-item")
            .First(item => item.QuerySelectorAll("div").Select(element => element.TextContent.Trim())
                .SequenceEqual(new[] { target.English, target.Japanese, target.Kana }));
}
