using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Repositories.Bases;

namespace RealJapanese.Components.Shared;

public abstract class WordComponentBase<T> : ComponentBase where T : Word
{
    protected abstract WordDataBase<T> WordData { get; }
    protected List<int> KnownIds { get; set; } = [];
    protected List<int> TrainingIds { get; set; } = [];
    protected List<T> AllWords { get; set; } = [];
    protected bool Training { get; set; } = false;

    protected override void OnInitialized()
    {
        AllWords = WordData.Words.ToList();
        KnownIds = WordData.VocabWordIds.ToList();
        TrainingIds = WordData.TrainingWordIds.ToList();
        StateHasChanged();
    }
    
    protected Task OnKnownWordSelected(T word)
    {
        WordData.AddToVocab(word);
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnKnownWordDeSelected(T word)
    {
        WordData.RemoveFromVocab(word);
        StateHasChanged();
        return Task.CompletedTask;
    }
    
    protected Task OnTrainingWordSelected(T word)
    {
        WordData.AddToTraining(word);
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnTrainingWordDeSelected(T word)
    {
        WordData.RemoveFromTraining(word);
        StateHasChanged();
        return Task.CompletedTask;
    }
}