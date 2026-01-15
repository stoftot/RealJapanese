using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RealJapanese.Components.Shared;

public partial class NumberStepper : ComponentBase
{
    [Parameter] public string Label { get; set; } = string.Empty;
    
    [Parameter] public int Value { get; set; } = 0;
    [Parameter] public EventCallback<int> ValueChanged { get; set; }

    [Parameter] public int Min { get; set; } = 0;
    [Parameter] public int Max { get; set; } = 100;

    private async Task OnInputChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var newValue))
            return;

        newValue = Math.Clamp(newValue, Min, Max);

        if (newValue != Value)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(Value);
        }

    }
}