namespace DataLoaders.Models;

public class KanjiRelationData
{
    public int KanjiId { get; set; }
    public List<int> VerbIds { get; set; }
    public List<int> AdjectiveIds { get; set; }
    public List<int> WordIds { get; set; }
}