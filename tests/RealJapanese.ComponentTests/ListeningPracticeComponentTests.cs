using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using DataLoaders.Models;
using RealJapanese.Components.Pages.Vocabulary;
using RealJapanese.Components.Shared;
using Repositories.Audio;

namespace RealJapanese.ComponentTests;

/// <summary>Exercises listening question selection, retries, and its read-only progress boundary.</summary>
public sealed class ListeningPracticeComponentTests
{
    [Fact]
    public void RouteUsesSavedCategoryAndCompositeMixerSourcesAndResetsWhenSourcesChange()
    {
        using var test = new ComponentTestContext(workspace =>
        {
            // Deliberately collide IDs in two independent catalogs.
            foreach (var dataset in new[] { "Nouns", "Adverbs" })
                File.WriteAllText(Path.Combine(workspace.CatalogRoot, dataset, dataset + ".json"),
                    System.Text.Json.JsonSerializer.Serialize(Entries().Take(3).Select((entry, index) => new Word
                    {
                        Id = index, Japanese = entry.Japanese, Kana = entry.Kana, English = entry.English
                    })));
        });
        test.Context.ComponentFactories.AddStub<ListeningAudioPlayer>();
        var noun = test.Nouns.Words.Single(word => word.Id == 0);
        var adverb = test.Adverbs.Words.Single(word => word.Id == 0);
        var training = test.Nouns.Words.Single(word => word.Id == 1);
        test.Nouns.AddToVocab(noun);
        test.Adverbs.AddToVocab(adverb);
        test.Nouns.AddToTraining(training);
        var collection = new VocabularyCollection(test.Paths);
        WriteManifest(test, collection.Entries.Where(word =>
            word.Dataset == "Nouns" && word.SourceId is 0 or 1 || word.Dataset == "Adverbs" && word.SourceId == 0));
        var navigation = test.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/words/listening?category=known&sources=Nouns");
        var cut = test.Context.Render<ListeningStudy>();
        var clip = cut.FindComponent<Stub<ListeningAudioPlayer>>().Instance.Parameters.Get(player => player.Clip);
        Assert.Equal(("Nouns", 0), (clip.Dataset, clip.Id));
        Assert.Equal("words?category=known&sources=Nouns", cut.Find("a").GetAttribute("href"));
        cut.FindAll("div[role=group] button").First(button => button.TextContent.Trim() != noun.English).Click();
        Assert.Contains("This clip will repeat", cut.Markup);

        navigation.NavigateTo("/words/listening?category=known&sources=Adverbs");
        cut.WaitForAssertion(() =>
        {
            var changed = cut.FindComponent<Stub<ListeningAudioPlayer>>().Instance.Parameters.Get(player => player.Clip);
            Assert.Equal(("Adverbs", 0), (changed.Dataset, changed.Id));
            Assert.DoesNotContain("This clip will repeat", cut.Markup);
        });
        navigation.NavigateTo("/nouns/listening?category=training");
        cut.WaitForAssertion(() =>
        {
            var changed = cut.FindComponent<Stub<ListeningAudioPlayer>>().Instance.Parameters.Get(player => player.Clip);
            Assert.Equal(("Nouns", 1), (changed.Dataset, changed.Id));
        });
        navigation.NavigateTo("/words/listening?category=known&sources=none");
        cut.WaitForAssertion(() => Assert.Contains("Nothing to listen to yet", cut.Markup));
    }

    [Fact]
    public void QuestionOffersThreeDistinctEnglishChoicesWithoutShowingJapaneseText()
    {
        using var test = new ComponentTestContext();
        var entries = Entries();
        var cut = Render(test, entries, entries, withAudio: true);

        var choices = ChoiceButtons(cut).Select(button => button.TextContent.Trim()).ToArray();
        Assert.Equal(3, choices.Length);
        Assert.Equal(3, choices.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(CurrentWord(cut, entries).English, choices);
        Assert.DoesNotContain(CurrentWord(cut, entries).Japanese, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(CurrentWord(cut, entries).Kana, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void MissedAnswerRepeatsUntilCorrectThenRestoresTheFullRound()
    {
        using var test = new ComponentTestContext();
        var entries = Entries();
        var cut = Render(test, entries, entries, withAudio: true);
        var roundSize = Total(cut);
        var missed = CurrentWord(cut, entries);

        ChooseWrong(cut, missed);
        Assert.Contains("This clip will repeat", cut.Markup, StringComparison.Ordinal);
        Click(cut, "Next question");
        for (var index = 1; index < roundSize; index++)
        {
            ChooseCorrect(cut, entries);
            Click(cut, "Next question");
        }

        Assert.Equal(missed.SourceId, CurrentWord(cut, entries).SourceId);
        Assert.Equal(1, Total(cut));
        ChooseWrong(cut, missed);
        Click(cut, "Next question");
        Assert.Equal(missed.SourceId, CurrentWord(cut, entries).SourceId);
        Assert.Equal(1, Total(cut));
        ChooseWrong(cut, missed);
        Click(cut, "Next question");
        Assert.Equal(missed.SourceId, CurrentWord(cut, entries).SourceId);
        Assert.Equal(1, Total(cut));
        ChoiceButtons(cut).Single(button => button.TextContent.Trim() == missed.English).Click();
        Assert.Contains("Correct!", cut.Markup, StringComparison.Ordinal);
        Click(cut, "Next question");

        Assert.Equal(roundSize, Total(cut));
    }

    [Fact]
    public void ChangingChunkOrSplitClearsFeedbackAndQueuedRetry()
    {
        using var test = new ComponentTestContext();
        var entries = Entries();
        var cut = Render(test, entries, entries, withAudio: true);
        ChooseWrong(cut, CurrentWord(cut, entries));
        Assert.Contains("This clip will repeat", cut.Markup, StringComparison.Ordinal);

        NumberInputs(cut)[0].Change("3");
        Assert.DoesNotContain("This clip will repeat", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Correct!", cut.Markup, StringComparison.Ordinal);
        NumberInputs(cut)[1].Change("3");
        Assert.Equal(1, Total(cut));
        Assert.DoesNotContain("This clip will repeat", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleWordChunkStillHasThreeChoicesAndQuestionNeedsExportAndTwoUnambiguousDistractors()
    {
        using var test = new ComponentTestContext();
        var entries = Entries();
        var cut = Render(test, entries, entries, withAudio: true);
        NumberInputs(cut)[0].Change("3");
        NumberInputs(cut)[1].Change("3");
        Assert.Equal(1, Total(cut));
        Assert.Equal(3, ChoiceButtons(cut).Length);

        var noExports = Render(test, entries, entries, withAudio: false);
        Assert.Contains("No usable audio is available", noExports.Markup, StringComparison.Ordinal);

        var targetOnly = entries.Take(1).ToList();
        var ambiguousPool = new List<VocabularyEntry>
        {
            entries[0], Entry(21, "猫科", "ねこか", "cat; feline"), Entry(22, "飼う", "かう", "pet")
        };
        var tooFewDistractors = Render(test, targetOnly, ambiguousPool, withAudio: true);
        Assert.Contains("Nothing to listen to yet", tooFewDistractors.Markup, StringComparison.Ordinal);
        Assert.Contains("At least three distinct meanings", tooFewDistractors.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateEnglishAndSameKanaCandidatesDoNotMakeChoicesAmbiguous()
    {
        using var test = new ComponentTestContext();
        var pool = Entries().Concat([
            Entry(15, "猫語", "ねこ", "feline"),
            Entry(16, "子猫", "こねこ", "cat")
        ]).ToList();
        var cut = Render(test, pool.Take(1).ToList(), pool, withAudio: true);

        var choices = ChoiceButtons(cut).Select(button => button.TextContent.Trim()).ToArray();
        Assert.Equal(3, choices.Length);
        Assert.Equal(3, choices.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("cat", choices);
        Assert.DoesNotContain("feline", choices);
    }

    [Theory]
    [InlineData("known")]
    [InlineData("rehearsing")]
    [InlineData("training")]
    public void AnsweringListeningPracticeLeavesSavedProgressUntouched(string category)
    {
        using var test = new ComponentTestContext();
        var sourceWords = test.Words.Words.Take(4).ToList();
        Assert.Equal(4, sourceWords.Count);
        var target = sourceWords[0];
        switch (category)
        {
            case "known": test.Words.AddToVocab(target); break;
            case "rehearsing": test.Words.AddToRehearsing(target); break;
            default: test.Words.AddToTraining(target); break;
        }
        var entries = sourceWords.Select(ToEntry).ToList();
        WriteManifest(test, entries);
        var progressPath = Path.Combine(test.Paths.ProgressRoot, "Progress.json");
        var before = File.ReadAllBytes(progressPath);
        var cut = Render(test, entries, entries, withAudio: true);
        var missed = CurrentWord(cut, entries);
        ChooseWrong(cut, missed);
        Click(cut, "Next question");
        for (var index = 1; index < 4; index++)
        {
            ChooseCorrect(cut, entries);
            Click(cut, "Next question");
        }
        Assert.Equal(missed.SourceId, CurrentWord(cut, entries).SourceId);
        ChoiceButtons(cut).Single(button => button.TextContent.Trim() == missed.English).Click();
        Click(cut, "Next question");

        Assert.Equal(before, File.ReadAllBytes(progressPath));
    }

    private static IRenderedComponent<ListeningPractice> Render(ComponentTestContext test,
        List<VocabularyEntry> words, List<VocabularyEntry> choices, bool withAudio)
    {
        // Playback/IO lifecycle is exercised in Chromium; these tests isolate quiz state.
        test.Context.ComponentFactories.AddStub<ListeningAudioPlayer>();
        if (withAudio) WriteManifest(test, choices);
        else
        {
            var manifest = Path.Combine(test.Workspace.CatalogRoot, "Audio", "manifest.json");
            if (File.Exists(manifest)) File.Delete(manifest);
        }
        var catalog = new StudyAudioCatalog(test.Paths);
        return test.Context.Render<ListeningPractice>(parameters => parameters
            .Add(component => component.Words, words)
            .Add(component => component.ChoiceWords, choices)
            .Add(component => component.Audio, catalog));
    }

    private static List<VocabularyEntry> Entries() =>
    [
        Entry(11, "猫", "ねこ", "cat"),
        Entry(12, "犬", "いぬ", "dog"),
        Entry(13, "鳥", "とり", "bird"),
        Entry(14, "魚", "さかな", "fish")
    ];

    private static VocabularyEntry Entry(int id, string japanese, string kana, string english) => new()
    {
        Id = id, SourceId = id, Dataset = "Words", SourceLabel = "Words",
        Japanese = japanese, Kana = kana, English = english
    };

    private static VocabularyEntry ToEntry(Word word) => new()
    {
        Id = word.Id, SourceId = word.Id, Dataset = "Words", SourceLabel = "Words",
        Japanese = word.Japanese, Kana = word.Kana, English = word.English
    };

    private static void WriteManifest(ComponentTestContext test, IEnumerable<VocabularyEntry> entries)
    {
        var root = Path.Combine(test.Workspace.CatalogRoot, "Audio");
        Directory.CreateDirectory(root);
        var exports = entries.Select((entry, index) => new
        {
            word = new { dataset = entry.Dataset, id = entry.SourceId, japanese = entry.Japanese, kana = entry.Kana },
            file = $"clip-{index}.wav"
        }).ToArray();
        File.WriteAllText(Path.Combine(root, "manifest.json"), System.Text.Json.JsonSerializer.Serialize(exports));
        for (var index = 0; index < exports.Length; index++)
            File.WriteAllBytes(Path.Combine(root, $"clip-{index}.wav"), [1, 2, 3]);
    }

    private static VocabularyEntry CurrentWord(IRenderedComponent<ListeningPractice> cut, IReadOnlyList<VocabularyEntry> entries)
    {
        var clip = cut.FindComponent<Stub<ListeningAudioPlayer>>().Instance.Parameters.Get(player => player.Clip);
        return entries.Single(entry => entry.SourceId == clip.Id && entry.Dataset == clip.Dataset);
    }

    private static AngleSharp.Dom.IElement[] ChoiceButtons(IRenderedComponent<ListeningPractice> cut) =>
        cut.FindAll("div[role=group] button").ToArray();

    private static AngleSharp.Dom.IElement[] NumberInputs(IRenderedComponent<ListeningPractice> cut) =>
        cut.FindAll("input[type=number]").ToArray();

    private static int Total(IRenderedComponent<ListeningPractice> cut) =>
        int.Parse(cut.Find("small.text-muted").TextContent.Split('/')[1]);

    private static void ChooseWrong(IRenderedComponent<ListeningPractice> cut, VocabularyEntry target) =>
        ChoiceButtons(cut).First(button => button.TextContent.Trim() != target.English).Click();

    private static void ChooseCorrect(IRenderedComponent<ListeningPractice> cut, IReadOnlyList<VocabularyEntry> entries)
    {
        var current = CurrentWord(cut, entries);
        ChoiceButtons(cut).Single(button => button.TextContent.Trim() == current.English).Click();
    }

    private static void Click(IRenderedComponent<ListeningPractice> cut, string label) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == label).Click();
}
