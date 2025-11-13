using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;
using Repositories.DTOs;

namespace RealJapanese.Components.Pages;

public partial class ParticlesBase : ComponentBase
{
    // You can inject your data service here instead
    // [Inject] public ParticleData ParticleData { get; set; } = null!;

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
        // TODO: replace with real data, e.g. from a repository
        Questions = new List<FillInBlanksDto>
        {
            new()
            {
                Header = "Particles - basics",
                Template = "私はきのう学校__行きました。", // "__" replaced by input
                Answers = new List<string> { "に" }
            },
            new()
            {
                Header = "Particles - two blanks",
                Template = "猫__テーブル__寝ています。",
                Answers = new List<string> { "は", "の上で" } // or just "の上に" etc.
            }
        }.OrderBy(_ => Guid.NewGuid()).ToList();
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