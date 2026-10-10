using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class CounterData : WordDataBase<Counter>
{
    public CounterData() : this(RepositoryPaths.Default) { }

    public CounterData(RepositoryPaths paths) : base(paths, "Counters", "Counters.json") { }
}
