using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories;
using Repositories.DTOs;

namespace RealJapanese.Components.Pages;

public partial class ParticlesBase : ComponentBase
{
    [Inject] private ParticlesData ParticlesData { get; set; } = null!;
    protected List<FillInBlanksDto> Questions { get; set; } = new();
    protected int currentIndex = 0;

    protected double UiScale { get; set; } = 1.0;

    protected FillInBlanksDto CurrentItem =>
        Questions.Count == 0 ? new FillInBlanksDto { Header = "", Template = "", Answers = new() }
            : Questions[currentIndex];

    protected string ProgressText =>
        Questions.Count == 0 ? string.Empty
            : $"{currentIndex + 1} / {Questions.Count}";

    protected override void OnInitialized()
    {
        Questions = ParticlesData.FillInBlanksQuestions().OrderBy(_ => Guid.NewGuid()).ToList();
    }

    protected Task GoToNextQuestionAsync()
    {
        if (Questions.Count == 0)
            return Task.CompletedTask;

        currentIndex++;

        if (currentIndex >= Questions.Count)
        {
            Questions = Questions.OrderBy(_ => Guid.NewGuid()).ToList();
            currentIndex = 0;
        }

        StateHasChanged();
        return Task.CompletedTask;
    }
}