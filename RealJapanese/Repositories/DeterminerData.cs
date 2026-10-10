using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class DeterminerData : WordDataBase<Determiner>
{
    public DeterminerData() : this(RepositoryPaths.Default) { }

    public DeterminerData(RepositoryPaths paths) : base(paths, "Determiners", "Determiners.json") { }
}
