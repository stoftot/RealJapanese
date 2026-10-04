using DataLoaders.Models;

namespace DataLoaders.Models.Genki;

/// <summary>Resolves schema forms through the application's word and conjugation models.</summary>
public static class GenkiForms
{
    private static readonly string[] VerbForms =
    [
        "polite", "politeNegative", "politePast", "politePastNegative",
        "short", "negative", "past", "pastNegative", "te", "stem", "negativeStem",
        "tai", "taiNegative", "taiPast", "taiPastNegative",
        "taiPolite", "taiPoliteNegative", "taiPolitePast", "taiPolitePastNegative"
    ];

    private static readonly string[] AdjectiveForms =
    [
        "polite", "politeNegative", "politePast", "politePastNegative",
        "short", "negative", "past", "pastNegative", "te", "attributive", "adverbial", "adjectiveStem"
    ];

    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(
        new[] { "base", "counterSuffix", "requestEnding" }.Concat(VerbForms).Concat(AdjectiveForms).Distinct(StringComparer.Ordinal).ToArray());

    private static readonly IReadOnlyDictionary<string, (string Kana, string GrammarPoint)> Counters =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["〜年生"] = ("〜ねんせい", "g01-01"),
            ["〜時"] = ("〜じ", "g01-01"),
            ["〜歳"] = ("〜さい", "g01-01"),
            ["〜番"] = ("〜ばん", "g01-01"),
            ["〜時間"] = ("〜じかん", "g04-06"),
            ["〜枚"] = ("〜まい", "g05-06"),
            ["〜人"] = ("〜にん", "g07-06")
        };

    /// <summary>Returns whether this vocabulary record can produce the named form.</summary>
    public static bool Supports(Word word, string form)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentException.ThrowIfNullOrWhiteSpace(form);

        return form == "base" || word switch
        {
            Verb => VerbForms.Contains(form, StringComparer.Ordinal),
            Adjective => AdjectiveForms.Contains(form, StringComparer.Ordinal),
            _ => form == "counterSuffix" && IsSupportedCounter(word) ||
                 form == "requestEnding" && IsSupportedRequestEnding(word)
        };
    }

    /// <summary>Renders a supported Japanese or kana form using the application's conjugators.</summary>
    public static string Render(Word word, string form, bool kana = false)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentException.ThrowIfNullOrWhiteSpace(form);
        if (!Supports(word, form))
            throw new NotSupportedException($"Form '{form}' is not supported for {word.GetType().Name} '{word.Japanese}'.");

        var target = kana ? Conjugatabel.ToConjugate.Kana : Conjugatabel.ToConjugate.Japanese;
        if (form == "base") return kana ? word.Kana : word.Japanese;
        if (form == "counterSuffix")
        {
            var text = kana ? word.Kana : word.Japanese;
            return text[1..];
        }
        if (form == "requestEnding")
        {
            var text = kana ? word.Kana : word.Japanese;
            return text.Replace("（〜を）", "を", StringComparison.Ordinal);
        }

        return word switch
        {
            Verb verb => RenderVerb(verb, form, target),
            Adjective adjective => RenderAdjective(adjective, form, target),
            _ => throw new NotSupportedException($"Form '{form}' is not supported for {word.GetType().Name}.")
        };
    }

    /// <summary>Returns the grammar point that first licenses this form.</summary>
    public static IReadOnlyList<string> RequiredGrammar(string form, Word word)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (!Supports(word, form))
            throw new NotSupportedException($"Form '{form}' is not supported for {word.GetType().Name} '{word.Japanese}'.");

        if (form == "base") return [];
        if (form == "counterSuffix") return [Counters[word.Japanese].GrammarPoint];
        if (form == "requestEnding") return ["g02-09"];

        return word switch
        {
            Verb => form switch
            {
                "polite" or "politeNegative" or "stem" => ["g03-01"],
                "politePast" or "politePastNegative" => ["g04-04"],
                "te" => ["g06-01"],
                "short" or "negative" => ["g08-01"],
                "past" or "pastNegative" => ["g09-01"],
                "negativeStem" => ["g12-05"],
                "tai" or "taiNegative" or "taiPast" or "taiPastNegative" or
                    "taiPolite" or "taiPoliteNegative" or "taiPolitePast" or "taiPolitePastNegative" => ["g11-01"],
                _ => []
            },
            Adjective => form switch
            {
                "polite" or "politeNegative" => ["g05-01"],
                "politePast" or "politePastNegative" => ["g05-02"],
                "short" or "negative" => ["g08-01"],
                "past" or "pastNegative" => ["g09-01"],
                "te" or "adverbial" => ["g07-04"],
                "attributive" => ["g05-03"],
                "adjectiveStem" => ["g12-02"],
                _ => []
            },
            _ => []
        };
    }

    private static string RenderVerb(Verb verb, string form, Conjugatabel.ToConjugate target) => form switch
    {
        "polite" => verb.Conjugate(target, Conjugatabel.ConjugationType.PresentAffirmative),
        "politeNegative" => verb.Conjugate(target, Conjugatabel.ConjugationType.PresentNegative),
        "politePast" => verb.Conjugate(target, Conjugatabel.ConjugationType.PastAffirmative),
        "politePastNegative" => verb.Conjugate(target, Conjugatabel.ConjugationType.PastNegative),
        "short" => verb.ShortForm(target, Conjugatabel.ConjugationType.PresentAffirmative),
        "negative" => verb.ShortForm(target, Conjugatabel.ConjugationType.PresentNegative),
        "past" => verb.ShortForm(target, Conjugatabel.ConjugationType.PastAffirmative),
        "pastNegative" => verb.ShortForm(target, Conjugatabel.ConjugationType.PastNegative),
        "te" => verb.Form(target, Verb.VerbForm.TE),
        "stem" => verb.MasuStem(target),
        "negativeStem" => verb.NegativeStem(target),
        "tai" => verb.DesireForm(target, Conjugatabel.ConjugationType.PresentAffirmative),
        "taiNegative" => verb.DesireForm(target, Conjugatabel.ConjugationType.PresentNegative),
        "taiPast" => verb.DesireForm(target, Conjugatabel.ConjugationType.PastAffirmative),
        "taiPastNegative" => verb.DesireForm(target, Conjugatabel.ConjugationType.PastNegative),
        "taiPolite" => verb.DesireForm(target, Conjugatabel.ConjugationType.PresentAffirmative, polite: true),
        "taiPoliteNegative" => verb.DesireForm(target, Conjugatabel.ConjugationType.PresentNegative, polite: true),
        "taiPolitePast" => verb.DesireForm(target, Conjugatabel.ConjugationType.PastAffirmative, polite: true),
        "taiPolitePastNegative" => verb.DesireForm(target, Conjugatabel.ConjugationType.PastNegative, polite: true),
        _ => throw new NotSupportedException($"Unknown verb form '{form}'.")
    };

    private static string RenderAdjective(Adjective adjective, string form, Conjugatabel.ToConjugate target)
    {
        return form switch
        {
            "polite" => adjective.Conjugate(target, Conjugatabel.ConjugationType.PresentAffirmative),
            "politeNegative" => adjective.Conjugate(target, Conjugatabel.ConjugationType.PresentNegative),
            "politePast" => adjective.Conjugate(target, Conjugatabel.ConjugationType.PastAffirmative),
            "politePastNegative" => adjective.Conjugate(target, Conjugatabel.ConjugationType.PastNegative),
            "short" => adjective.ShortForm(target, Conjugatabel.ConjugationType.PresentAffirmative),
            "negative" => adjective.ShortForm(target, Conjugatabel.ConjugationType.PresentNegative),
            "past" => adjective.ShortForm(target, Conjugatabel.ConjugationType.PastAffirmative),
            "pastNegative" => adjective.ShortForm(target, Conjugatabel.ConjugationType.PastNegative),
            "te" => adjective.TeForm(target),
            "attributive" => adjective.AttributiveForm(target),
            "adverbial" => adjective.AdverbialForm(target),
            "adjectiveStem" => adjective.AdjectiveStem(target),
            _ => throw new NotSupportedException($"Unknown adjective form '{form}'.")
        };
    }

    private static bool IsSupportedCounter(Word word) =>
        Counters.TryGetValue(word.Japanese, out var counter) && word.Kana == counter.Kana;

    private static bool IsSupportedRequestEnding(Word word) => word.Japanese is "（〜を）お願いします" or "（〜を）ください" &&
        word.Kana is "（〜を）おねがいします" or "（〜を）ください";
}
