using System.Text.Encodings.Web;
using System.Text.Json;

namespace DataLoaders;

public class JsonSaver<T>(string folderPath, string fileName)
{
    private string FileName { get; } = fileName;
    private string FolderPath { get; } = folderPath;
    private string FilePath => $"{FolderPath}/{FileName}";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping  // 👈 allows Japanese, Chinese, emojis, etc.
    };
    
    public void Save(IEnumerable<T> data)
    {
        var fileType = FileName.Split('.').Last();

        switch (fileType)
        {
            case "jsonl":
                SaveJsonl(data);
                break;
            case "json":
                SaveJson(data);
                break;
            default:
                throw new NotImplementedException();
        }
    }
    
    public void Save(T data)
    {
        var fileType = FileName.Split('.').Last();

        switch (fileType)
        {
            case "json":
                SaveJson(data);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    private void SaveJson(T data)
    {
        Directory.CreateDirectory(FolderPath);

        var json = JsonSerializer.Serialize(data, Options);

        File.WriteAllText(FilePath, json);
    }

    private void SaveJsonl(IEnumerable<T> data)
    {
        Directory.CreateDirectory(FolderPath);

        using var writer = new StreamWriter(FilePath, false);
        foreach (var item in data)
        {
            var line = JsonSerializer.Serialize(item, Options);
            writer.WriteLine(line);
        }
    }

    private void SaveJson(IEnumerable<T> data)
    {
        Directory.CreateDirectory(FolderPath);

        var json = JsonSerializer.Serialize(data.ToList(), Options);

        File.WriteAllText(FilePath, json);
    }

}