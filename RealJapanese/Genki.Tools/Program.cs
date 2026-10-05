using System.Text.Json;
using DataLoaders.Models.Genki;
using Genki.Generation;
using Genki.Inference;
using Repositories.Genki;

return await Cli.RunAsync(args);

internal static class Cli
{
    private const string Help = """
    Genki offline tools (no model is used by learner practice)
      inspect --config FILE
      tag --config FILE [--words noun:0,verb:1] [--tags place,food] --limit GROUPS
      generate --config FILE [--schemas ID,ID] [--words noun:0,noun:1] --limit CANDIDATES
      status --config FILE
      publish --config FILE --out questions.jsonl
      retry-failed --config FILE [--area tags|questions]

    Both tag and generate require --limit N, --minutes N, or an explicit --all.
    --all means exhaustive traversal of the selected schemas/vocabulary. There is
    no permanent quota: run limits pause coverage and the next run resumes work.
    --limit counts requested tag groups or newly processed candidates, respectively.
    Completed and rejected items are reused; uncertain items remain for review.
    Ctrl+C preserves successful stages. Only publish writes the learner question bank.
    Configuration paths are resolved relative to the config file. See docs/genki.md.
    """;
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h") { Console.WriteLine(Help); return 0; }
        using var cancel = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancel.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var command = args[0]; var flags = Parse(args.Skip(1).ToArray());
            if (!new[] { "inspect", "tag", "generate", "status", "publish", "retry-failed" }.Contains(command)) throw new ArgumentException("Unknown command. Use --help.");
            var configPath = Path.GetFullPath(Required(flags, "config"));
            var config = JsonSerializer.Deserialize<GenkiConfiguration>(File.ReadAllText(configPath), GenkiJson.Options)
                ?? throw new InvalidDataException("Empty configuration.");
            var basePath = Path.GetDirectoryName(configPath)!;
            var dataRoot = Path.GetFullPath(config.DataRoot, basePath); var stateRoot = Path.GetFullPath(config.StateRoot, basePath);
            if (stateRoot == dataRoot || stateRoot.StartsWith(dataRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("StateRoot must be separate from the source vocabulary directory.");
            var store = new BatchStore(stateRoot);
            if (command == "status") { PrintStatus(store); return 0; }
            var catalog = new GenkiCatalog(); var fullVocabulary = GenkiVocabulary.Load(dataRoot);
            catalog.ValidateVocabularyReferences(fullVocabulary);
            using var lease = store.Lock();
            if (command == "retry-failed") { ResetFailed(store, flags.GetValueOrDefault("area")); PrintStatus(store); return 0; }
            if (command == "publish")
            {
                var output = Path.GetFullPath(Required(flags, "out"));
                QuestionBankPublisher.Publish(store, catalog, fullVocabulary, output);
                Console.WriteLine($"Published completed questions to {output}"); return 0;
            }
            var words = Values(flags, "words").Select(ParseWord).ToArray();
            foreach (var word in words) fullVocabulary.Resolve(word);
            var vocabulary = words.Length == 0 ? fullVocabulary : new GenkiVocabulary(words.Select(fullVocabulary.Resolve));
            var schemaIds = Values(flags, "schemas");
            var schemas = schemaIds.Length == 0 ? catalog.Schemas : schemaIds.Select(id => catalog.Schemas.SingleOrDefault(s => s.Id == id)
                ?? throw new ArgumentException($"Unknown schema {id}.")).ToArray();
            CandidateEnumerator CreateEnumerator()
            {
                var annotations = BatchStore.ReadFile<WordTags[]>(Path.Combine(store.Root, "word-tags.json")) ?? [];
                return new(catalog, vocabulary, annotations.Where(a => vocabulary.Contains(a.Word)));
            }
            if (command == "inspect")
            {
                var enumerator = CreateEnumerator();
                Console.WriteLine($"{catalog.Lessons.Count} lessons, {catalog.Lessons.Sum(l => l.GrammarPoints.Count)} grammar points, {catalog.Schemas.Count} schemas, {catalog.Registry.Tags.Count} tags, {fullVocabulary.Entries.Count} vocabulary entries.");
                foreach (var schema in schemas)
                {
                    // Pool inspection does not traverse combinations, even when all relations would reject them.
                    Console.WriteLine($"{schema.Id}: semantic slot pools {JsonSerializer.Serialize(enumerator.SemanticPoolSizes(schema), GenkiJson.Compact)}; forms/relations may reduce these.");
                }
                return 0;
            }
            if (!flags.ContainsKey("all") && !flags.ContainsKey("limit") && !flags.ContainsKey("minutes"))
                throw new ArgumentException("Specify --limit/--minutes for a controlled run, or explicitly launch --all after reviewing the implementation.");
            var options = config.Batch with
            {
                Limit = flags.TryGetValue("limit", out var limit) ? int.Parse(limit!) : null,
                TimeBudget = flags.TryGetValue("minutes", out var minutes) ? TimeSpan.FromMinutes(double.Parse(minutes!, System.Globalization.CultureInfo.InvariantCulture)) : null
            };
            options.Validate();
            var modelsOptions = config.Models with { ModelsDirectory = Path.GetFullPath(config.Models.ModelsDirectory, basePath) };
            await using var models = new AiLibraryModels(modelsOptions);
            BatchSummary summary;
            if (command == "tag")
            {
                // Full source vocabulary remains available while a small word selection controls work.
                var tagIds = Values(flags, "tags");
                summary = await new SemanticTagger(fullVocabulary, catalog.Registry, store, models).RunAsync(options,
                    words.Length == 0 ? null : words, tagIds.Length == 0 ? null : tagIds, cancel.Token, Console.WriteLine);
            }
            else summary = await new QuestionPipeline(catalog, vocabulary, CreateEnumerator(), store, models)
                .RunAsync(schemas, options, cancel.Token, Console.WriteLine);
            Console.WriteLine(JsonSerializer.Serialize(summary, GenkiJson.Options));
            Console.WriteLine("Checkpoint retained. No learner bank changed; inspect state and run publish explicitly.");
            return summary.Failed > 0 ? 2 : 0;
        }
        catch (OperationCanceledException) { Console.WriteLine("Stopped. Completed evaluations and stages are durable; rerun the same command to resume."); return 130; }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        finally { Console.CancelKeyPress -= handler; }
    }
    private static Dictionary<string,string?> Parse(string[] args)
    {
        var result = new Dictionary<string,string?>(StringComparer.Ordinal);
        var known = new[] { "config", "words", "tags", "schemas", "limit", "minutes", "all", "out", "area" };
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument {args[i]}.");
            var key = args[i][2..];
            if (!known.Contains(key) || result.ContainsKey(key)) throw new ArgumentException($"Unknown/duplicate option --{key}.");
            if (key == "all") result[key] = null;
            else if (++i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)) result[key] = args[i];
            else throw new ArgumentException($"Missing value for --{key}.");
        }
        return result;
    }
    private static string Required(Dictionary<string,string?> flags, string name) => flags.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value : throw new ArgumentException($"--{name} is required.");
    private static string[] Values(Dictionary<string,string?> flags, string name) => flags.GetValueOrDefault(name)?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
    private static WordRef ParseWord(string value)
    {
        var parts = value.Split(':'); if (parts.Length != 2) throw new ArgumentException($"Invalid WordRef {value}."); return new(parts[0], parts[1]);
    }
    private static void PrintStatus(BatchStore store)
    {
        static Dictionary<string, long> Count(IEnumerable<string> statuses)
        {
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var status in statuses) counts[status] = counts.GetValueOrDefault(status) + 1;
            return counts;
        }
        Console.WriteLine("Questions: " + JsonSerializer.Serialize(Count(store.ReadAll<QuestionState>("questions").Select(s => s.Status)), GenkiJson.Compact));
        Console.WriteLine("Word × tag evaluations: " + JsonSerializer.Serialize(Count(store.ReadAll<TaggingState>("tags").SelectMany(s => s.Evaluations.Values).Select(e => e.Status)), GenkiJson.Compact));
        Console.WriteLine($"State: {store.Root}; inspect each hashed JSON checkpoint for stage results, reasons and provenance.");
    }
    private static void ResetFailed(BatchStore store, string? area)
    {
        if (area is not (null or "tags" or "questions")) throw new ArgumentException("--area must be tags or questions.");
        if (area is null or "questions") foreach (var state in store.ReadAll<QuestionState>("questions").Where(s => s.Status == "failed"))
        {
            if (state.ActiveStage is not null) state.Attempts[state.ActiveStage] = 0;
            state.Status = "pending"; store.Save("questions", state.CandidateId, state);
        }
        if (area is null or "tags") foreach (var state in store.ReadAll<TaggingState>("tags"))
        {
            foreach (var evaluation in state.Evaluations.Values.Where(e => e.Status == "failed")) { evaluation.Status = "pending"; evaluation.Attempts = 0; }
            store.Save("tags", state.Word.ToString(), state);
        }
    }
}
