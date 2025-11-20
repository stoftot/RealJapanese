using System.Text.Json;

namespace DataLoaders;

public abstract class JsonLoader<T>
{
    protected abstract string FileName { get; }
    protected abstract string FolderPath { get; }
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