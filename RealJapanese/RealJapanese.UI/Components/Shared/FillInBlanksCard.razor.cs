using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public partial class FillInBlanksCardBase : ComponentBase
{
    // Content
    [Parameter] public string Header { get; set; } = "Fill in the blanks";
    [Parameter] public string ProgressText { get; set; } = string.Empty;
    [Parameter] public string Template { get; set; } = string.Empty;

    // Answers – ordered, 1:1 with blanks
    [Parameter] public List<string> Answers { get; set; } = new();

    // What marks a blank in the template, e.g. "__" or "§§"
    [Parameter] public string BlankToken { get; set; } = "__";

    // Navigation
    [Parameter] public EventCallback NextRequested { get; set; }

    // Internal state
    protected List<string> TemplateSegments { get; set; } = new();
    protected List<string> UserInputs { get; set; } = new();
    protected List<bool> Correctness { get; set; } = new();
    protected bool ShowFeedback { get; set; }

    protected int BlankCount => Math.Max(0, TemplateSegments.Count - 1);

    protected bool CanCheck => BlankCount > 0 && Answers.Count >= BlankCount;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        Answers ??= new List<string>();

        // Split the template into text segments around the blank token
        if (string.IsNullOrEmpty(Template))
        {
            TemplateSegments = new List<string> { string.Empty };
        }
        else
        {
            TemplateSegments = Template.Split(
                BlankToken,
                StringSplitOptions.None
            ).ToList();

            if (TemplateSegments.Count == 0)
            {
                TemplateSegments.Add(string.Empty);
            }
        }

        var blanks = BlankCount;
        if (blanks < 0) blanks = 0;

        EnsureListSize(UserInputs, blanks, string.Empty);
        EnsureListSize(Correctness, blanks, false);

        // If there are fewer answers than blanks, pad with empty strings
        while (Answers.Count < blanks)
        {
            Answers.Add(string.Empty);
        }

        // When question changes, hide old feedback
        ShowFeedback = false;
    }

    private static void EnsureListSize<T>(List<T> list, int size, T defaultValue)
    {
        if (list.Count < size)
        {
            for (var i = list.Count; i < size; i++)
            {
                list.Add(defaultValue);
            }
        }
        else if (list.Count > size)
        {
            list.RemoveRange(size, list.Count - size);
        }
    }

    protected void CheckAnswers()
    {
        var blanks = BlankCount;

        for (var i = 0; i < blanks; i++)
        {
            var expected = i < Answers.Count ? Answers[i] ?? string.Empty : string.Empty;
            var actual = i < UserInputs.Count ? UserInputs[i] ?? string.Empty : string.Empty;

            Correctness[i] = Normalize(actual) == Normalize(expected);
        }

        ShowFeedback = true;
        StateHasChanged();
    }

    protected async Task OnNextClicked()
    {
        // Clear inputs & feedback for next question
        for (var i = 0; i < UserInputs.Count; i++)
        {
            UserInputs[i] = string.Empty;
        }

        ShowFeedback = false;

        if (NextRequested.HasDelegate)
        {
            await NextRequested.InvokeAsync();
        }
    }

    protected string GetInputCss(int index)
    {
        var css = "form-control d-inline-block fill-blank-input";

        if (ShowFeedback)
        {
            css += Correctness[index]
                ? " border-success bg-success-subtle"
                : " border-danger bg-danger-subtle";
        }

        return css;
    }

    protected string GetAnswerCss(int index)
    {
        // Only the word, styled by correctness
        var css = "small";

        if (ShowFeedback)
        {
            css += Correctness[index] ? " text-success" : " text-danger";
        }

        return css;
    }

    private static string Normalize(string? s)
    {
        s ??= string.Empty;
        var trimmedNoWhitespace = new string(
            s.Trim().Where(c => !char.IsWhiteSpace(c)).ToArray()
        );

        return trimmedNoWhitespace.ToLowerInvariant();
    }
}
