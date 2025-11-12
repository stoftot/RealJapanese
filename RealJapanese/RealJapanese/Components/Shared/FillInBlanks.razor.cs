using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text.RegularExpressions;

namespace RealJapanese.Components.Shared;

public partial class FillInBlanksBase : ComponentBase
{
    [Parameter] public string? TopText { get; set; }
    [Parameter] public string Template { get; set; } = "";
    [Parameter] public IReadOnlyList<string> Answers { get; set; } = Array.Empty<string>();
    [Parameter] public string Placeholder { get; set; } = "__";
    [Parameter] public string InputWidth { get; set; } = "5.5ch";
    [Parameter] public bool SpaceAdvances { get; set; } = true;
    [Parameter] public bool ShowAnswerBelow { get; set; } = true;
    [Parameter] public EventCallback<FillInResult> OnChecked { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<string>> OnAnswersChanged { get; set; }

    protected List<string> Chunks { get; private set; } = new();
    protected int BlankCount => Answers.Count;
    protected string[] UserAnswers = Array.Empty<string>();
    protected bool ShowResults { get; set; }
    protected bool ReadyToSubmit => BlankCount > 0;
    protected ElementReference[] inputRefs = Array.Empty<ElementReference>();
    protected bool questionChanged = false;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // detect if BlankCount changed or Template changed
        if (Chunks.Count != (inputRefs?.Length ?? 0))
        {
            Chunks = SplitOnBlanks(Template);
            UserAnswers = Enumerable.Repeat(string.Empty, BlankCount).ToArray();
            inputRefs = new ElementReference[BlankCount];
            ShowResults = false;
            questionChanged = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (questionChanged)
        {
            questionChanged = false;
            if (inputRefs.Length > 0)
            {
                await inputRefs[0].FocusAsync();
            }
        }
    }

    private static List<string> SplitOnBlanks(string template)
        => Regex.Split(template ?? "", "__", RegexOptions.Compiled).ToList();

    protected void OnTyping(int index, ChangeEventArgs e)
    {
        // (Not strictly needed since @bind handles it)
        _ = OnAnswersChanged.InvokeAsync(UserAnswers.ToArray());
    }

    protected async Task OnKeyDown(int index, KeyboardEventArgs e)
    {
        if (SpaceAdvances && (e.Key == " " || e.Key == "Space" || e.Key == "Spacebar"))
        {
            await MoveNextOrSubmit(index);
        }
        else if (e.Key == "Enter")
        {
            await MoveNextOrSubmit(index);
        }
    }

    protected bool ShouldPreventDefault(int index)
    {
        // Prevent default only if we want to capture space/enter
        return SpaceAdvances;
    }

    private async Task MoveNextOrSubmit(int index)
    {
        if (index < inputRefs.Length - 1)
        {
            var nextIndex = index + 1;
            await InvokeAsync(StateHasChanged);  // ensure any render is done
            await Task.Yield();                   // allow render loop to catch up
            await inputRefs[nextIndex].FocusAsync();
        }
        else
        {
            await Submit();
        }
    }

    private static string Normalize(string s)
        => new string((s ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());

    protected bool IsCorrect(int i)
        => Normalize(UserAnswers[i]) == Normalize(Answers[i]);

    protected void Reveal()
    {
        ShowResults = true;
        StateHasChanged();
    }

    protected async Task Submit()
    {
        ShowResults = true;
        var per = Enumerable.Range(0, BlankCount).Select(IsCorrect).ToArray();
        var result = new FillInResult
        {
            Provided = UserAnswers.ToArray(),
            Expected = Answers.ToArray(),
            PerBlankCorrect = per,
            AllCorrect = per.All(x => x),
        };
        await OnChecked.InvokeAsync(result);
        StateHasChanged();
    }

    public class FillInResult
    {
        public required string[] Provided { get; set; }
        public required string[] Expected { get; set; }
        public required bool[] PerBlankCorrect { get; set; }
        public bool AllCorrect { get; set; }
    }
}
