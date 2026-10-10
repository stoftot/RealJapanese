using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class PronounData : WordDataBase<Pronoun>
{
    public PronounData() : this(RepositoryPaths.Default) { }

    public PronounData(RepositoryPaths paths) : base(paths, "Pronouns", "Pronouns.json") { }
}
