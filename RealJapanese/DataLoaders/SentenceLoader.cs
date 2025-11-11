using System.Text.Json;
using DataLoaders.Models;

namespace DataLoaders;

public class SentenceLoader(string filename)
{
    private static string FolderPath => "../Data/Sentences";
    private string FileName { get; } = filename;
    private string FilePath => $"{FolderPath}/{FileName}";


    public List<Sentence> Load()
    {
        var fileType = FileName.Split('.').Last();

        return fileType switch
        {
            "jsonl" => LoadJsonl(),
            "json" => LoadJson(),
            _ => throw new NotImplementedException()
        };
    }

    private List<Sentence> LoadJsonl()
    {
        return (from line
                    in File.ReadLines(FilePath)
                where !string.IsNullOrWhiteSpace(line)
                select JsonSerializer.Deserialize<Sentence>(line))
            .ToList();
    }

    private List<Sentence> LoadJson()
    {
        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<Sentence>>(json) ?? [];
    }
}