namespace DataLoaders.Models.Genki;

public sealed record GenkiLesson
{
    public int SchemaVersion { get; init; } = 2;
    public int Number { get; init; }
    public required string Title { get; init; }
    public required string SourcePages { get; init; }
    public IReadOnlyList<GenkiGrammarPoint> GrammarPoints { get; init; } = [];
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
}

/// <summary>Collection identity is independent of a verb/adjective's conjugation type.</summary>
public sealed record WordRef(string WordType, string Id)
{
    public override string ToString() => $"{WordType}:{Id}";
}

public sealed record SemanticTag
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> Parents { get; init; } = [];
}

public sealed record WordTags
{
    public required WordRef Word { get; init; }
    public required string WordFingerprint { get; init; }
    public IReadOnlyList<string> DirectTags { get; init; } = [];
}

public sealed record TagFilter
{
    public IReadOnlyList<string> AllOf { get; init; } = [];
    public IReadOnlyList<string> AnyOf { get; init; } = [];
    public IReadOnlyList<string> NoneOf { get; init; } = [];
}

public sealed record GenkiSlot
{
    public IReadOnlyList<string> WordTypes { get; init; } = [];
    public TagFilter Tags { get; init; } = new();
    public IReadOnlyList<string> ConjugationTypes { get; init; } = [];
}

/// <summary>Only grammatical material is literal. Lexical content has a real WordRef.</summary>
public sealed record GenkiSegment
{
    public required string Kind { get; init; }
    public string? Text { get; init; }
    public string? Name { get; init; }
    public WordRef? Word { get; init; }
    public string Form { get; init; } = "base";
    public IReadOnlyList<string> FormChoices { get; init; } = [];
    public IReadOnlyList<GenkiSegment> Children { get; init; } = [];
}

public sealed record GenkiRelation
{
    public required string Kind { get; init; }
    public required string Left { get; init; }
    public required string Right { get; init; }
    // compatible-tags rejects only explicitly declared incompatible pairs.
    public IReadOnlyList<TagPair> ForbiddenPairs { get; init; } = [];
}

public sealed record TagPair(string LeftTag, string RightTag);
public sealed record GrammarMaterial(string Text, string GrammarPointId);

public sealed record GenkiSchema
{
    public required string Id { get; init; }
    public required string GrammarPointId { get; init; }
    public string ExerciseType { get; init; } = "translation";
    public required string Register { get; init; }
    public required string Tense { get; init; }
    public required string Polarity { get; init; }
    public IReadOnlyList<string> PrerequisiteGrammarIds { get; init; } = [];
    public IReadOnlyList<GenkiSegment> Segments { get; init; } = [];
    public IReadOnlyDictionary<string, GenkiSlot> Slots { get; init; } = new Dictionary<string, GenkiSlot>();
    public IReadOnlyList<GenkiRelation> Relations { get; init; } = [];
    public IReadOnlyList<string> ValidationRules { get; init; } = [];
}

public sealed record GenkiAnswer
{
    public required string Japanese { get; init; }
    public required string Kana { get; init; }
    public IReadOnlyList<WordRef> RequiredWords { get; init; } = [];
}

public sealed record GenkiQuestion
{
    public int Version { get; init; } = 1;
    public required string Id { get; init; }
    public required string SchemaId { get; init; }
    public required string GrammarPointId { get; init; }
    public required string English { get; init; }
    public string? Setting { get; init; }
    public required string Register { get; init; }
    public IReadOnlyList<string> RequiredGrammar { get; init; } = [];
    // The first answer is canonical; vocabulary requirements belong to EACH answer.
    public IReadOnlyList<GenkiAnswer> Answers { get; init; } = [];
    public string? Provenance { get; init; }
}
