using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class KanjiData
{
    private const string BaseFolderPath = "../Data/Kanji/";
    
    public class SingleData() : WordDataBase<Word>(FolderPath, DataFileName)
    {
        private const string FolderPath = BaseFolderPath + "Singel/";
        private const string DataFileName = "Singel.json";
    }
    
    public class CombinedData() : WordDataBase<Word>(FolderPath, DataFileName)
    {
        private const string FolderPath = BaseFolderPath + "Combined/";
        private const string DataFileName = "Combined.json";
    }
    
    public SingleData Single { get; } = new();
    public CombinedData Combined { get; } = new();
}