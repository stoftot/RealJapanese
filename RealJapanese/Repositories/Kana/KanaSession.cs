using System.Diagnostics;

namespace Repositories.Kana;

/// <summary>A finite pass through kana; mistakes and reveals cannot earn a correct first try.</summary>
public sealed class KanaSession
{
    private readonly KanaCharacter[] cards;
    private readonly Stopwatch timer = Stopwatch.StartNew();
    public KanaSession(IEnumerable<KanaCharacter> selection, bool randomOrder)
    {
        cards = selection.DistinctBy(c => c.Id).ToArray();
        if (randomOrder) Random.Shared.Shuffle(cards);
    }

    public int Total => cards.Length;
    public int Completed { get; private set; }
    public int Correct { get; private set; }
    public bool Finished => Completed >= Total;
    public KanaCharacter? Current => Finished ? null : cards[Completed];
    public bool Missed { get; private set; }
    public bool Revealed { get; private set; }
    public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Correct / Total);
    public double Seconds => timer.Elapsed.TotalSeconds;

    public bool Answer(string input)
    {
        if (Current is null) return false;
        if (!Current.Accepts(input)) { Missed = true; return false; }
        if (!Missed) Correct++;
        Advance();
        return true;
    }

    public void Reveal()
    {
        if (Finished) return;
        Missed = true;
        Revealed = true;
    }

    public void Skip()
    {
        if (!Finished) Advance();
    }

    private void Advance()
    {
        Completed++;
        Missed = false;
        Revealed = false;
        if (Finished) timer.Stop();
    }
}
