using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace DataLoaders;

public abstract class Loader<T>
{
    protected abstract string FileName { get; }

    protected List<T> Elements { get; private set; }

    private string FolderPath { get; }
    private string FilePath => $"{FolderPath}/{FileName}";

    private CsvReader CsvReader { get; }

    protected Loader(string folderPath, ClassMap<T> classMap)
    {
        FolderPath = folderPath;
        
        using var reader = new StreamReader(FilePath);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "|",
            PrepareHeaderForMatch = args => args.Header.ToLower(),
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
        };

        CsvReader = new CsvReader(reader, config);

        Elements = Load(classMap);
    }

    private List<T> Load(ClassMap<T> classMap)
    {
        CsvReader.Context.RegisterClassMap(classMap);
        return CsvReader.GetRecords<T>().ToList();
    }
}