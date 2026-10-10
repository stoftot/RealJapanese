using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class SuffixData : WordDataBase<Suffix>
{
    public SuffixData() : this(RepositoryPaths.Default) { }

    public SuffixData(RepositoryPaths paths) : base(paths, "Suffixes", "Suffixes.json") { }
}
