using System.Text.Json;
using DataLoaders.Models;

namespace DataLoaders;

public class ParticalsLoader(string filename)
{
    private static string FolderPath => "../Data/Gramar";
    private string FileName { get; } = filename;
    private string FilePath => $"{FolderPath}/{FileName}";


    public List<ParticalsModel> Load()
    {
        var fileType = FileName.Split('.').Last();

        return fileType switch
        {
            "jsonl" => LoadJsonl(),
            "json" => LoadJson(),
            _ => throw new NotImplementedException()
        };
    }

    private List<ParticalsModel> LoadJsonl()
    {
        return (from line
                    in File.ReadLines(FilePath)
                where !string.IsNullOrWhiteSpace(line)
                select JsonSerializer.Deserialize<ParticalsModel>(line))
            .ToList();
    }

    private List<ParticalsModel> LoadJson()
    {
        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<ParticalsModel>>(json) ?? [];
    }
}