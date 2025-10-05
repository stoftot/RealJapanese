using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public partial class PracticeShellBase : ComponentBase
{
    [Parameter] public bool ShowScaleToolbar { get; set; } = true;

    // two-way bindable UiScale
    [Parameter] public double UiScale { get; set; } = 1.0;
    [Parameter] public EventCallback<double> UiScaleChanged { get; set; }

    // slider options
    [Parameter] public double Min { get; set; } = 0.8;
    [Parameter] public double Max { get; set; } = 1.6;
    [Parameter] public double Step { get; set; } = 0.05;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected Task OnScaleInput(ChangeEventArgs e)
    {
        var v = Convert.ToDouble(e.Value);
        return UiScaleChanged.InvokeAsync(v);
    }
}