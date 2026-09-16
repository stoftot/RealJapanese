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
            : base(
                paths.CatalogFolder("Kanji", "Singel"),
                DataFileName,
                paths.ProgressFolder("Kanji", "Singel"))
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
            : base(
                paths.CatalogFolder("Kanji", "Combined"),
                DataFileName,
                paths.ProgressFolder("Kanji", "Combined"))
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
