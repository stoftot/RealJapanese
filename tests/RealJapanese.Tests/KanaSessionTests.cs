using Repositories.Kana;

namespace RealJapanese.Tests;

/// <summary>Checks finite practice rounds, first-try scoring and safe restoration of local choices.</summary>
public sealed class KanaSessionTests
{
    [Fact]
    public void FiniteRound_VisitsEverySelectedIdOnceAndDeduplicatesSelection()
    {
        var first = Character("h-あ");
        var second = Character("h-い");
        var session = new KanaSession([first, second, first], randomOrder: false);

        Assert.Equal(2, session.Total);
        Assert.Equal(first.Id, session.Current!.Id);
        Assert.True(session.Answer("a"));
        Assert.Equal(second.Id, session.Current!.Id);
        Assert.True(session.Answer("i"));
        Assert.True(session.Finished);
        Assert.Equal(2, session.Completed);
        Assert.Equal(2, session.Correct);
        Assert.Null(session.Current);
    }

    [Fact]
    public void CorrectTrimmedAlias_AdvancesAndScores()
    {
        var session = new KanaSession([Character("h-し")], randomOrder: false);

        Assert.True(session.Answer("  SI  "));
        Assert.Equal(1, session.Completed);
        Assert.Equal(1, session.Correct);
        Assert.Equal(100, session.Percent);
    }

    [Fact]
    public void WrongThenCorrect_AdvancesButDoesNotScore()
    {
        var session = new KanaSession([Character("h-し"), Character("h-ち")], randomOrder: false);

        Assert.False(session.Answer("chi"));
        Assert.True(session.Missed);
        Assert.Equal(0, session.Completed);
        Assert.True(session.Answer("shi"));
        Assert.Equal(1, session.Completed);
        Assert.Equal(0, session.Correct);
        Assert.False(session.Missed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevealThenAnswerOrSkip_DoesNotScore(bool skip)
    {
        var session = new KanaSession([Character("h-あ")], randomOrder: false);

        session.Reveal();
        Assert.True(session.Revealed);
        if (skip)
        {
            session.Skip();
        }
        else
        {
            Assert.True(session.Answer("a"));
        }

        Assert.True(session.Finished);
        Assert.Equal(0, session.Correct);
        Assert.Equal(1, session.Completed);
    }

    [Fact]
    public void FinishedSession_IgnoresAnswerRevealAndSkip()
    {
        var session = new KanaSession([Character("h-あ")], randomOrder: false);
        Assert.True(session.Answer("a"));

        Assert.False(session.Answer("a"));
        session.Reveal();
        session.Skip();

        Assert.True(session.Finished);
        Assert.Equal(1, session.Completed);
        Assert.Equal(1, session.Correct);
        Assert.False(session.Revealed);
        Assert.Null(session.Current);
    }

    [Fact]
    public void RandomRound_CoversEverySelectedItemOnce()
    {
        var selected = new[] { "h-あ", "h-い", "h-う", "h-え", "h-お", "h-か" }
            .Select(Character)
            .ToArray();
        var session = new KanaSession(selected, randomOrder: true);
        var visited = new List<string>();

        while (!session.Finished)
        {
            var current = session.Current!;
            visited.Add(current.Id);
            Assert.True(session.Answer(current.Romaji));
        }

        Assert.Equal(selected.Length, visited.Count);
        Assert.Equal(selected.Select(character => character.Id).Order(), visited.Order());
    }

    [Fact]
    public void EmptySession_IsAlreadyFinishedAndSafe()
    {
        var session = new KanaSession([], randomOrder: true);

        Assert.True(session.Finished);
        Assert.Equal(0, session.Total);
        Assert.Equal(0, session.Percent);
        Assert.Null(session.Current);
        Assert.False(session.Answer("a"));
        session.Reveal();
        session.Skip();
        Assert.Equal(0, session.Completed);
        Assert.Equal(0, session.Correct);
    }

    [Fact]
    public void Sanitize_RemovesUnknownIdsAndInvalidPreferencesButPreservesEmptySelection()
    {
        var preferences = new KanaPreferences
        {
            Selected = ["h-あ", "unknown"],
            ReviewCards = ["k-ア", "unknown"],
            Fonts = [-1, 0, 9],
            Script = (KanaScript)99,
            Group = KanaGroup.Extended
        };

        preferences.Sanitize();

        Assert.Equal(new[] { "h-あ" }, preferences.Selected);
        Assert.Equal(new[] { "k-ア" }, preferences.ReviewCards);
        Assert.Equal(new[] { 0 }, preferences.Fonts);
        Assert.Equal(KanaScript.Hiragana, preferences.Script);
        Assert.Equal(KanaGroup.Single, preferences.Group);

        preferences.Selected.Clear();
        preferences.Group = (KanaGroup)99;
        preferences.Fonts.Clear();
        preferences.Sanitize();

        Assert.Empty(preferences.Selected);
        Assert.Equal(KanaGroup.Single, preferences.Group);
        Assert.Equal(new[] { 0 }, preferences.Fonts);
    }

    [Fact]
    public void Sanitize_RepairsNullCollectionsAndDropsInvalidBestScores()
    {
        var preferences = new KanaPreferences
        {
            Selected = null!,
            ReviewCards = null!,
            Fonts = null!,
            Best = new Dictionary<string, KanaBest>
            {
                ["good"] = new(92, 4.5),
                ["percent-low"] = new(-1, 2),
                ["percent-high"] = new(101, 2),
                ["negative-time"] = new(80, -0.1),
                ["infinite-time"] = new(80, double.PositiveInfinity),
                ["null"] = null!
            }
        };

        preferences.Sanitize();

        Assert.Empty(preferences.Selected);
        Assert.Empty(preferences.ReviewCards);
        Assert.Equal(new[] { 0 }, preferences.Fonts);
        Assert.Equal(new Dictionary<string, KanaBest> { ["good"] = new(92, 4.5) }, preferences.Best);
    }

    private static KanaCharacter Character(string id) =>
        Assert.Single(KanaCatalog.Characters, character => character.Id == id);
}
