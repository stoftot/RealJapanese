using Bunit;
using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;

namespace RealJapanese.ComponentTests;

/// <summary>Checks component-level scale and numeric control bounds while visual and assistive behavior stays browser-owned.</summary>
public sealed class SharedPracticeControlTests
{
    [Fact]
    public void PracticeScaleEmitsTheRequestedValueAndUpdatesItsVisiblePercentage()
    {
        using var test = new ComponentTestContext();
        var value = 1.0;
        var cut = test.Context.Render<PracticeShell>(parameters => parameters
            .Add(component => component.UiScale, value)
            .Add(component => component.UiScaleChanged, EventCallback.Factory.Create<double>(this, next => value = next)));

        cut.Find("input[type=range]").Input("1.6");
        cut.Render(parameters => parameters
            .Add(component => component.UiScale, value)
            .Add(component => component.UiScaleChanged, EventCallback.Factory.Create<double>(this, next => value = next)));

        Assert.Equal(1.6, value);
        Assert.Contains("160", cut.Markup);
        Assert.Equal("practice-size", cut.Find("label").GetAttribute("for"));
    }

    [Theory]
    [InlineData("-20", 1)]
    [InlineData("50", 10)]
    public void NumberStepperClampsTypedValuesToItsBounds(string entered, int expected)
    {
        using var test = new ComponentTestContext();
        var value = 5;
        var cut = test.Context.Render<NumberStepper>(parameters => parameters
            .Add(component => component.Label, "Chunks")
            .Add(component => component.Value, value)
            .Add(component => component.Min, 1)
            .Add(component => component.Max, 10)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<int>(this, next => value = next)));

        cut.Find("input[type=number]").Change(entered);

        Assert.Equal(expected, value);
    }
}
