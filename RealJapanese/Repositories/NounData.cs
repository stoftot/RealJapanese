using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class NounData : WordDataBase<Noun>
{
    public NounData() : this(RepositoryPaths.Default) { }

    public NounData(RepositoryPaths paths) : base(paths, "Nouns", "Nouns.json") { }
}
