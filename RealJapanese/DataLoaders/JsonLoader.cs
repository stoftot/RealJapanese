using System.Text.Json;

namespace DataLoaders;

public class JsonLoader<T>(string folderPath, string fileName)
{
    private string FileName { get; } = fileName;
    private string FolderPath { get; } = folderPath;
    private string FilePath => $"{FolderPath}/{FileName}";
    
    public IEnumerable<T> Load()
    {
        var fileType = FileName.Split('.').Last();

        return fileType switch
        {
            "jsonl" => LoadJsonl(),
            "json" => LoadJson(),
            _ => throw new NotImplementedException()
        };
    }

    private IEnumerable<T> LoadJsonl()
    {
        return from line
                    in File.ReadLines(FilePath)
                where !string.IsNullOrWhiteSpace(line)
                select JsonSerializer.Deserialize<T>(line);
    }

    private IEnumerable<T> LoadJson()
    {
        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<T>>(json) ?? [];
    }
}