using System.Text.Json;
using DataLoaders.Models;

namespace DataLoaders;

public class ParticalsLoader(string filename) : JsonLoader<ParticalsModel>("../Data/Gramar", filename)
{
    // protected string FolderPath => "../Data/Gramar";
    // protected string FileName { get; } = filename;
}