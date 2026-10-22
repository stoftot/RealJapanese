using Microsoft.AspNetCore.Components;
using Repositories.Audio;
using Repositories.DTOs;
using Repositories.Exstensions;

namespace RealJapanese.Components.Shared;

public class ListeningPracticeBase : FlashCardPracticeBase
{
    [Parameter, EditorRequired] public List<VocabularyEntry> Words { get; set; } = [];
    [Parameter, EditorRequired] public List<VocabularyEntry> ChoiceWords { get; set; } = [];
    [Parameter, EditorRequired] public StudyAudioCatalog Audio { get; set; } = null!;
    private readonly Dictionary<QuestionAnswerDto, (StudyAudioClip Clip, List<string> Alternatives)> details = [];
    protected List<string> Choices { get; private set; } = [];
    protected int Attempt { get; private set; }
    protected bool WasCorrect { get; private set; }
    private string? selectedAnswer;
    protected StudyAudioClip CurrentClip => details[Questions[currentQuestionIndex]].Clip;
    protected int UnavailableCount => Words.Count - details.Count;
    protected string EmptyMessage => Words.Count == 0
        ? "Return to the study page and add words to the selected list."
        : details.Count == 0
            ? "No usable audio is available for this list. Export clips in Audio Studio, then reopen this practice. At least three distinct meanings are needed in this vocabulary area."
            : "This chunk is empty. Select another chunk or reduce the number of chunks.";

    protected override void OnInitialized()
    {
        foreach (var word in Words.DistinctBy(word => (word.Dataset, word.SourceId)))
        {
            var clip = Audio.Find(word.Dataset, word.SourceId, word.Japanese, word.Kana);
            if (clip is null || string.IsNullOrWhiteSpace(word.English)) continue;
            var meanings = Meanings(word.English);
            var alternatives = ChoiceWords.Where(other =>
                    !string.IsNullOrWhiteSpace(other.English) &&
                    !string.Equals(other.Kana.Trim(), word.Kana.Trim(), StringComparison.Ordinal) &&
                    !string.Equals(other.Japanese.Trim(), word.Japanese.Trim(), StringComparison.Ordinal) &&
                    !Meanings(other.English).Overlaps(meanings))
                .Select(other => other.English.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (alternatives.Count < 2) continue;
            var question = new QuestionAnswerDto
            {
                Question = $"{word.Dataset}/{word.SourceId}", Answer = word.English.Trim(), SourceWord = word
                // Listening deliberately has no RecordAnswer callback: no progress moves or rehearsal credit.
            };
            details.Add(question, (clip, alternatives));
        }
        OrginalQuestions = details.Keys;
        UpdateQuestions();
    }

    private static HashSet<string> Meanings(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"\([^)]*\)", "")
            .Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    protected override void OnQuestionsUpdated() => PrepareQuestion();

    protected override void NextQuestion()
    {
        base.NextQuestion();
        PrepareQuestion();
    }

    private void PrepareQuestion()
    {
        Attempt++;
        selectedAnswer = null;
        WasCorrect = false;
        Choices = [];
        if (Questions.Count == 0) return;
        var alternatives = details[Questions[currentQuestionIndex]].Alternatives.ToList();
        alternatives.Shuffle();
        Choices = [CurrentAnswer, .. alternatives.Take(2)];
        Choices.Shuffle();
    }

    protected void Choose(string answer)
    {
        if (showAnswer || !Choices.Contains(answer)) return;
        selectedAnswer = answer;
        WasCorrect = answer == CurrentAnswer;
        if (WasCorrect) ShowAnswer();
        else RevealAnswer();
    }

    protected string ChoiceClass(string answer) => !showAnswer ? "btn-outline-primary"
        : answer == CurrentAnswer ? "btn-success" : answer == selectedAnswer ? "btn-danger" : "btn-outline-secondary";

    protected Task AdvanceAsync() => showAnswer ? CompleteQuestionAsync() : Task.CompletedTask;
}
