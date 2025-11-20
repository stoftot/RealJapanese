using System.Text.Json;
using DataLoaders.Models;

namespace DataLoaders;

public class SentenceLoader(string filename) : JsonLoader<Sentence>
{
    protected override string FolderPath => "../Data/Sentences";
    protected override string FileName { get; } = filename;
}