using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class ConjunctionData : WordDataBase<Conjunction>
{
    public ConjunctionData() : this(RepositoryPaths.Default) { }

    public ConjunctionData(RepositoryPaths paths) : base(paths, "Conjunctions", "Conjunctions.json") { }
}
