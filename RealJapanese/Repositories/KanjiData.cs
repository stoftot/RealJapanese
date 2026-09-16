using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class KanjiData
{
    public class SingleData : WordDataBase<Word>
    {
        private const string DataFileName = "Singel.json";

        public SingleData()
            : this(RepositoryPaths.Default)
        {
        }

        public SingleData(RepositoryPaths paths)
            : base(paths, "Kanji/Singel", DataFileName)
        {
        }
    }
    
    public class CombinedData : WordDataBase<Word>
    {
        private const string DataFileName = "Combined.json";

        public CombinedData()
            : this(RepositoryPaths.Default)
        {
        }

        public CombinedData(RepositoryPaths paths)
            : base(paths, "Kanji/Combined", DataFileName)
        {
        }
    }
    
    public SingleData Single { get; }
    public CombinedData Combined { get; }

    public KanjiData()
        : this(RepositoryPaths.Default)
    {
    }

    public KanjiData(RepositoryPaths paths)
    {
        Single = new SingleData(paths);
        Combined = new CombinedData(paths);
    }
}
