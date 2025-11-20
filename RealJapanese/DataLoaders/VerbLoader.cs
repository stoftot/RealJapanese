using DataLoaders.Models;

namespace DataLoaders;

public class VerbLoader(string filename) : JsonLoader<Verb>
{
    protected override string FolderPath => "../Data/Verbs";
    protected override string FileName { get; } = filename;
}