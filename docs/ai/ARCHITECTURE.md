# Architecture

## Components and dependency direction

```text
Blazor web host -> Repositories -> DataLoaders -> local JSON files
Duplicate-cleanup console ------> DataLoaders
Kanji extraction console -------> DataLoaders
                          \----> external AiLibrary -> local llama-server/model
```

The [project map](PROJECT_MAP.md) owns paths and project relationships. These are
observed boundaries, not newly imposed dependency rules.

- **Web host:** registers singleton repositories and Interactive Server services,
  routes Razor pages, and owns browser-facing layout and practice components.
- **Repositories:** owns vocabulary/progress collections and persistence calls;
  converts selected domain records into question DTOs, romanizes text and generates
  number questions.
- **DataLoaders:** owns JSON/JSONL IO and serialized domain records. Verb/adjective
  conjugation behavior also lives in these models, despite the library's IO name.
- **Data utilities:** maintain the same datasets outside normal web requests.
  Extraction adds local model inference through external projects.

## State boundaries

Vocabulary, verbs, adjectives, kanji repositories and number generation are
singletons in `Program.cs`. The running server shares their in-memory state and
one set of progress files across sessions. No user identity, database transaction
or per-user persistence boundary is implemented.

Per-practice UI state (current question, input, revealed answer, chunk selection
and retry queue) resides in component instances. Shared cards communicate through
parameters/callbacks; JavaScript keyboard/focus helpers call .NET handlers through
Blazor interop. This is a server-interactive app, not a WebAssembly client.

## Important flows

### Vocabulary selection and persistence

1. A selector derives from `WordComponentBase<T>` and injects a typed repository.
2. Repository construction loads vocabulary and `SavedData.json`, then calls
   `UpdateIDs()` and saves the vocabulary file.
3. UI category actions remove membership from other categories, add the chosen
   membership and save progress through `WordDataBase<T>`.
4. Practice links pass `category=known|rehearsing|training` to the route.

Repository add/remove methods save immediately; they do not themselves guarantee
mutually exclusive membership. Exclusivity is coordinated by the selector.

### Practice rounds

A page such as `WordSpellingBase` parses the category, obtains repository records
and converts them to `QuestionAnswerDto` sequences. Shared answer bases select
chunks, shuffle, check normalized input and advance. `PracticeBase` owns revealed
question retries and round progression; single-answer, multiple-answer and
flashcard bases specialize interaction. Cards and `blazorHelpers.js` handle
keyboard/focus interactions.

### Data maintenance

Duplicate cleanup deduplicates words, assigns replacement IDs and remaps all three
progress lists before saving. Kanji extraction reads verbs/adjectives/words,
builds kanji-to-word relations, asks a local model to fill new kanji records and
writes the resulting datasets. The generated relation dataset is separate from
the web app's single/combined kanji vocabulary.

## Data contracts and constraints

- Relative file paths make working directory part of the runtime contract;
  [ASP.NET](modules/ASPNET.md) and [DOTNET](modules/DOTNET.md) own launch details.
- Vocabulary IDs connect records to saved progress and extracted relations.
  Preserving these associations matters when editing data or using maintenance tools.
- `JsonLoader<T>` dispatches by extension, supports JSON arrays or a single object,
  and reads JSONL line by line. Missing files propagate file IO errors.
- `JsonSaver<T>` writes directly to target files, creating directories. There is no
  atomic replacement or concurrency control in this persistence layer.
- `UpdateIDs()` expects nonempty vocabulary and assigns IDs to records with `-1`.
  It writes the primary dataset even during initialization. This is observed
  behavior, not a claim of concurrency safety or a rationale for the design.
- Practice category parsing rejects unknown/missing values; persisted progress
  lookups expect each referenced ID to exist in the corresponding dataset.

## Integrations and unknowns

WanaKanaSharp supports romanization and kanji detection. CsvHelper is declared by
DataLoaders; the inspected persistence path uses System.Text.Json. Bootstrap is
vendored under the web root. No configured remote service is required for the web
host. Extraction's external projects, model directory and `LLAMA_SERVER_PATH`
are a separate integration boundary.

Deployment topology, concurrent-use expectations and the rationale for singleton
file-backed storage are Unknown. No architecture rationale document was found;
see [DECISIONS](DECISIONS.md). Runtime validation is necessary before asserting
correctness of the observed flows.
