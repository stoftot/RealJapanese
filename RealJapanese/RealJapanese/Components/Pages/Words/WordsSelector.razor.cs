using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Repositories;

namespace RealJapanese.Components.Pages.Words;

public class WordsSelectorBase : ComponentBase
{
    [Inject] private WordData WordData { get; set; } = null!;

    protected List<int> KnownIds { get; set; } = [];
    protected List<int> TrainingIds { get; set; } = [];
    protected List<Word> AllWords { get; set; } = [];
    protected bool Training { get; set; } = false;

    protected override void OnInitialized()
    {
        // For demo: first 3 known, last 2 in training
        AllWords = WordData.Words.ToList();
        KnownIds = WordData.VocabWordIds.ToList();
        TrainingIds = WordData.TrainingWordIds.ToList();
        StateHasChanged();
    }

    protected List<Word> SelectedWords() => Training
        ? AllWords.Where(w => TrainingIds.Contains(w.Id)).ToList()
        : AllWords.Where(w => KnownIds.Contains(w.Id)).ToList();

    // Handle clicks in “Known” list (optional logic)
    protected Task OnKnownWordSelected(Word word)
    {
        WordData.AddToVocab(word);
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnKnownWordDeSelected(Word word)
    {
        WordData.RemoveFromVocab(word);
        StateHasChanged();
        return Task.CompletedTask;
    }

    // Handle clicks in “Training” list (optional logic)
    protected Task OnTrainingWordSelected(Word word)
    {
        WordData.AddToTraining(word);
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnTrainingWordDeSelected(Word word)
    {
        WordData.RemoveFromTraining(word);
        StateHasChanged();
        return Task.CompletedTask;
    }
}