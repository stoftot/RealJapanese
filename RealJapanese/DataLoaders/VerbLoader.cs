using DataLoaders.Models;

namespace DataLoaders;

public class VerbLoader(string filename) : JsonLoader<Verb>("../Data/Verbs", filename)
{
    // protected string FolderPath => "../Data/Verbs";
    // protected string FileName { get; } = filename;
}