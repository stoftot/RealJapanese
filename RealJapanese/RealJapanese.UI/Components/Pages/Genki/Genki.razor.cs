using DataLoaders.Models;
using DataLoaders.Models.Genki;
using Microsoft.AspNetCore.Components;
using Repositories.Genki;

namespace RealJapanese.Components.Pages.Genki;

public class GenkiBase : ComponentBase
{
    [Inject] protected GenkiCatalog Catalog { get; set; } = null!;
    [Inject] protected GenkiPracticeService Practice { get; set; } = null!;
    [Inject] protected GenkiVocabulary Vocabulary { get; set; } = null!;

    [Parameter] public int? LessonNumber { get; set; }
    protected GenkiLesson? Lesson { get; private set; }
    protected GenkiExercise? Current { get; private set; }
    protected GenkiGrammarPoint? CurrentPoint => Current is null ? null : Catalog.Point(Current.GrammarId);
    protected string? SelectedPoint { get; private set; }
    protected string? Input { get; private set; }
    protected bool Revealed { get; private set; }
    protected bool Finished { get; private set; }
    protected string Feedback { get; private set; } = "";
    protected string? EmptyMessage { get; private set; }
    protected int Reviewed { get; private set; }
    protected bool HasPublishedQuestions => Practice.HasPublishedQuestions;
    private readonly Queue<GenkiExercise> pending = new();
    protected ElementReference PracticeHeading;
    private bool focusPractice;

    protected string ProgressText => $"{Reviewed} reviewed · {pending.Count + (Current is null ? 0 : 1)} remaining";
    protected string VisibleAnswer => Current is null ? "" : string.Join(" / ", Current.ModelAnswers);
    protected IReadOnlyList<Word> CurrentWords => Current is null
        ? []
        : Current.DisplayAnswers.SelectMany(answer => answer.RequiredWords)
            .Distinct().Select(reference => Vocabulary.Resolve(reference).Word).ToArray();

    protected override void OnParametersSet()
    {
        Lesson = LessonNumber.HasValue ? Catalog.FindLesson(LessonNumber.Value) : null;
        SelectedPoint = null;
        Stop();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusPractice || Current is null) return;
        focusPractice = false;
        await PracticeHeading.FocusAsync();
    }

    protected void Start(string? pointId)
    {
        if (Lesson is null) return;
        Stop();
        SelectedPoint = pointId;
        if (!Practice.HasPublishedQuestions)
        {
            EmptyMessage = "The offline question bank has no published questions yet.";
            return;
        }

        foreach (var exercise in Practice.CreateRound(Lesson.Number, pointId)) pending.Enqueue(exercise);
        if (pending.Count == 0)
        {
            EmptyMessage = "No published questions match this grammar and the words you have marked known.";
            return;
        }

        Advance();
        focusPractice = Current is not null;
    }

    protected void Stop()
    {
        pending.Clear(); Current = null; Finished = false; Revealed = false;
        Input = ""; Feedback = ""; EmptyMessage = null; Reviewed = 0; focusPractice = false;
    }

    protected void SetInput(string? value)
    {
        Input = value;
        if (Revealed) Compare();
    }

    protected void Compare()
    {
        if (Current is null) return;
        Revealed = true;
        Feedback = Current.MatchesModel(Input ?? "")
            ? "Your sentence matches a supplied model. Check that you understand the form before continuing."
            : "A different sentence may also be valid. Compare the meaning, grammar and register with the approved answers.";
    }

    protected void Next()
    {
        if (!Revealed || Current is null) return;
        Reviewed++;
        Advance();
    }

    protected void Retry()
    {
        if (!Revealed || Current is null) return;
        pending.Enqueue(Current);
        Next();
    }

    private void Advance()
    {
        Current = pending.TryDequeue(out var exercise) ? exercise : null;
        Finished = Current is null && Reviewed > 0;
        Revealed = false; Input = ""; Feedback = "";
    }

    protected string PrerequisiteTitle(string id) => Catalog.Point(id).Title;
    protected string PrerequisiteLink(string id) => $"genki/{Catalog.LessonNumber(id)}#{id}";
}
