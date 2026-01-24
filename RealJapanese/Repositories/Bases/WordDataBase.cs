using DataLoaders;
using DataLoaders.Models;

namespace Repositories.Bases;

public abstract class WordDataBase<T> where T : Word
{
    private const string SaveFileName = "SavedData.json";
    
    protected readonly JsonLoader<T> DataFileLoader;
    protected readonly JsonLoader<VocabSaveFile> SaveFileLoader;
    protected readonly JsonSaver<T> DataFileSaver;
    protected readonly JsonSaver<VocabSaveFile> SaveFileSaver;

    protected readonly IEnumerable<T> wordData;

    public IEnumerable<T> Words { get; set; }
    protected VocabSaveFile VocabSaveFile { get; set; }

    protected WordDataBase(string folderPath, string dataFileName)
    {
        DataFileLoader = new JsonLoader<T>(folderPath: folderPath, fileName: dataFileName);
        SaveFileLoader = new JsonLoader<VocabSaveFile>(folderPath: folderPath, fileName: SaveFileName);
        DataFileSaver = new JsonSaver<T>(folderPath: folderPath, fileName: dataFileName);
        SaveFileSaver = new JsonSaver<VocabSaveFile>(folderPath: folderPath, fileName: SaveFileName);

        wordData = DataFileLoader.Load();
        Words = new List<T>(wordData);
        VocabSaveFile = SaveFileLoader.Load().FirstOrDefault() ?? new VocabSaveFile();
        UpdateIDs();
    }


    public void SaveProgress()
    {
        SaveFileSaver.Save(VocabSaveFile);
    }

    public IEnumerable<T> VocabWords =>
        Words.Where(w => VocabSaveFile.KnownIds.Contains(w.Id));

    public IEnumerable<T> TrainingWords =>
        Words.Where(w => VocabSaveFile.TrainingIds.Contains(w.Id));

    public IEnumerable<int> VocabWordIds =>
        VocabSaveFile.KnownIds;

    public IEnumerable<int> TrainingWordIds =>
        VocabSaveFile.TrainingIds;

    public void AddToVocab(T word)
    {
        VocabSaveFile.KnownIds.Add(word.Id);
        SaveProgress();
    }

    public void RemoveFromVocab(T word)
    {
        VocabSaveFile.KnownIds.Remove(word.Id);
        SaveProgress();
    }

    public void AddToTraining(T word)
    {
        VocabSaveFile.TrainingIds.Add(word.Id);
        SaveProgress();
    }

    public void RemoveFromTraining(T word)
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

        var nextId = maxExistingId + 1; // 0 on first run

        foreach (var word in words.Where(w => w.Id == -1))
        {
            word.Id = nextId;
            nextId++;
        }

        DataFileSaver.Save(words);
    }
}