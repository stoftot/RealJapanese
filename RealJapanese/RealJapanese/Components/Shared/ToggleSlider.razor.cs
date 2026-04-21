using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public class ToggleSliderBase : ComponentBase
{
    protected sealed record SliderOption(int Index, string Text);

    [Parameter] public string LeftText { get; set; } = "Left";
    [Parameter] public string RightText { get; set; } = "Right";
    [Parameter] public string? MiddleText { get; set; }

    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }

    [Parameter] public int SelectedIndex { get; set; }
    [Parameter] public EventCallback<int> SelectedIndexChanged { get; set; }

    [Parameter] public string? CssClass { get; set; }

    protected IReadOnlyList<SliderOption> Options =>
        BuildOptions();

    protected int CurrentIndex => HasIndexedBinding ? SelectedIndex : (Value ? 1 : 0);

    private bool HasIndexedBinding =>
        SelectedIndexChanged.HasDelegate || !string.IsNullOrWhiteSpace(MiddleText);

    private IReadOnlyList<SliderOption> BuildOptions()
    {
        var options = new List<SliderOption>
        {
            new(0, LeftText)
        };

        if (!string.IsNullOrWhiteSpace(MiddleText))
        {
            options.Add(new SliderOption(1, MiddleText));
            options.Add(new SliderOption(2, RightText));
            return options;
        }

        options.Add(new SliderOption(1, RightText));
        return options;
    }

    protected async Task OnOptionClicked(int index)
    {
        if (HasIndexedBinding)
        {
            SelectedIndex = index;
            await SelectedIndexChanged.InvokeAsync(index);
            await InvokeAsync(StateHasChanged);
            return;
        }

        Value = index == 1;
        await ValueChanged.InvokeAsync(Value);
        await InvokeAsync(StateHasChanged);
    }
}
