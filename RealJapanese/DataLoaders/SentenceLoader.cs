using System.Text.Json;
using DataLoaders.Models;

namespace DataLoaders;

public class SentenceLoader(string filename) : JsonLoader<Sentence>("../Data/Sentences", filename)
{
    // protected string FolderPath => "../Data/Sentences";
    // protected string FileName { get; } = filename;
}