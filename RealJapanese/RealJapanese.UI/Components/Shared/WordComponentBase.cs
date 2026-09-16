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
    
    protected Task OnKnownWordSelected(T word) => UpdateProgress(() => WordData.AddToVocab(word));
    protected Task OnKnownWordDeSelected(T word) => UpdateProgress(() => WordData.RemoveFromVocab(word));
    protected Task OnTrainingWordSelected(T word) => UpdateProgress(() => WordData.AddToTraining(word));
    protected Task OnTrainingWordDeSelected(T word) => UpdateProgress(() => WordData.RemoveFromTraining(word));
    protected Task OnRehearsingWordSelected(T word) => UpdateProgress(() => WordData.AddToRehearsing(word));
    protected Task OnRehearsingWordDeSelected(T word) => UpdateProgress(() => WordData.RemoveFromRehearsing(word));

    private Task UpdateProgress(Action persist)
    {
        // The repository changes category membership in one durable commit.
        persist();
        KnownIds = WordData.VocabWordIds.ToList();
        TrainingIds = WordData.TrainingWordIds.ToList();
        RehearsingIds = WordData.RehearsingWordIds.ToList();
        StateHasChanged();
        return Task.CompletedTask;
    }
}
