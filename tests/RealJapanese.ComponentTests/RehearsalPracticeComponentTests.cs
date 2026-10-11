using System.Text.Json;
using Bunit;
using DataLoaders.Models;
using RealJapanese.Components.Pages.Nouns;
using RealJapanese.Components.Pages.Verbs;
using RealJapanese.Components.Pages.Adjectives;
using RealJapanese.Components.Pages.Kanji;
using RealJapanese.Components.Pages.Vocabulary;
using RealJapanese.Components.Pages.Words;
using RealJapanese.Components.Shared;
using Repositories.Exstensions;

namespace RealJapanese.ComponentTests;

/// <summary>Protects progress callbacks from revealed answers, skips, wrong source IDs, and other exercises.</summary>
public sealed class RehearsalPracticeComponentTests
{
    [Fact]
    public void Finishing_retries_removes_moved_words_and_rechunks_the_pool_at_the_selected_chunk()
    {
        using var test = new ComponentTestContext();
        var words = test.Words.Words.Take(5).ToList();
        test.Words.SetCategory(words, WordPracticeCategory.Known);
        var cut = test.Context.Render<VocabularySpelling<Word>>(parameters => parameters
            .Add(component => component.Repository, test.Words)
            .Add(component => component.Category, "known"));
        cut.FindAll("input[type=number]")[0].Change("2");
        cut.FindAll("input[type=number]")[1].Change("2");
        var missed = words.Single(word => word.English == cut.Find("p.lead").TextContent.Trim());
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        Button(cut, "Next").Click(); // Finish the original two-item chunk.
        Assert.Equal(missed.English, cut.Find("p.lead").TextContent.Trim());
        Assert.Contains("1/1", cut.Markup);
        cut.Find("input[placeholder]").Input(missed.Kana.ToRomaji());

        Assert.Equal("2", cut.FindAll("input[type=number]")[0].GetAttribute("value"));
        Assert.Equal("2", cut.FindAll("input[type=number]")[1].GetAttribute("value"));
        var remaining = words.Where(word => word.Id != missed.Id).ToList();
        var seen = new HashSet<string>();
        for (var index = 0; index < 2; index++)
        {
            Assert.Contains("/2", cut.Markup);
            Assert.True(seen.Add(cut.Find("p.lead").TextContent.Trim()));
            Button(cut, "Next").Click();
        }
        Assert.Equal(remaining.Skip(2).Select(word => word.English).Order(), seen.Order());
        Assert.DoesNotContain(missed.English, seen);
    }

    [Fact]
    public void Removing_last_item_in_selected_chunk_keeps_that_chunk_empty_and_other_chunks_available()
    {
        using var test = new ComponentTestContext();
        var words = test.Words.Words.Take(3).ToList();
        test.Words.SetCategory(words, WordPracticeCategory.Known);
        var cut = test.Context.Render<VocabularySpelling<Word>>(parameters => parameters
            .Add(component => component.Repository, test.Words)
            .Add(component => component.Category, "known"));
        cut.FindAll("input[type=number]")[0].Change("3");
        cut.FindAll("input[type=number]")[1].Change("3");
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        cut.Find("input[placeholder]").Input(words[2].Kana.ToRomaji());
        Assert.Contains("Nothing to practise yet", cut.Markup);
        Assert.Equal("3", cut.FindAll("input[type=number]")[1].GetAttribute("value"));
        cut.FindAll("input[type=number]")[1].Change("1");
        Assert.Equal(words[0].English, cut.Find("p.lead").TextContent.Trim());
        Assert.Contains("1/1", cut.Markup);
    }

    [Fact]
    public void Moved_question_does_not_interrupt_other_retries_before_the_pool_is_rechunked()
    {
        using var test = new ComponentTestContext();
        var words = test.Words.Words.Take(3).ToDictionary(word => word.English);
        test.Words.SetCategory(words.Values, WordPracticeCategory.Known);
        var cut = test.Context.Render<VocabularySpelling<Word>>(parameters => parameters
            .Add(component => component.Repository, test.Words)
            .Add(component => component.Category, "known"));
        for (var index = 0; index < 3; index++)
        {
            Button(cut, "Show answer (Enter)").Click();
            Button(cut, "Next").Click();
        }
        var first = words[cut.Find("p.lead").TextContent.Trim()];
        cut.Find("input[placeholder]").Input(first.Kana.ToRomaji());
        Assert.Contains("2/3", cut.Markup);
        var stillMissed = words[cut.Find("p.lead").TextContent.Trim()];
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        var third = words[cut.Find("p.lead").TextContent.Trim()];
        cut.Find("input[placeholder]").Input(third.Kana.ToRomaji());
        Assert.Contains("1/1", cut.Markup);
        Assert.Equal(stillMissed.English, cut.Find("p.lead").TextContent.Trim());
        cut.Find("input[placeholder]").Input(stillMissed.Kana.ToRomaji());
        Assert.Contains("Nothing to practise yet", cut.Markup);
        Assert.Empty(test.Words.VocabWordIds);
    }

    [Fact]
    public void Revealed_known_word_moves_immediately_while_its_retry_remains_active()
    {
        using var test = new ComponentTestContext();
        var word = test.Words.Words.First();
        test.Words.AddToVocab(word);
        var cut = test.Context.Render<VocabularySpelling<Word>>(parameters => parameters
            .Add(component => component.Repository, test.Words)
            .Add(component => component.Category, "known"));

        cut.Find("input[placeholder]").Input("wrong");
        cut.Find("input[placeholder]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        Assert.DoesNotContain(word.Id, test.Words.VocabWordIds);
        Assert.Contains(word.Id, test.Words.RehearsingWordIds);
        cut.Find("input[placeholder]").Input(word.Kana.ToRomaji());
        Assert.DoesNotContain(word.Id, test.Words.VocabWordIds);
        Assert.Contains(word.Id, test.Words.RehearsingWordIds);
        Assert.Equal(0, Saved(test, "Words").Rehearsal[word.Id].CorrectDays);
        Assert.Contains("1/1", cut.Markup); // The original question remains queued for retry.
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        Assert.DoesNotContain(word.Id, test.Words.VocabWordIds);
        cut.Find("input[placeholder]").Input(word.Kana.ToRomaji());
        Assert.DoesNotContain(word.Id, test.Words.VocabWordIds);
        Assert.Contains(word.Id, test.Words.RehearsingWordIds);
        Assert.Equal(0, Saved(test, "Words").Rehearsal[word.Id].CorrectDays);
        Assert.Contains("Nothing to practise yet", cut.Markup);
    }

    [Fact]
    public void Skips_do_not_earn_credit_and_a_reveal_overrides_todays_success()
    {
        using var test = new ComponentTestContext();
        var word = test.Words.Words.First();
        test.Words.AddToRehearsing(word);
        var cut = test.Context.Render<VocabularySpelling<Word>>(parameters => parameters
            .Add(component => component.Repository, test.Words)
            .Add(component => component.Category, "rehearsing"));

        Button(cut, "Next").Click();
        Assert.Empty(Saved(test, "Words").Rehearsal);
        cut.Find("input[placeholder]").Input(word.Kana.ToRomaji());
        cut.Find("input[placeholder]").Input(word.Kana.ToRomaji());
        Assert.Equal(1, Saved(test, "Words").Rehearsal[word.Id].CorrectDays);
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        cut.Find("input[placeholder]").Input(word.Kana.ToRomaji());
        Assert.Equal(0, Saved(test, "Words").Rehearsal[word.Id].CorrectDays);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Combined_spelling_moves_only_the_failed_source_despite_colliding_catalog_ids(bool supporting)
    {
        using var test = new ComponentTestContext(workspace =>
        {
            File.WriteAllText(Path.Combine(workspace.CatalogRoot, "Adverbs", "Adverbs.json"),
                JsonSerializer.Serialize(new Word[] { new() { Id = 0, Japanese = "あお", Kana = "あお", English = "blue" } }));
            File.WriteAllText(Path.Combine(workspace.CatalogRoot, "Expressions", "Expressions.json"),
                JsonSerializer.Serialize(new Word[] { new() { Id = 0, Japanese = "あか", Kana = "あか", English = "red" } }));
        });
        test.Adverbs.AddToVocab(test.Adverbs.Words.Single());
        test.Expressions.AddToVocab(test.Expressions.Words.Single());
        test.UseCategory("known");
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut = supporting
            ? test.Context.Render<SupportingVocabularyStudy>(parameters => parameters.Add(component => component.Exercise, "spelling"))
            : test.Context.Render<WordSpelling>();
        var failed = cut.Find("p.lead").TextContent.Trim();
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        Assert.Equal(failed == "blue", test.Adverbs.RehearsingWordIds.Contains(0));
        Assert.Equal(failed == "red", test.Expressions.RehearsingWordIds.Contains(0));
        Assert.Contains("2/2", cut.Markup); // Persisting the move does not shrink the active round.
        var other = cut.Find("p.lead").TextContent.Trim();
        cut.Find("input[placeholder]").Input(other == "blue" ? "ao" : "aka");
        Assert.Equal(failed, cut.Find("p.lead").TextContent.Trim());
        cut.Find("input[placeholder]").Input(failed == "blue" ? "ao" : "aka");

        Assert.Equal(failed == "blue", test.Adverbs.RehearsingWordIds.Contains(0));
        Assert.Equal(failed == "red", test.Expressions.RehearsingWordIds.Contains(0));
        Assert.Equal(failed != "blue", test.Adverbs.VocabWordIds.Contains(0));
        Assert.Equal(failed != "red", test.Expressions.VocabWordIds.Contains(0));
        Assert.Equal(other, cut.Find("p.lead").TextContent.Trim());
        Assert.Contains("1/1", cut.Markup);
        var nextFullRound = test.Context.Render<WordSpelling>();
        Assert.NotEqual(failed, nextFullRound.Find("p.lead").TextContent.Trim());
    }

    [Fact]
    public void Flashcards_and_type_categorisation_do_not_update_learning_progress()
    {
        using var test = new ComponentTestContext();
        var adverb = test.Adverbs.Words.First();
        var verb = test.Verbs.Words.First();
        test.Adverbs.AddToVocab(adverb);
        test.Verbs.AddToVocab(verb);
        test.UseCategory("known");
        var before = File.ReadAllBytes(Path.Combine(test.Paths.ProgressRoot, "Progress.json"));
        var flash = test.Context.Render<WordFlashCards>();
        Button(flash, "Forgot").Click();
        Button(flash, "Next question").Click();
        Button(flash, "Show answer").Click();
        Button(flash, "Next question").Click();
        var spelling = test.Context.Render<CategoriesVerbs>();
        Button(spelling, "Show answer (Enter)").Click();
        Button(spelling, "Next").Click();
        spelling.Find("input[placeholder]").Input(verb.TypeShortForm());
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(test.Paths.ProgressRoot, "Progress.json")));
    }

    [Theory]
    [InlineData("Nouns")]
    [InlineData("Verbs")]
    [InlineData("Adjectives")]
    [InlineData("Kanji/Singel")]
    [InlineData("Kanji/Combined")]
    public void Each_learn_exercise_moves_on_reveal_and_keeps_all_retry_answers_active(string dataset)
    {
        using var test = new ComponentTestContext();
        test.UseCategory("known");
        Word word;
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut;
        switch (dataset)
        {
            case "Nouns":
                word = test.Nouns.Words.First();
                test.Nouns.AddToVocab((Noun)word);
                cut = test.Context.Render<NounSpelling>();
                break;
            case "Verbs":
                word = test.Verbs.Words.First();
                test.Verbs.AddToVocab((Verb)word);
                cut = test.Context.Render<VerbSpelling>();
                break;
            case "Adjectives":
                word = test.Adjectives.Words.First();
                test.Adjectives.AddToVocab((Adjective)word);
                cut = test.Context.Render<AdjectiveSpelling>();
                break;
            case "Kanji/Singel":
                word = test.Kanji.Single.Words.First(item => item.English.Contains(';'));
                test.Kanji.Single.AddToVocab(word);
                cut = test.Context.Render<KanjiSingleMeaning>();
                break;
            default:
                word = test.Kanji.Combined.Words.First();
                test.Kanji.Combined.AddToVocab(word);
                cut = test.Context.Render<KanjiCombinedMeaning>();
                break;
        }
        Button(cut, "Show answer (Enter)").Click();
        Button(cut, "Next").Click();
        var answers = dataset switch
        {
            "Kanji/Singel" => word.English.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            "Kanji/Combined" => new[] { word.English },
            "Verbs" or "Adjectives" => new[] { word.Kana.ToRomaji(), ((Conjugatabel)word).TypeShortForm() },
            _ => new[] { word.Kana.ToRomaji() }
        };
        Assert.DoesNotContain(word.Id, Saved(test, dataset).KnownIds);
        Assert.Contains(word.Id, Saved(test, dataset).RehearsingIds);
        Assert.Equal(0, Saved(test, dataset).Rehearsal[word.Id].CorrectDays);
        Assert.Contains("1/1", cut.Markup); // Retry remains in the active Learn session.
        foreach (var answer in answers)
        {
            Assert.DoesNotContain(word.Id, Saved(test, dataset).KnownIds);
            cut.Find("input[placeholder]").Input(answer);
        }
        Assert.DoesNotContain(word.Id, Saved(test, dataset).KnownIds);
        Assert.Contains(word.Id, Saved(test, dataset).RehearsingIds);
        Assert.Equal(0, Saved(test, dataset).Rehearsal[word.Id].CorrectDays);
        Assert.Contains("Nothing to practise yet", cut.Markup);
    }

    private static VocabSaveFile Saved(ComponentTestContext test, string dataset)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(test.Paths.ProgressRoot, "Progress.json")));
        return document.RootElement.GetProperty("Data").GetProperty(dataset).Deserialize<VocabSaveFile>()!;
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label);
}
