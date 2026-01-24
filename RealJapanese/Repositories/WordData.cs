using DataLoaders;
using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class WordData() : WordDataBase<Word>(FolderPath, DataFileName)
{
    private const string FolderPath = "../Data/Words";
    private const string DataFileName = "Words.json";
}