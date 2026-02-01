using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class AdjectiveData() : WordDataBase<Adjective>(FolderPath, DataFileName)
{
    private const string FolderPath = "../Data/Adjectives";
    private const string DataFileName = "Adjectives.json";
}