using DataLoaders;
using DataLoaders.Models;

namespace Repositories.Bases;

public abstract class WordDataBase<T> where T : Word
{
    private const string SaveFileName = "SavedData.json";
    
    protected readonly JsonLoader<T> DataFileLoader;
    protected readonly JsonLoader<VocabSaveFile> SaveFileLoader;
    protected readonly JsonSaver<VocabSaveFile> SaveFileSaver;

    public IEnumerable<T> Words { get; set; }
    protected VocabSaveFile VocabSaveFile { get; set; }

    protected WordDataBase(string folderPath, string dataFileName)
        : this(folderPath, dataFileName, folderPath)
    {
    }

    protected WordDataBase(string dataFolderPath, string dataFileName, string progressFolderPath)
    {
        DataFileLoader = new JsonLoader<T>(folderPath: dataFolderPath, fileName: dataFileName);
        SaveFileLoader = new JsonLoader<VocabSaveFile>(folderPath: progressFolderPath, fileName: SaveFileName);
        SaveFileSaver = new JsonSaver<VocabSaveFile>(folderPath: progressFolderPath, fileName: SaveFileName);

        var words = DataFileLoader.Load().ToList();
        UpdateIDs(words);
        Words = words;

        var saveFilePath = Path.Combine(progressFolderPath, SaveFileName);
        VocabSaveFile = File.Exists(saveFilePath)
            ? SaveFileLoader.Load().FirstOrDefault() ?? new VocabSaveFile()
            : new VocabSaveFile();
        RemoveStaleProgressIds();
    }


    public void SaveProgress()
    {
        SaveFileSaver.Save(VocabSaveFile);
    }

    public List<T> VocabWords => ResolveWords(VocabSaveFile.KnownIds);

    public List<T> TrainingWords => ResolveWords(VocabSaveFile.TrainingIds);

    public List<T> RehearsingWords => ResolveWords(VocabSaveFile.RehearsingIds);

    public IEnumerable<int> VocabWordIds =>
        VocabSaveFile.KnownIds;

    public IEnumerable<int> TrainingWordIds =>
        VocabSaveFile.TrainingIds;

    public IEnumerable<int> RehearsingWordIds =>
        VocabSaveFile.RehearsingIds;

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

    public void AddToRehearsing(T word)
    {
        VocabSaveFile.RehearsingIds.Add(word.Id);
        SaveProgress();
    }

    public void RemoveFromRehearsing(T word)
    {
        VocabSaveFile.RehearsingIds.Remove(word.Id);
        SaveProgress();
    }

    public List<T> GetWords(WordPracticeCategory category) =>
        category switch
        {
            WordPracticeCategory.Training => TrainingWords,
            WordPracticeCategory.Rehearsing => RehearsingWords,
            _ => VocabWords
        };

    private static void UpdateIDs(IList<T> words)
    {
        var maxExistingId = words
            .Where(w => w.Id >= 0)
            .Select(w => w.Id)
            .DefaultIfEmpty(-1)
            .Max();

        var nextId = maxExistingId + 1; // 0 on first run

        foreach (var word in words.Where(w => w.Id == -1))
        {
            word.Id = nextId;
            nextId++;
        }
    }

    private List<T> ResolveWords(IEnumerable<int> ids)
    {
        var wordsById = Words
            .GroupBy(word => word.Id)
            .ToDictionary(group => group.Key, group => group.First());

        return ids
            .Where(wordsById.ContainsKey)
            .Select(id => wordsById[id])
            .ToList();
    }

    private void RemoveStaleProgressIds()
    {
        var validIds = Words.Select(word => word.Id).ToHashSet();

        VocabSaveFile.KnownIds = VocabSaveFile.KnownIds?.Where(validIds.Contains).ToList() ?? [];
        VocabSaveFile.TrainingIds = VocabSaveFile.TrainingIds?.Where(validIds.Contains).ToList() ?? [];
        VocabSaveFile.RehearsingIds = VocabSaveFile.RehearsingIds?.Where(validIds.Contains).ToList() ?? [];
    }
}
