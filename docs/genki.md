# Genki offline generation and practice

Genki keeps the 12 lessons of **GENKI I, third edition** as concise recaps. The
curriculum retains all 86 grammar-point identities, including the supplementary
expression notes. The supplied book establishes progression and printed source
pages; it does not constrain the vocabulary or scenarios that can be generated.
The scanned PDF has nine images before printed page 1. Reading/writing sections
pair with the same lessons rather than defining another grammar sequence.

Practice reads a published question bank. It uses no model, network connection or
runtime sentence generator. The repository ships an empty bank intentionally;
controlled development fixtures are not production questions. A complete
production run is an explicit operator action after review.

## Projects and inputs

| Owner | Responsibility |
| --- | --- |
| `DataLoaders/Models/Genki` | Curriculum, schema, WordRef and question contracts; forms delegate to the existing word/conjugation models |
| `Repositories/Genki/Content/lesson-*.json` | Short recaps and source references; no exercise banks or lesson lexicons |
| `Repositories/Genki/Content/tags.json` | Reusable semantic classifications and parent relationships |
| `Repositories/Genki/Content/grammar-material.json` | Grammatical literals and the points that introduce them |
| `Repositories/Genki/Schemas/lesson-*.json` | Structural generation definitions |
| `Genki.Generation` | Lazy candidate enumeration, tagging, checkpoints and the offline A–H pipeline |
| `Genki.Inference` | Shared configuration and optional AiLibrary model adapter used by the offline tools |
| `Genki.Tools` | Independently runnable `tag`, `generate`, scan/status and publication workflows |
| `Genki.Studio` | Local PC browser workspace for configuration, scoped jobs, progress, review and explicit publication |
| `Repositories/Genki/GenkiPracticeService.cs` | Stored-question selection and deterministic comparison |

Paths in this table are below `RealJapanese/`. Existing vocabulary remains in
`Data/Words/Words.json`, `Nouns/Nouns.json`, `Verbs/Verbs.json` and
`Adjectives/Adjectives.json`. The tools read these files without assigning IDs or
rewriting them. A `WordRef` has `wordType` (`word`, `noun`, `verb`, `adjective`) and
a string `id`; `noun:1` and `verb:1` are different entries. Verb/adjective `type`
controls conjugation and is not the collection discriminator. Miscellaneous words
are not assumed to be nouns. Each dictionary object is the annotation unit;
conflicting senses go to review rather than a fabricated sense inventory.

The source catalog currently lacks some lexical heads needed by certain schemas.
Those schemas report no eligible combinations until real compatible vocabulary
and annotations exist. This is not a completed-coverage claim. Fixed references
must resolve to source entries; no model-created vocabulary is inserted.

## Build and configuration

### PC workspace

On Windows, double-click [Start Genki Studio.cmd](../Start%20Genki%20Studio.cmd)
from the repository root. It requires PowerShell 7 and the .NET 10 SDK, builds the Studio, starts
the local server at `http://127.0.0.1:5278`, and opens the browser when the page is
ready. Keep its console window open while using the workspace; press Ctrl+C there
to stop the server. The launcher stops only the server process it started.
It detects AiLibrary from `AI_LIBRARY_ROOT` or the existing extraction project
reference. If AiLibrary is unavailable, Studio still opens for setup, coverage
scans and review, while model tagging and generation are disabled. No job is
started or resumed automatically at launch.

The default configuration and generation state live under the ignored `.tooling/`
directory. Open **Configuration → Show and edit configuration** to set the data root,
state root, question-bank publish path and model settings, then save them. Paths
are resolved relative to the configuration file. `AI_MODELS_DIRECTORY` or the
legacy `AI_MODELS_PATH` initializes the model directory when creating a new
configuration; you can also set it in the workspace. Set `GENKI_STUDIO_CONFIG`
or pass `-ConfigFile <path>` to the launcher to use another config file. Use
`-NoBrowser` to keep the server in the console without opening a browser, or
`-Url http://127.0.0.1:<port>` to use another loopback port when the default is
already occupied.

A safe first run is:

1. Save the local paths and model settings in Workspace configuration.
2. Tag only a small selected group of words, then inspect uncertain and failed
   tag evaluations before relying on semantic filters.
3. Select a lesson, grammar pattern and a small vocabulary subset. Run **Scan
   coverage · no models** first. A bounded scan reports visited and unseen
   combinations; only a completed scan gives an exact count for its recorded
   scope. The last scan can become stale if the vocabulary, schemas or tags change.
4. Queue a bounded **Generate questions** run. The job card shows examined
   candidates and completed, review, rejected and failed counts. You can pause,
   resume or cancel; successful stages are saved. On application restart, active
   jobs are paused and require an explicit Resume.
5. Load and review generated questions. Use **Review publication** to confirm the
   configured destination, then explicitly publish. Cancelling the confirmation
   leaves the learner bank untouched. **Regenerate selected scope** first asks
   for confirmation and archives the old checkpoints; it does not rewrite the
   learner bank.

The pattern **Queue status** filter describes generation jobs for a schema. A
coverage scan is recorded as a job and updates scan coverage, but does not mark a
pattern as queued for generation. Upper bounds are approximate before traversal;
partial scans never label unvisited candidates complete.

Jobs retain the configuration saved when they were queued. Pause or cancel active
and queued jobs before changing settings. A larger coverage scan recounts its
selected scope from the beginning; generation resumes using successful checkpoints.
Use **Run exhaustive coverage** only when you deliberately want to remove both
run limits. **Previously queued** records job history, not proof of complete
candidate coverage.

Studio is a local PC web app separate from the learner-facing web and Android
hosts. It binds only to loopback and does not add model inference to practice.

### Command line

The app and deterministic tests do not reference AiLibrary. Build the offline tool
with the external checkout explicitly supplied (PowerShell, repository root):

```powershell
$env:AI_LIBRARY_ROOT = 'C:\path\to\Ai library'
dotnet build RealJapanese/Genki.Tools/Genki.Tools.csproj -m:1
```

Alternatively pass `-p:AiLibraryRoot='C:\path\to\Ai library'` to the build. The
adapter references `src/AiLibrary.Core` and `src/AiLibrary.LlamaServer` in that
checkout. Without these references the CLI still builds for deterministic
inspection/publication; inference reports that live model support is unavailable.
Do not use `--no-build` with an old binary expecting a changed build property to
add model support.

Copy [the configuration example](../RealJapanese/Genki.Tools/appsettings.example.json)
to a local file and set `dataRoot`, `stateRoot` and `models.modelsDirectory`.
Relative data, state and models-directory paths are resolved beside the config file. Keep state
outside the vocabulary tree. State and configuration can live under the ignored
`.tooling/` directory. `models.serverExecutable` can give the absolute path to `llama-server`, or
leave it null for PATH discovery. Qwen and LFM IDs default to the profiles actually
exposed by AiLibrary: `qwen2.5-14b-instruct` and `lfm2-enjp-translation`. Their bundled
profiles expect `Qwen2.5-14B-Instruct-Q4_K_M.gguf` and
`LFM2-350M-ENJP-MT-F16.gguf` in the configured directory.

Model IDs, GGUF file overrides (`qwenModelFile` and `translationModelFile`, relative
to the models directory or absolute), context sizes, temperature, output tokens,
request timeout and GPU-layer settings are configurable. Qwen defaults to a 16,384
token context and LFM to 4,096; keep each context larger than `maxTokens`, with room
for the input. One request runs at a time. The process disposes its model service
when it exits. No hosted model is used, and build/inspect/status do not infer.
Qwen receives structured linguistic tasks; LFM receives the Japanese user message
with setting/register in the system message. AiLibrary owns backend/model startup.

## Controlled checks, full runs and resume

Use the compiled CLI to avoid accidentally rebuilding without AiLibrary:

```powershell
$tool = 'RealJapanese/Genki.Tools/bin/Debug/net10.0/Genki.Tools.dll'
$config = '.tooling/genki.local.json'
dotnet $tool inspect --config $config --schemas g01-01-noun-predicate --words noun:0,noun:1
dotnet $tool tag --config $config --words noun:0 --tags place,person --limit 1
dotnet $tool generate --config $config --schemas g01-01-noun-predicate --words noun:0,noun:1 --limit 2
dotnet $tool status --config $config
```

These examples deliberately select small real subsets. A schema may need tagged
words or fixed references outside that subset and then produce no candidate.
`inspect` counts semantic slot pools without traversing their Cartesian products;
form and relation constraints can further reduce those pools.

After inspecting the implementation and its outputs, the operator can explicitly
launch exhaustive work:

```powershell
dotnet $tool tag --config $config --all
dotnet $tool generate --config $config --all
```

`--all` is an explicit full-run opt-in, not a hidden default. Alternatively use
`--limit N` or `--minutes N`; either pauses work without recording unvisited items
as complete. Tag limits count model request groups; generation limits count newly
processed candidates. `batch.tagGroupSize`, `maxAttempts`, `alternativeLimit` and
`delayMilliseconds` control resources. Ctrl+C stops at a checkpoint; completed
stages survive. Rerun the same command to resume. New words, tags, schemas and
newly positive annotations introduce new work automatically.

Enumeration sorts collection-qualified IDs, lazily traverses each active slot's
Cartesian choices, and checks existing candidate state by a SHA-256 identity of
schema ID, **named bindings**, and optional/form choices. It does not save a list
index cursor, preload the Cartesian product, create pending jobs for every
combination, sample randomly or impose a permanent quota. Reordering source lists
cannot skip combinations. Omitted groups do not bind their unused slots. A resume
may revisit earlier identities to find new work; completed results are not sent
to models again. Runtime round sampling is separate from exhaustive batch coverage.

## Schema and semantic contracts

The C# records, strict JSON deserialization (unknown members rejected),
`GenkiCatalog` validation and `SemanticRegistry` constitute the machine validator.
Invalid IDs, missing/cyclic tag parents, future prerequisites, undeclared literal
material, invalid segment kinds, references and relation kinds fail loading.
Schemas contain no finished English prompt, kana answer, scenario or accepted set.

A schema identifies the target grammar, exercise type, register, tense, polarity,
explicit prerequisites, typed slots, segments, deterministic relations and model
validation rules. Point prerequisites are closed transitively. Earlier points in
the same lesson may be required; later points cannot be prerequisites.

| Segment | Meaning |
| --- | --- |
| `literal` | Exact registered grammatical text or punctuation; its introduction must be in the allowed grammar closure |
| `slot` | One named selected dictionary object, with a form for this occurrence |
| `fixed` | A real `word: {wordType,id}` and its form; it always contributes a vocabulary requirement |
| `optional` | Include or omit its entire `children` group, including that phrase's particles |

`formChoices` enumerates a finite set of forms for an occurrence. Repeated slot
names reuse the same vocabulary object and may have different occurrence forms.
Forms map to `GenkiForms` and existing `Verb`/`Adjective` methods: base, polite and
short tense/polarity forms, te, masu/negative stems, desire forms, adjective
attributive/adverbial/stem forms. The supported counter/request template forms
unwrap only explicit existing dictionary patterns. Unsupported forms do not get
model-generated replacements. Form introduction grammar is checked per candidate.

Tag IDs represent reusable classifications. Direct child membership implies all
ancestors, through every parent path; a parent never implies its children. Parent
links mean “is a kind of,” not association. The clean mapping stores the most
specific direct matches; inherited membership is computed. On a slot:

- `allOf`: every listed tag must match.
- `anyOf`: at least one must match, unless the list is empty.
- `noneOf`: no listed tag may positively match.
- All three empty: no semantic filter. `wordTypes` still restrict the collection.

An unknown tag evaluation is not a negative result. `noneOf` conservatively excludes
known positive matches; uncertainty about other combinations is assessed by Qwen.
`conjugationTypes` optionally restricts the existing verb/adjective type field.
The deterministic relations are `distinct` (different WordRefs), `same` (same
WordRef) and `compatible-tags` (reject the explicitly listed inherited tag pairs in
`forbiddenPairs`). No arbitrary relation text is interpreted as code. Descriptive
`validationRules` go to Qwen for broader naturalness judgments.

## Tag state and failure handling

Each word checkpoint in `stateRoot/tags/<hash>.json` retains its source fingerprint
and a result for every evaluated tag. Results distinguish pending/in-progress,
matches, does-not-match, uncertain and failed. Missing entries mean unknown.
Definition fingerprints include ancestors. A successful response must name exactly
the requested group once; omissions, unexpected tags and malformed output are
failures, not negative classifications. Existing positive and negative evaluations
are reused. A newly added tag still reaches words processed on previous runs.

A child match conflicting with an explicit negative ancestor, or an ambiguous
entry, excludes that word from the clean mapping pending review. An omitted
ancestor is inherited rather than treated as negative. `word-tags.json` is written
separately from coverage state. Models receive the original meaning, collection,
conjugation type and tag definitions; they do not invent dictionary entries.

Checkpoint writes use flushed temporary files and atomic replacement. An OS-held
writer lock prevents concurrent writes from corrupting a state directory and
releases after a crash. In-flight work can be retried; successful stages are reused.
The read-only `status` command can run while a batch is active; counts reflect
the atomic checkpoints observed during that scan.
Transport/model errors and malformed responses have bounded retries. Completed,
rejected and review-needed items are not retried on every unchanged run. To retry
failed work after correcting a connection/model issue:

```powershell
dotnet $tool retry-failed --config $config --area questions
# Use --area tags for failed tag evaluations, then rerun the bounded/full command.
```

Review the reason, attempt counts, full successful stage outputs and provenance in
the JSON checkpoints. Linguistic rejections and uncertainty are separate from
transport failures. An uncertain judgment is never silently promoted to training
content. Model approval is evidence, not a guarantee of linguistic correctness.

## Question stages and provenance

Each `stateRoot/questions/<hash>.json` is one candidate's resumable record:

1. **A:** render canonical Japanese through known forms; save named bindings,
   structural choices, fixed/selected word references and expected readings.
2. **B:** Qwen checks grammar, meaning, ordinary plausibility and naturalness.
3. **C:** Qwen supplies brief English context only where useful and rechecks the
   unchanged candidate. Context belongs to this question, never the schema.
4. **D:** LFM translates the canonical sentence. Qwen checks faithfulness before
   that single English prompt is used as the basis for alternatives.
5. **E:** Qwen proposes a configurable bounded set of alternative Japanese answers.
6. **F:** every alternative must resolve to existing vocabulary and allowed
   grammatical segments. Qwen checks its meaning and construction; LFM
   back-translates, then Qwen judges equivalence. Matching English alone proves
   neither grammar nor meaning. Unvalidated alternatives remain excluded.
7. **G:** Qwen produces complete kana. It must agree with tracked word-form readings
   and grammatical orthography. Preserve は/へ/を and appropriate katakana; do not
   substitute romaji. Conflicting/uncertain canonical readings block publication.
8. **H:** validate per-answer dependencies and deduplicate answers within the
   question, preserving the canonical answer first; mark complete atomically.

Alternatives must express the same facts, referents, tense, polarity, register and
target construction in the same setting. Omissions cannot remove the target
construction. Any different existing vocabulary carries that answer's own refs.
The generator never corrects an invalid candidate in place under its old bindings.
Question identities are not globally merged merely because Japanese text matches.

## Publication and learner behavior

After reviewing completed outputs, publish explicitly:

```powershell
dotnet $tool publish --config $config --out RealJapanese/Data/Genki/questions.jsonl
```

Publication validates source schema/word provenance and atomically replaces the
JSONL bank using only completed states. Every question has one English prompt,
optional setting, register, schema/grammar identity, required grammar and answers.
Each answer has Japanese, kana and **its own** `requiredWords`. There is no union of
all alternative vocabulary as an eligibility requirement.

The web host reads the bank beneath its configured catalog root. Android packages
that file and installs it with the catalogs; rebuild/redeploy the APK to distribute
a revised bank. It is not part of progress sync. Development runs use isolated
state and fixture roots; they never publish sample banks to `RealJapanese/Data`.

The learner selects a lesson or point. Its lesson progression supplies available
grammar, including declared earlier prerequisites. A question is eligible when at
least one stored answer uses only words marked **Known** in the existing vocabulary
progress. New known words can therefore unlock earlier patterns. The round shows
known-word explanations first and accepts **all** stored answers, even an approved
answer whose words the learner has not marked known. Rounds and retries are
session-local; they do not claim vocabulary mastery or change the sync format.

Comparison normalizes Unicode compatibility/width and whitespace and ignores
terminal sentence punctuation. It does not remove internal particles/punctuation,
use fuzzy similarity or call a model. A mismatch means “not in the stored accepted
set,” not proof that another valid formulation is wrong. Kana is accepted directly;
romaji conversion remains outside this workflow. Empty-bank/no-eligible states
explain the missing content/knowledge instead of falling back to hard-coded prompts.

## Edits, review and stale data

Incremental **additions** and stop/resume are implemented. Automatic dependency-wide
regeneration after editing existing material is not. Word/tag fingerprints and
candidate/schema/model-prompt provenance catch many stale uses and stop with an
explicit error; they do not replace a complete invalidation graph. In particular,
changed annotation meanings/hierarchy, corrected accepted alternatives, model files
replaced under the same path, prompt edits without a version change, and changed
runtime grammar policy require operator review and reprocessing.

Before changing existing words, tags, schemas, model settings or prompts, back up
the state directory and published bank. Stop tools, inspect the affected JSON
checkpoints, and remove the exact affected tag/question checkpoint files (or use a
fresh separate state directory for the affected dataset). Do not delete canonical
vocabulary or progress. Changing an ancestor may require re-evaluating descendants;
changing a word may affect every answer that uses it. Rerun tagging, regenerate the
clean mapping, rerun affected schemas, review and republish. For uncertain output,
correct the cause and remove that candidate/evaluation checkpoint before retrying;
there is no “approve everything” command. Keep previous banks out of use while
known stale questions are being regenerated. Removed schemas/words are rejected
when publication/runtime validation finds their references.

## Validation

The normal xUnit suite covers word identity, conjugation oracles, hierarchy/filter
semantics, persistent word×tag coverage, small exhaustive products, additions,
interruption/failure recovery, lexical dependency tracking, pipeline stages and
stored-answer eligibility. Component and real-browser tests use isolated fixture
banks and progress. No model service is required for those tests. Live-model smoke
checks should always specify small word/schema sets and a run limit; a successful
small check is not a claim that the full production corpus has been generated or
linguistically reviewed. [Manual cases](../tests/manual/genki.md) cover operator
review and Android/native checks.
