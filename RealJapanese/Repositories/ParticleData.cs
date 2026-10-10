using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class ParticleData : WordDataBase<Particle>
{
    public ParticleData() : this(RepositoryPaths.Default) { }

    public ParticleData(RepositoryPaths paths) : base(paths, "Particles", "Particles.json") { }
}
