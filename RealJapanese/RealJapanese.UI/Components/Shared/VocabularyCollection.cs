using DataLoaders.Models;
using Repositories;
using Repositories.Bases;

namespace RealJapanese.Components.Shared;

/// <summary>A presentation view over separate catalogs; projected IDs are never persisted.</summary>
public sealed class VocabularyCollection
{
    public IReadOnlyList<VocabularySource> Sources { get; }
    public IEnumerable<VocabularySource> MixerSources => Sources.Where(source => source.Key != "Words");
    public List<VocabularyEntry> Entries { get; }
    public IEnumerable<string> SupportingSources => Sources.Where(source => source.Key is not ("Nouns" or "Verbs" or "Adjectives"))
        .Select(source => source.Key);

    public VocabularyCollection(RepositoryPaths paths)
    {
        Sources = [
            Source("Words", "General words", new WordData(paths)),
            Source("Nouns", "Nouns", new NounData(paths)),
            Source("Verbs", "Verbs", new VerbData(paths)),
            Source("Adjectives", "Adjectives", new AdjectiveData(paths)),
            Source("Adverbs", "Adverbs", new AdverbData(paths)),
            Source("Expressions", "Expressions", new ExpressionData(paths)),
            Source("Pronouns", "Pronouns", new PronounData(paths)),
            Source("Determiners", "Determiners", new DeterminerData(paths)),
            Source("Counters", "Counters", new CounterData(paths)),
            Source("Conjunctions", "Conjunctions", new ConjunctionData(paths)),
            Source("Particles", "Particles", new ParticleData(paths)),
            Source("Suffixes", "Suffixes", new SuffixData(paths))
        ];
        Entries = Sources.SelectMany(source => source.Words.Select(word => (source, word)))
            .Select((entry, index) => new VocabularyEntry
            {
                Id = index, Dataset = entry.source.Key, SourceLabel = entry.source.Label, SourceId = entry.word.Id,
                Japanese = entry.word.Japanese, Kana = entry.word.Kana, English = entry.word.English,
                Category = entry.word.Category, Origin = entry.word.Origin, AdditionalOrigins = entry.word.AdditionalOrigins
            }).ToList();
    }

    public HashSet<string> ParseMixerSources(string? query) => query is null
        ? MixerSources.Select(source => source.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : MixerSources.Where(source => query.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(source.Key, StringComparer.OrdinalIgnoreCase)).Select(source => source.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public string SerializeMixerSources(IEnumerable<string> keys)
    {
        var selected = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var value = string.Join(',', MixerSources.Where(source => selected.Contains(source.Key)).Select(source => source.Key));
        return value.Length == 0 ? "none" : value;
    }

    public List<VocabularyEntry> SavedWords(IEnumerable<string> keys, WordPracticeCategory category)
    {
        var selected = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var saved = Sources.Where(source => selected.Contains(source.Key))
            .SelectMany(source => source.SavedWords(category).Select(word => (source.Key, word.Id))).ToHashSet();
        return Entries.Where(entry => saved.Contains((entry.Dataset, entry.SourceId))).ToList();
    }

    public void SetCategory(List<VocabularyEntry> entries, WordPracticeCategory category, bool selected)
    {
        if (entries.Any(entry => !Entries.Contains(entry))) throw new ArgumentException("Unknown vocabulary entry.", nameof(entries));
        foreach (var group in entries.DistinctBy(entry => (entry.Dataset, entry.SourceId)).GroupBy(entry => entry.Dataset))
            Sources.Single(source => source.Key == group.Key).Update(group.Select(entry => entry.SourceId).ToHashSet(), category, selected);
    }

    public bool RecordPracticeAnswer(Word word, bool correct)
    {
        if (word is not VocabularyEntry entry || !Entries.Contains(entry))
            throw new ArgumentException("Unknown vocabulary entry.", nameof(word));
        return Sources.Single(source => source.Key == entry.Dataset).RecordAnswer(entry.SourceId, correct);
    }

    private static VocabularySource Source<T>(string key, string label, WordDataBase<T> repository) where T : Word =>
        new(key, label, repository.Words.Cast<Word>().ToList(), category => repository.GetWords(category).Cast<Word>().ToList(),
            (id, correct) => repository.RecordPracticeAnswer(id, correct),
            (ids, category, selected) =>
            {
                var words = repository.Words.Where(word => ids.Contains(word.Id)).ToList();
                if (selected) repository.SetCategory(words, category);
                else repository.RemoveFromCategory(words, category);
            });
}

public sealed record VocabularySource(string Key, string Label, IReadOnlyList<Word> Words,
    Func<WordPracticeCategory, List<Word>> SavedWords, Func<int, bool, bool> RecordAnswer,
    Action<HashSet<int>, WordPracticeCategory, bool> Update);

public sealed record VocabularyEntry : Word
{
    public required string Dataset { get; init; }
    public required string SourceLabel { get; init; }
    public int SourceId { get; init; }
}
