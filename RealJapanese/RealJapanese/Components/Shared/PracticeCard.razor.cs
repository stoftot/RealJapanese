using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace RealJapanese.Components.Shared;

public class PracticeCardBase : ComponentBase
{
    // Content
    [Parameter] public string Title { get; set; } = "Practice";
    [Parameter] public string ProgressText { get; set; } = "";
    [Parameter] public string Question { get; set; } = "";
    [Parameter] public string Answer { get; set; } = "";

    // Input
    [Parameter] public string? UserInput { get; set; }
    [Parameter] public EventCallback<string?> UserInputChanged { get; set; }
    [Parameter] public string Placeholder { get; set; } = "Type your answer…";

    // Behavior
    [Parameter] public bool ShowAnswer { get; set; }
    [Parameter] public EventCallback RevealRequested { get; set; }
    [Parameter] public EventCallback NextRequested { get; set; }
    [Parameter] public string RevealLabel { get; set; } = "Show answer";
    [Parameter] public string NextLabel { get; set; } = "Next";
    [Parameter] public bool NextDisabled { get; set; } = false;

    [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }

    protected ElementReference answerInputRef;

    // Let parents focus the input even though it lives here
    protected internal Task FocusInput() => answerInputRef.FocusAsync().AsTask();

    protected Task OnTyping(ChangeEventArgs e)
        => UserInputChanged.InvokeAsync(e.Value?.ToString());

    protected Task OnKeyDownProxy(KeyboardEventArgs e)
        => OnKeyDown.HasDelegate ? OnKeyDown.InvokeAsync(e) : Task.CompletedTask;
}