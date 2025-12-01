using System.Text.Json;

namespace DataLoaders;

public class JsonLoader<T>(string folderPath, string fileName)
{
    private string FileName { get; } = fileName;
    private string FolderPath { get; } = folderPath;
    private string FilePath => Path.Combine(FolderPath, FileName);

    public IEnumerable<T> Load()
    {
        var fileType = FileName.Split('.').Last();

        return fileType switch
        {
            "jsonl" => LoadJsonl(),
            "json"  => LoadJson(),
            _       => throw new NotImplementedException()
        };
    }

    private IEnumerable<T> LoadJsonl()
    {
        return File.ReadLines(FilePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<T>(line))
            .Where(x => x is not null)
            .Cast<T>();
    }

    private IEnumerable<T> LoadJson()
    {
        var json = File.ReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(json))
            return Enumerable.Empty<T>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Case 1: root is an array → deserialize as List<T>
        if (root.ValueKind == JsonValueKind.Array)
        {
            var list = JsonSerializer.Deserialize<List<T>>(json);
            return list ?? Enumerable.Empty<T>();
        }

        // Case 2: root is a single value / object → deserialize as T and wrap in a sequence
        var item = JsonSerializer.Deserialize<T>(json);
        return item is null
            ? Enumerable.Empty<T>()
            : new[] { item };
    }
}