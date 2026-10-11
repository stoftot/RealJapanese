using DataLoaders;
using DataLoaders.Models;
using Repositories.Sync;

namespace Repositories.Bases;

public abstract class WordDataBase<T> where T : Word
{
    private readonly ProgressStore progress;
    private readonly string dataset;
    private readonly TimeProvider clock;
    public IEnumerable<T> Words { get; }

    protected WordDataBase(RepositoryPaths paths, string dataset, string dataFileName)
    {
        this.dataset = dataset;
        progress = paths.Progress;
        clock = paths.Clock;
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
    public void RemoveFromVocab(T word) => RemoveFromCategory([word], WordPracticeCategory.Known);
    public void RemoveFromTraining(T word) => RemoveFromCategory([word], WordPracticeCategory.Training);
    public void RemoveFromRehearsing(T word) => RemoveFromCategory([word], WordPracticeCategory.Rehearsing);

    /// <summary>Records an answer and returns whether the item moved to another study list.</summary>
    public bool RecordPracticeAnswer(int wordId, bool correct)
    {
        if (!Words.Any(word => word.Id == wordId))
            throw new ArgumentException("A word is not in this catalog.", nameof(wordId));
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var moved = false;
        progress.ChangeIf(dataset, saved =>
        {
            var known = saved.KnownIds.Contains(wordId);
            if (!known && !saved.RehearsingIds.Contains(wordId)) return false;
            var state = saved.Rehearsal.GetValueOrDefault(wordId) ?? new RehearsalProgress();
            if (!correct)
            {
                var reset = state with { CorrectDays = 0, LastResetDay = today, AwaitingRetry = false };
                if (!known && reset == state) return false;
                if (known)
                {
                    saved.KnownIds.Remove(wordId);
                    saved.RehearsingIds.Add(wordId);
                    moved = true;
                }
                saved.Rehearsal[wordId] = reset;
                return true;
            }
            if (known)
            {
                // Older saves can still contain a Known item awaiting its retry.
                if (!state.AwaitingRetry) return false;
                saved.KnownIds.Remove(wordId);
                saved.RehearsingIds.Add(wordId);
                saved.Rehearsal[wordId] = state with { AwaitingRetry = false };
                moved = true;
                return true;
            }
            // A miss wins over any success that day; repeated practice earns no extra credit.
            if (state.LastResetDay >= today || state.LastCorrectDay >= today) return false;
            var days = state.CorrectDays + 1;
            if (days == 7)
            {
                saved.RehearsingIds.Remove(wordId);
                saved.KnownIds.Add(wordId);
                saved.Rehearsal.Remove(wordId);
                moved = true;
            }
            else
                saved.Rehearsal[wordId] = state with { CorrectDays = days, LastCorrectDay = today };
            return true;
        });
        return moved;
    }

    private void SetCategory(T word, WordPracticeCategory category) => SetCategory([word], category);

    public void SetCategory(IEnumerable<T> words, WordPracticeCategory category)
    {
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        var ids = ValidateIds(words);
        if (ids.Count == 0) return;
        progress.Change(dataset, saved =>
        {
            var existing = GetCategoryIds(saved, category).ToHashSet();
            foreach (var id in ids.Except(existing)) saved.Rehearsal.Remove(id);
            saved.KnownIds.RemoveAll(ids.Contains);
            saved.TrainingIds.RemoveAll(ids.Contains);
            saved.RehearsingIds.RemoveAll(ids.Contains);
            var target = category switch
            {
                WordPracticeCategory.Known => saved.KnownIds,
                WordPracticeCategory.Training => saved.TrainingIds,
                _ => saved.RehearsingIds
            };
            target.AddRange(ids.Order());
        });
    }

    public void RemoveFromCategory(IEnumerable<T> words, WordPracticeCategory category)
    {
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        var ids = ValidateIds(words);
        if (ids.Count == 0) return;
        progress.Change(dataset, saved =>
        {
            var target = category switch
            {
                WordPracticeCategory.Known => saved.KnownIds,
                WordPracticeCategory.Training => saved.TrainingIds,
                _ => saved.RehearsingIds
            };
            foreach (var id in target.Where(ids.Contains)) saved.Rehearsal.Remove(id);
            target.RemoveAll(ids.Contains);
        });
    }

    private static List<int> GetCategoryIds(VocabSaveFile saved, WordPracticeCategory category) => category switch
    {
        WordPracticeCategory.Known => saved.KnownIds,
        WordPracticeCategory.Training => saved.TrainingIds,
        _ => saved.RehearsingIds
    };

    private HashSet<int> ValidateIds(IEnumerable<T> words)
    {
        var ids = words.Select(word => word.Id).ToHashSet();
        if (!ids.IsSubsetOf(Words.Select(word => word.Id)))
            throw new ArgumentException("A word is not in this catalog.", nameof(words));
        return ids;
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
