using static DataLoaders.Models.Conjugatabel;

namespace DataLoaders.Models;

/// <summary>A noun with polite, short and connecting forms; the noun itself stays unchanged.</summary>
public record Noun : Word
{
    public string Conjugate(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        var noun = Text(toConjugate);
        var ending = conjugationType switch
        {
            ConjugationType.PresentAffirmative => "です",
            ConjugationType.PresentNegative => "じゃないです",
            ConjugationType.PastAffirmative => "でした",
            ConjugationType.PastNegative => "じゃなかったです",
            _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
        };
        return noun + ending;
    }

    public string ShortForm(ToConjugate toConjugate, ConjugationType conjugationType)
    {
        var noun = Text(toConjugate);
        var ending = conjugationType switch
        {
            ConjugationType.PresentAffirmative => "だ",
            ConjugationType.PresentNegative => "じゃない",
            ConjugationType.PastAffirmative => "だった",
            ConjugationType.PastNegative => "じゃなかった",
            _ => throw new ArgumentOutOfRangeException(nameof(conjugationType), conjugationType, null)
        };
        return noun + ending;
    }

    public string TeForm(ToConjugate toConjugate) => Text(toConjugate) + "で";

    private string Text(ToConjugate toConjugate) => toConjugate switch
    {
        ToConjugate.Japanese => Japanese,
        ToConjugate.Kana => Kana,
        _ => throw new ArgumentOutOfRangeException(nameof(toConjugate), toConjugate, null)
    };
}
