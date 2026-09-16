using DataLoaders;
using DataLoaders.Models;
using Repositories.Sync;

namespace Repositories.Bases;

public abstract class WordDataBase<T> where T : Word
{
    private readonly ProgressStore progress;
    private readonly string dataset;
    public IEnumerable<T> Words { get; }

    protected WordDataBase(RepositoryPaths paths, string dataset, string dataFileName)
    {
        this.dataset = dataset;
        progress = paths.Progress;
        var words = new JsonLoader<T>(Path.Combine(paths.CatalogRoot, dataset), dataFileName).Load().ToList();
        var nextId = words.Where(w => w.Id >= 0).Select(w => w.Id).DefaultIfEmpty(-1).Max() + 1;
        foreach (var word in words.Where(w => w.Id == -1)) word.Id = nextId++;
        Words = words;
    }

    private VocabSaveFile ReadProgress()
    {
        var saved = progress.Read().Data[dataset];
        var ids = Words.Select(word => word.Id).ToHashSet();
        saved.KnownIds.RemoveAll(id => !ids.Contains(id));
        saved.TrainingIds.RemoveAll(id => !ids.Contains(id));
        saved.RehearsingIds.RemoveAll(id => !ids.Contains(id));
        return saved;
    }

    public List<T> VocabWords => ResolveWords(VocabWordIds);
    public List<T> TrainingWords => ResolveWords(TrainingWordIds);
    public List<T> RehearsingWords => ResolveWords(RehearsingWordIds);
    public IEnumerable<int> VocabWordIds => ReadProgress().KnownIds;
    public IEnumerable<int> TrainingWordIds => ReadProgress().TrainingIds;
    public IEnumerable<int> RehearsingWordIds => ReadProgress().RehearsingIds;

    public void AddToVocab(T word) => SetCategory(word, WordPracticeCategory.Known);
    public void AddToTraining(T word) => SetCategory(word, WordPracticeCategory.Training);
    public void AddToRehearsing(T word) => SetCategory(word, WordPracticeCategory.Rehearsing);
    public void RemoveFromVocab(T word) => progress.Change(dataset, saved => saved.KnownIds.Remove(word.Id));
    public void RemoveFromTraining(T word) => progress.Change(dataset, saved => saved.TrainingIds.Remove(word.Id));
    public void RemoveFromRehearsing(T word) => progress.Change(dataset, saved => saved.RehearsingIds.Remove(word.Id));

    private void SetCategory(T word, WordPracticeCategory category)
    {
        if (!Words.Any(candidate => candidate.Id == word.Id))
            throw new ArgumentException("The word is not in this catalog.", nameof(word));
        progress.Change(dataset, saved =>
        {
            saved.KnownIds.Remove(word.Id);
            saved.TrainingIds.Remove(word.Id);
            saved.RehearsingIds.Remove(word.Id);
            var target = category switch
            {
                WordPracticeCategory.Known => saved.KnownIds,
                WordPracticeCategory.Training => saved.TrainingIds,
                _ => saved.RehearsingIds
            };
            target.Add(word.Id);
        });
    }

    public List<T> GetWords(WordPracticeCategory category) => category switch
    {
        WordPracticeCategory.Training => TrainingWords,
        WordPracticeCategory.Rehearsing => RehearsingWords,
        _ => VocabWords
    };

    private List<T> ResolveWords(IEnumerable<int> ids)
    {
        var wordsById = Words.GroupBy(word => word.Id).ToDictionary(group => group.Key, group => group.First());
        return ids.Where(wordsById.ContainsKey).Select(id => wordsById[id]).ToList();
    }
}
