using DataLoaders.Models;

namespace Repositories;

public class SentencesData
{
    public IEnumerable<Sentence> Sentences { get; }
    public SentencesData()
    {
        // var loader = new DataLoaders.SentenceLoader("jp_kana_reading_dataset_v3.jsonl");
        var loader = new DataLoaders.SentenceLoader("Real sentences.json");
        
        Sentences = loader.Load();
    }
}