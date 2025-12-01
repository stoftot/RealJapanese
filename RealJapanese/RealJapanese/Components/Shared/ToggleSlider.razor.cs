using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public  class ToggleSliderBase : ComponentBase
{
    [Parameter] public string LeftText { get; set; } = "Left";
    [Parameter] public string RightText { get; set; } = "Right";

    // The selected side: false = left, true = right
    [Parameter] public bool Value { get; set; }

    // For two-way binding: @bind-Value
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }

    [Parameter] public string? CssClass { get; set; }

    protected async Task Toggle()
    {
        Value = !Value;
        await ValueChanged.InvokeAsync(Value);
    }
}