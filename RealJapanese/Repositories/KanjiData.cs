using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class KanjiData
{
    private const string BaseFolderPath = "../Data/Kanji/";
    
    public class SingelData() : WordDataBase<Word>(FolderPath, DataFileName)
    {
        private const string FolderPath = BaseFolderPath + "Singel/";
        private const string DataFileName = "Singel.json";
    }
    
    public class CombinedData() : WordDataBase<Word>(FolderPath, DataFileName)
    {
        private const string FolderPath = BaseFolderPath + "Combined/";
        private const string DataFileName = "Combined.json";
    }
    
    public SingelData Singel { get; } = new();
    public CombinedData Combined { get; } = new();
}