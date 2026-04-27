using Microsoft.AspNetCore.Components;
namespace RealJapanese.Components.Shared;

public class FlashPracticeCardBase : ComponentBase
{
    [Parameter] public string Title { get; set; } = "Practice";
    [Parameter] public string ProgressText { get; set; } = "";
    [Parameter] public string Question { get; set; } = "";
    [Parameter] public string Answer { get; set; } = "";
    [Parameter] public bool ShowAnswer { get; set; }
    [Parameter] public string ShowAnswerLabel { get; set; } = "Show answer";
    [Parameter] public string NextQuestionLabel { get; set; } = "Next question";
    [Parameter] public EventCallback PrimaryActionRequested { get; set; }

    protected ElementReference actionButtonRef;
    protected string PrimaryActionLabel => ShowAnswer ? NextQuestionLabel : ShowAnswerLabel;

    protected internal Task FocusCardAsync() => actionButtonRef.FocusAsync().AsTask();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await FocusCardAsync();
        }
    }

    protected Task TriggerPrimaryActionAsync()
        => PrimaryActionRequested.InvokeAsync();
}
