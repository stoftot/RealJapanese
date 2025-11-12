using Microsoft.AspNetCore.Components;
using RealJapanese.Components.Shared;

namespace RealJapanese.Components.Pages;

public partial class ParticlesPracticeBase : PracticeShellBase
{
    public class ParticleItem
    {
        public string English { get; set; } = "";
        public string JapaneseTemplate { get; set; } = ""; // "__" markers
        public List<string> MissingParticles { get; set; } = new();
    }

    protected List<ParticleItem> Items { get; set; } = new();
    protected int Index { get; set; }

    protected override void OnInitialized()
    {
        // Seed with your samples; replace with your data source as desired.
        Items = new()
        {
            new ParticleItem
            {
                English = "I have a dog.",
                JapaneseTemplate = "私__犬を飼っています。",
                MissingParticles = new() { "は" }
            },
            new ParticleItem
            {
                English = "In the afternoon, I took a walk in the park with my sister.",
                JapaneseTemplate = "午後__姉__公園__散歩しました。",
                MissingParticles = new() { "に", "と", "で" }
            },
        };
    }

    protected void Next()
    {
        Index++;
        if (Index >= Items.Count) Index = 0;
    }

    protected void Prev()
    {
        Index--;
        if (Index < 0) Index = Items.Count - 1;
    }

    protected Task HandleChecked(FillInBlanksBase.FillInResult result)
    {
        // You can log progress, store stats, or pop a toast here.
        // Example: if (result.AllCorrect) …
        return Task.CompletedTask;
    }

    protected Task HandleAnswersChanged(IReadOnlyList<string> current)
    {
        // Optional live tracking
        return Task.CompletedTask;
    }
}