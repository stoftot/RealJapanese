using DataLoaders.Models;
using Microsoft.AspNetCore.Components;
using Repositories.Bases;

namespace RealJapanese.Components.Shared;

public abstract class WordComponentBase<T> : ComponentBase where T : Word
{
    protected abstract WordDataBase<T> WordData { get; }
    protected List<int> KnownIds { get; set; } = [];
    protected List<int> TrainingIds { get; set; } = [];
    protected List<int> RehearsingIds { get; set; } = [];
    protected List<T> AllWords { get; set; } = [];
    protected WordPracticeCategory SelectedCategory { get; set; } = WordPracticeCategory.Known;
    protected int SelectedCategoryIndex
    {
        get => (int)SelectedCategory;
        set => SelectedCategory = value switch
        {
            1 => WordPracticeCategory.Rehearsing,
            2 => WordPracticeCategory.Training,
            _ => WordPracticeCategory.Known
        };
    }

    protected string SelectedCategoryQueryValue => SelectedCategory.ToQueryValue();

    protected override void OnInitialized()
    {
        AllWords = WordData.Words.ToList();
        KnownIds = WordData.VocabWordIds.ToList();
        TrainingIds = WordData.TrainingWordIds.ToList();
        RehearsingIds = WordData.RehearsingWordIds.ToList();
        StateHasChanged();
    }
    
    protected Task OnKnownWordSelected(T word)
    {
        RemoveFromList(TrainingIds, word.Id, () => WordData.RemoveFromTraining(word));
        RemoveFromList(RehearsingIds, word.Id, () => WordData.RemoveFromRehearsing(word));
        AddToList(KnownIds, word.Id, () => WordData.AddToVocab(word));
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnKnownWordDeSelected(T word)
    {
        RemoveFromList(KnownIds, word.Id, () => WordData.RemoveFromVocab(word));
        StateHasChanged();
        return Task.CompletedTask;
    }
    
    protected Task OnTrainingWordSelected(T word)
    {
        RemoveFromList(KnownIds, word.Id, () => WordData.RemoveFromVocab(word));
        RemoveFromList(RehearsingIds, word.Id, () => WordData.RemoveFromRehearsing(word));
        AddToList(TrainingIds, word.Id, () => WordData.AddToTraining(word));
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnTrainingWordDeSelected(T word)
    {
        RemoveFromList(TrainingIds, word.Id, () => WordData.RemoveFromTraining(word));
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnRehearsingWordSelected(T word)
    {
        RemoveFromList(KnownIds, word.Id, () => WordData.RemoveFromVocab(word));
        RemoveFromList(TrainingIds, word.Id, () => WordData.RemoveFromTraining(word));
        AddToList(RehearsingIds, word.Id, () => WordData.AddToRehearsing(word));
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected Task OnRehearsingWordDeSelected(T word)
    {
        RemoveFromList(RehearsingIds, word.Id, () => WordData.RemoveFromRehearsing(word));
        StateHasChanged();
        return Task.CompletedTask;
    }

    private static void AddToList(List<int> ids, int id, Action persist)
    {
        if (ids.Contains(id))
        {
            return;
        }

        ids.Add(id);
        persist();
    }

    private static void RemoveFromList(List<int> ids, int id, Action persist)
    {
        if (!ids.Contains(id))
        {
            return;
        }

        ids.Remove(id);
        persist();
    }
}
