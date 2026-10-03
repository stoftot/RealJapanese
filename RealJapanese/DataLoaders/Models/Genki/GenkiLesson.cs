namespace DataLoaders.Models.Genki;

public sealed record GenkiLesson
{
    public int SchemaVersion { get; init; } = 1;
    public int Number { get; init; }
    public required string Title { get; init; }
    public required string SourcePages { get; init; }
    public IReadOnlyList<GenkiGrammarPoint> GrammarPoints { get; init; } = [];
    public IReadOnlyList<GenkiLexeme> Lexicon { get; init; } = [];
}

public sealed record GenkiGrammarPoint
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string SourcePages { get; init; }
    public required string Meaning { get; init; }
    public required string Formation { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public IReadOnlyList<string> Prerequisites { get; init; } = [];
    public IReadOnlyList<GenkiText> Examples { get; init; } = [];
    public IReadOnlyList<GenkiSchema> Schemas { get; init; } = [];
}

public record GenkiText
{
    public string English { get; init; } = "";
    public required string Japanese { get; init; }
    public required string Kana { get; init; }
}

public sealed record GenkiForm : GenkiText
{
    public int IntroducedLesson { get; init; }
}

/// <summary>Reviewed linguistic metadata around the application's existing word record.</summary>
public sealed record GenkiLexeme : Word
{
    public required string Key { get; init; }
    public required string WordType { get; init; }
    public int IntroducedLesson { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Accepts { get; init; } = [];
    public IReadOnlyDictionary<string, GenkiForm> Forms { get; init; } = new Dictionary<string, GenkiForm>();

    public GenkiForm? GetForm(string name) => name == "base"
        ? new() { Japanese = Japanese, Kana = Kana, English = English, IntroducedLesson = IntroducedLesson }
        : Forms.GetValueOrDefault(name);

    // Keep reviewed English (including articles/possessives needed by templates), tags and forms;
    // the dictionary supplies identity and Japanese text. Freeform Category cannot establish valency.
    public static GenkiLexeme FromWord(Word word, GenkiLexeme metadata)
    {
        if (metadata.Forms.Count > 0 && (metadata.Japanese != word.Japanese || metadata.Kana != word.Kana))
            throw new ArgumentException("Reviewed forms must belong to the supplied word.", nameof(metadata));
        return metadata with
        {
            Id = word.Id, Japanese = word.Japanese, Kana = word.Kana,
            Category = word.Category
        };
    }
}

public sealed record GenkiSlot
{
    public required string Name { get; init; }
    public required string WordType { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Forms { get; init; } = ["base"];
}

public sealed record GenkiRelation
{
    public required string Kind { get; init; }
    public required string Left { get; init; }
    public required string Right { get; init; }
}

public sealed record GenkiSchema : GenkiText
{
    public required string Id { get; init; }
    public string Kind { get; init; } = "translation";
    public string Instruction { get; init; } = "Write this in Japanese.";
    public required string Context { get; init; }
    public required string Register { get; init; }
    public required string Tense { get; init; }
    public required string Polarity { get; init; }
    public required string Omission { get; init; }
    public IReadOnlyList<string> Particles { get; init; } = [];
    public IReadOnlyList<string> Restrictions { get; init; } = [];
    public IReadOnlyList<string> Prerequisites { get; init; } = [];
    public IReadOnlyList<GenkiSlot> Slots { get; init; } = [];
    public IReadOnlyList<GenkiRelation> Relations { get; init; } = [];
    public IReadOnlyList<GenkiText> Alternatives { get; init; } = [];
}
