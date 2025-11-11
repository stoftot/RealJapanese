using DataLoaders.Models;

namespace Repositories;

public class TrainingNonsensSentences
{
    public List<Sentence> Sentences { get; }
    public TrainingNonsensSentences()
    {
        var loader = new DataLoaders.SentenceLoader("jp_kana_reading_dataset_v3.jsonl");
        Sentences = loader.Load();
    }
}