using DataLoaders;
using DataLoaders.Models;

namespace Repositories;

public class WordData
{
    private const string FolderPath = "../Data/Words";
    private const string DataFileName = "Words.json";
    private const string SaveFileName = "SavedData.json";
    
    private static JsonLoader<Word> DataFileLoader = new (folderPath: FolderPath, fileName: DataFileName);
    private static JsonLoader<VocabSaveFile> SaveFileLoader = new (folderPath: FolderPath, fileName: SaveFileName);
    private static JsonSaver<Word> DataFileSaver = new (folderPath: FolderPath, fileName: DataFileName);
    private static JsonSaver<VocabSaveFile> SaveFileSaver = new (folderPath: FolderPath, fileName: SaveFileName);
    
    private static readonly IEnumerable<Word> wordData = DataFileLoader.Load();

    public IEnumerable<Word> Words { get; set; } = new List<Word>(wordData);
    public VocabSaveFile VocabSaveFile { get; set; } = SaveFileLoader.Load().FirstOrDefault() ?? new VocabSaveFile();
    
    public  WordData()
    {
        UpdateIDs();
    }
    
    
    public void SaveProgress()
    {
        SaveFileSaver.Save(VocabSaveFile);
    }
    
    public IEnumerable<Word> VocabWords =>
        Words.Where(w => VocabSaveFile.KnownIds.Contains(w.Id));
    
    public IEnumerable<Word> TrainingWords =>
        Words.Where(w => VocabSaveFile.TrainingIds.Contains(w.Id));
    
    public IEnumerable<int> VocabWordIds =>
        VocabSaveFile.KnownIds;
    
    public IEnumerable<int> TrainingWordIds =>
        VocabSaveFile.TrainingIds;
    
    public void AddToVocab(Word word)
    {
        VocabSaveFile.KnownIds.Add(word.Id);
        SaveProgress();
    }
    public void RemoveFromVocab(Word word)
    {
        VocabSaveFile.KnownIds.Remove(word.Id);
        SaveProgress();
    }
    
    public void AddToTraining(Word word)
    {
        VocabSaveFile.TrainingIds.Add(word.Id);
        SaveProgress();
    }
    
    public void RemoveFromTraining(Word word)
    {
        VocabSaveFile.TrainingIds.Remove(word.Id);
        SaveProgress();
    }
    
    private void UpdateIDs()
    {
        var words = DataFileLoader.Load().ToList();
        
        var maxExistingId = words
            .Select(w => w.Id)
            .Max();

        var nextId = maxExistingId + 1;     // 0 on first run
        
        foreach (var word in words.Where(w => w.Id == -1))
        {
            word.Id = nextId;
            nextId++;
        }

        DataFileSaver.Save(words);
    }
}