using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
namespace RealJapanese.Components.Shared;

public class FlashPracticeCardBase : ComponentBase, IAsyncDisposable
{
    [Parameter] public string Title { get; set; } = "Practice";
    [Parameter] public string ProgressText { get; set; } = "";
    [Parameter] public string Question { get; set; } = "";
    [Parameter] public string Answer { get; set; } = "";
    [Parameter] public bool ShowAnswer { get; set; }
    [Parameter] public string ShowAnswerLabel { get; set; } = "Show answer";
    [Parameter] public string NextQuestionLabel { get; set; } = "Next question";
    [Parameter] public string ForgotLabel { get; set; } = "Forgot";
    [Parameter] public EventCallback PrimaryActionRequested { get; set; }
    [Parameter] public EventCallback ForgotRequested { get; set; }

    [Inject] protected IJSRuntime JS { get; set; } = null!;

    protected ElementReference actionButtonRef;
    private DotNetObjectReference<FlashPracticeCardBase>? _dotNetRef;
    private string? _shortcutId;
    protected string PrimaryActionLabel => ShowAnswer ? NextQuestionLabel : ShowAnswerLabel;

    protected internal Task FocusCardAsync() => actionButtonRef.FocusAsync().AsTask();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _shortcutId = await JS.InvokeAsync<string>("blazorHelpers.registerFlashCardShortcuts", _dotNetRef);
            await FocusCardAsync();
        }
    }

    [JSInvokable]
    public Task HandleGlobalSpaceAsync()
        => TriggerPrimaryActionAsync();

    [JSInvokable]
    public Task HandleGlobalBackspaceAsync()
        => TriggerForgotAsync();

    protected Task TriggerPrimaryActionAsync()
        => PrimaryActionRequested.InvokeAsync();

    protected Task TriggerForgotAsync()
        => ForgotRequested.InvokeAsync();

    public async ValueTask DisposeAsync()
    {
        if (_shortcutId is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("blazorHelpers.unregisterFlashCardShortcuts", _shortcutId);
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _dotNetRef?.Dispose();
    }
}
