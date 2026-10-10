using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class AdverbData : WordDataBase<Adverb>
{
    public AdverbData() : this(RepositoryPaths.Default) { }

    public AdverbData(RepositoryPaths paths) : base(paths, "Adverbs", "Adverbs.json") { }
}
