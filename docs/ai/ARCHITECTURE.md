# Architecture

## Components and dependency direction

```text
ASP.NET host -------\
                     > RealJapanese.UI -> Repositories -> DataLoaders -> local JSON
MAUI Android host --/                       ^
       |                                    |
       +-> packaged catalogs -> app storage-+

Duplicate-cleanup console -------------------------------> DataLoaders
Kanji extraction console --------------------------------> DataLoaders
                                                    \----> external AiLibrary
```

The [project map](PROJECT_MAP.md) owns paths and project relationships.

- **Web host:** owns the HTML document, error page, Interactive Server setup,
  middleware and web-specific storage configuration.
- **Android host:** owns MAUI startup, the local `BlazorWebView`, app-private
  catalog installation and Android lifecycle/package configuration.
- **Shared UI:** owns routes, navigation, study pages, reusable components and
  their CSS/JavaScript/Bootstrap assets. It contains no host startup.
- **Repositories:** owns catalog/progress collections, persistence, question
  conversion, romanization and number generation.
- **DataLoaders:** owns JSON/JSONL IO and serialized domain records. Verb/adjective
  conjugation behavior also lives in these models.
- **Data utilities:** maintain canonical datasets outside normal app execution.

The mobile app follows the standard MAUI Blazor Hybrid shape described by
[Microsoft's MAUI Blazor Hybrid guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0):
Razor components execute in the native app and render into a local WebView.

## Host and state boundaries

Both hosts register repository instances as singletons within their own process.
They share code and catalog content but do not share running state or save files.

- Web catalog and progress roots come from `StudyData:CatalogRoot` and
  `StudyData:ProgressRoot`. Both default to the existing `RealJapanese/Data/` tree.
- Android uses `FileSystem.AppDataDirectory/Catalog` for copied catalogs and
  `FileSystem.AppDataDirectory/Progress` for saves.
- There is no account, server API, cloud backup or synchronization boundary.
- Web sessions need the local ASP.NET process because the UI uses Interactive
  Server. Android study features run offline in-process without that server.

Per-practice UI state remains in component instances. Shared cards communicate
through parameters/callbacks; JavaScript keyboard/focus helpers use the host's
Blazor interop runtime.

## Important flows

### Catalog and progress initialization

1. A host registers a `RepositoryPaths` value with independent catalog/progress roots.
2. A repository reads its catalog once. Missing IDs are assigned deterministically
   in memory; startup does not rewrite the source catalog.
3. Missing progress initializes as empty. Existing progress is loaded from its own
   root, and IDs absent from the current catalog are ignored in memory.
4. Category changes save only `SavedData.json` under the progress root.

On Android, `StudyDataInstaller` first copies these five packaged files to the
private catalog root, writing a temporary file before replacing each earlier copy:

- `Words/Words.json`
- `Verbs/Verbs.json`
- `Adjectives/Adjectives.json`
- `Kanji/Singel/Singel.json`
- `Kanji/Combined/Combined.json`

Packaged assets never include web `SavedData.json` files, and catalog refreshes do
not touch the private progress root.

### Vocabulary selection and practice

A selector derives from `WordComponentBase<T>` and injects a typed repository.
Category actions coordinate exclusive known/training/rehearsing membership and
save progress. Practice links pass `category=known|rehearsing|training`; shared
practice bases select chunks, shuffle, check normalized input and manage retries.

### Data maintenance

Duplicate cleanup deduplicates words, assigns replacement IDs and remaps progress.
Kanji extraction reads verbs/adjectives/words, builds relations, asks a local model
to fill new records and writes generated datasets. These utilities are not used by
the web or Android runtime.

## Data contracts and constraints

- Vocabulary IDs connect records to saved progress and extracted relations.
- `JsonLoader<T>` supports JSON arrays/single objects and JSONL. Missing catalog
  files remain an error; missing progress is valid and starts empty.
- `JsonSaver<T>` creates directories and writes directly. It has no transaction or
  cross-process concurrency control.
- The `Singel` directory spelling is part of the current persisted path contract.
- Practice category parsing rejects unknown or missing query values.

## Integrations and current limits

WanaKanaSharp supports romanization and kanji detection. CsvHelper is declared by
DataLoaders. No remote service is needed by either application host. Extraction's
external projects, model directory and `LLAMA_SERVER_PATH` form a separate local
integration boundary.

The web and Android Debug builds have been observed passing. The Android build
produced a sideloadable debug APK, but installation, startup and device behavior
remain unverified; no connected adb target or installed AVD was available.
