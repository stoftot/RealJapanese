using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class NounData : WordDataBase<Word>
{
    public NounData() : this(RepositoryPaths.Default) { }

    public NounData(RepositoryPaths paths) : base(paths, "Nouns", "Nouns.json") { }
}
