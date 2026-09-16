using DataLoaders;
using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class WordData : WordDataBase<Word>
{
    private const string DataFileName = "Words.json";

    public WordData()
        : this(RepositoryPaths.Default)
    {
    }

    public WordData(RepositoryPaths paths)
        : base(paths, "Words", DataFileName)
    {
    }
}
