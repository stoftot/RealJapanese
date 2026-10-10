namespace DataLoaders.Models;

public class KanjiRelationData
{
    public int KanjiId { get; set; }
    public List<int> VerbIds { get; set; }
    public List<int> AdjectiveIds { get; set; }
    public List<int> WordIds { get; set; }
    public List<int> NounIds { get; set; } = [];
    public List<int> AdverbIds { get; set; } = [];
    public List<int> ExpressionIds { get; set; } = [];
    public List<int> PronounIds { get; set; } = [];
    public List<int> DeterminerIds { get; set; } = [];
    public List<int> CounterIds { get; set; } = [];
    public List<int> ConjunctionIds { get; set; } = [];
    public List<int> ParticleIds { get; set; } = [];
    public List<int> SuffixIds { get; set; } = [];
}
