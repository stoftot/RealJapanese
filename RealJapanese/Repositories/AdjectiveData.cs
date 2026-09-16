using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class AdjectiveData : WordDataBase<Adjective>
{
    private const string DataFileName = "Adjectives.json";

    public AdjectiveData()
        : this(RepositoryPaths.Default)
    {
    }

    public AdjectiveData(RepositoryPaths paths)
        : base(paths.CatalogFolder("Adjectives"), DataFileName, paths.ProgressFolder("Adjectives"))
    {
    }
}
