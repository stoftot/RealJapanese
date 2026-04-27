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
    [Parameter] public EventCallback PrimaryActionRequested { get; set; }

    [Inject] protected IJSRuntime JS { get; set; } = null!;

    protected ElementReference actionButtonRef;
    private DotNetObjectReference<FlashPracticeCardBase>? _dotNetRef;
    private string? _spaceShortcutId;
    protected string PrimaryActionLabel => ShowAnswer ? NextQuestionLabel : ShowAnswerLabel;

    protected internal Task FocusCardAsync() => actionButtonRef.FocusAsync().AsTask();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _spaceShortcutId = await JS.InvokeAsync<string>("blazorHelpers.registerSpaceShortcut", _dotNetRef);
            await FocusCardAsync();
        }
    }

    [JSInvokable]
    public Task HandleGlobalSpaceAsync()
        => TriggerPrimaryActionAsync();

    protected Task TriggerPrimaryActionAsync()
        => PrimaryActionRequested.InvokeAsync();

    public async ValueTask DisposeAsync()
    {
        if (_spaceShortcutId is not null)
        {
            try
            {
                await JS.InvokeVoidAsync("blazorHelpers.unregisterSpaceShortcut", _spaceShortcutId);
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _dotNetRef?.Dispose();
    }
}
