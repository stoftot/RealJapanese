// See https://aka.ms/new-console-template for more information

using DataLoaders;
using DataLoaders.Models;

const string basePath = "../../../../Data/";
const string SaveFileName = "SavedData.json";

var wordData = new JsonLoader<Word>(folderPath: basePath+"Words", fileName: "Words.json") 
    .Load();
var wordDataSaver = new JsonSaver<Word>(folderPath: basePath+"Words", fileName: "Words.json");

var wordSaveData = new JsonLoader<VocabSaveFile>(folderPath: basePath+"Words", fileName: SaveFileName) 
    .Load()
    .First();
var wordSaveDataSaver = new JsonSaver<VocabSaveFile>(folderPath: basePath+"Words", fileName: SaveFileName);

var newWordData = new List<Word>();
foreach (var word in wordData)
{
    if (newWordData.Any(w =>
            w.Japanese == word.Japanese &&
            w.English == word.English &&
            w.Kana == word.Kana))
        continue;
    
    newWordData.Add(new Word
    {
        Id = word.Id,
        Category =  word.Category,
        English =  word.English,
        Kana = word.Kana,
        Japanese = word.Japanese
    });
}

var id = 0;
foreach (var newWord in newWordData)
{
    newWord.Id = id++;
}

var newKnownIds = new List<int>();
foreach (var ids in wordSaveData.KnownIds)
{
    var word = wordData.First(w => w.Id == ids);
    var newid = newWordData.First(w =>
        w.Japanese == word.Japanese &&
        w.English == word.English &&
        w.Kana == word.Kana).Id;
    if (!newKnownIds.Contains(newid))
        newKnownIds.Add(newid);
}

var newRehearsingIds = new List<int>();
foreach (var ids in wordSaveData.RehearsingIds)
{
    var word = wordData.First(w => w.Id == ids);
    var newid = newWordData.First(w =>
        w.Japanese == word.Japanese &&
        w.English == word.English &&
        w.Kana == word.Kana).Id;
    
    if (!newKnownIds.Contains(newid) && !newRehearsingIds.Contains(newid))
        newRehearsingIds.Add(newid);
}

var newTrainingIds = new List<int>();
foreach (var ids in wordSaveData.TrainingIds)
{
    var word = wordData.First(w => w.Id == ids);
    var newid = newWordData.First(w =>
        w.Japanese == word.Japanese &&
        w.English == word.English &&
        w.Kana == word.Kana).Id;
    
    if (!newKnownIds.Contains(newid) && !newRehearsingIds.Contains(newid) && !newTrainingIds.Contains(newid))
        newTrainingIds.Add(newid);
}

wordDataSaver.Save(newWordData);
wordSaveDataSaver.Save(new VocabSaveFile
{
    KnownIds =  newKnownIds,
    RehearsingIds = newRehearsingIds,
    TrainingIds = newTrainingIds
});






return 0;