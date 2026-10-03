using DataLoaders.Models.Genki;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Repositories.Genki;

namespace RealJapanese.Components.Pages.Genki;

public class GenkiBase : ComponentBase
{
    [Inject] protected GenkiCatalog Catalog { get; set; } = null!;
    [Inject] protected GenkiGenerator Generator { get; set; } = null!;
    [Parameter] public int? LessonNumber { get; set; }
    protected GenkiLesson? Lesson { get; private set; }
    protected GenkiExercise? Current { get; private set; }
    protected GenkiGrammarPoint? CurrentPoint => Lesson?.GrammarPoints.FirstOrDefault(x => x.Id == Current?.GrammarId);
    protected string? SelectedPoint { get; private set; }
    protected int VocabularyLesson { get; set; }
    protected string? Input { get; private set; }
    protected bool Revealed { get; private set; }
    protected bool Finished { get; private set; }
    protected string Feedback { get; private set; } = "";
    protected string? Error { get; private set; }
    protected int Reviewed { get; private set; }
    private readonly Queue<GenkiExercise> pending = new();
    protected ElementReference PracticeHeading;
    private bool focusPractice;
    protected string ProgressText => $"{Reviewed} reviewed · {pending.Count + 1} remaining";
    protected string VisibleAnswer => Current is null ? "" : string.Join(" / ", Current.ModelAnswers);

    protected override void OnParametersSet()
    {
        Lesson = LessonNumber.HasValue ? Catalog.FindLesson(LessonNumber.Value) : null;
        VocabularyLesson = Lesson?.Number ?? 1;
        Stop();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!focusPractice || Current is null) return;
        focusPractice = false;
        // Starting a point near the bottom of a long recap must bring the new prompt into view.
        await PracticeHeading.FocusAsync();
    }

    protected void Start(string? pointId)
    {
        if (Lesson is null) return;
        Stop();
        SelectedPoint = pointId;
        try
        {
            // Each pattern gets a turn; round-robin across points avoids drilling one form repeatedly.
            var sets = Lesson.GrammarPoints.Where(p => pointId is null || p.Id == pointId)
                .Select(p => (Point: p, Schemas: p.Schemas.OrderBy(_ => Random.Shared.Next()).ToArray())).ToArray();
            for (var index = 0; index < sets.Max(x => x.Schemas.Length); index++)
                foreach (var set in sets.Where(x => x.Schemas.Length > index))
                    pending.Enqueue(Generator.Generate(Lesson.Number, set.Point.Id, set.Schemas[index].Id, VocabularyLesson));
            Advance();
            focusPractice = Current is not null;
        }
        catch (InvalidOperationException)
        {
            pending.Clear();
            Current = null;
            Error = "This practice pattern has no compatible vocabulary at this level. Choose another grammar point.";
        }
    }

    protected void Stop()
    {
        pending.Clear(); Current = null; Finished = false; Revealed = false;
        Input = ""; Feedback = ""; Error = null; Reviewed = 0; focusPractice = false;
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
            : "Compare your sentence with the models. A different sentence may also be valid; check the grammar, meaning and register.";
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
        Finished = Current is null; Revealed = false; Input = ""; Feedback = "";
    }

    protected string PrerequisiteTitle(string id) => Catalog.Lessons.SelectMany(x => x.GrammarPoints)
        .First(p => p.Id == id).Title;
    protected string PrerequisiteLink(string id) => $"genki/{Catalog.Lessons.First(l => l.GrammarPoints.Any(p => p.Id == id)).Number}#{id}";
}
