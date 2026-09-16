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
They share code and catalog content but keep separate running state and save files
until the user explicitly transfers progress over the local network.

- Web catalog and progress roots come from `StudyData:CatalogRoot` and
  `StudyData:ProgressRoot`. Both default to the existing `RealJapanese/Data/` tree.
- Android uses `FileSystem.AppDataDirectory/Catalog` for copied catalogs and
  `FileSystem.AppDataDirectory/Progress` for saves.
- There is no account, cloud service or background synchronization boundary.
- The shared `/sync` page exposes a short-lived, user-initiated local TCP transfer
  between two open app instances on the same private IPv4 network.
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
3. Progress is stored for all five datasets in one versioned `Progress.json` file.
   A missing bundle is initialized once from the five legacy `SavedData.json`
   files; those files remain untouched and are not updated afterward.
4. Category changes commit the complete bundle through a temporary file and atomic
   replace. The store serializes in-process changes and refuses a commit if another
   process changed the on-disk revision; repositories do not live-refresh across processes.

On Android, `StudyDataInstaller` first copies these five packaged files to the
private catalog root, writing a temporary file before replacing each earlier copy:

- `Words/Words.json`
- `Verbs/Verbs.json`
- `Adjectives/Adjectives.json`
- `Kanji/Singel/Singel.json`
- `Kanji/Combined/Combined.json`

Packaged assets never include web `SavedData.json` files, and catalog refreshes do
not touch the private progress root.

### Local progress transfer

1. The sharing app freezes its current progress snapshot and listens on a temporary
   random TCP port for at most five minutes.
2. The receiving app connects with a displayed IPv4 address and port. It sends the
   fixed empty `RJLAN002` fetch request; the sender returns one bounded framed
   snapshot reply of at most 4 MiB, then stops sharing after the first successful
   fetch.
3. The receiver rejects unknown schema/IDs or any catalog whose raw-file SHA-256
   differs, then previews `MergeKeepLocal`, `MergeUseIncoming` or `Replace` before
   writing. `MergeKeepLocal` is the default.
4. Import atomically replaces the bundle and persists the previous state as the
   one-level recovery snapshot. A later import replaces recovery; ordinary study
   changes do not discard it.

The listener and receiver accept only literal RFC1918 private IPv4 or loopback
addresses; hostnames and other address forms are rejected. The listener is disposed
on success, cancel, page navigation or expiry. Transfer code has no arbitrary-path
or remote-write API. It does not change firewall rules or configure router forwarding.

The transport has no pairing secret, encryption or sender authentication. Anyone
on the LAN who knows the displayed address and port can fetch the snapshot while
sharing is active, and a network peer can observe, alter or substitute traffic.
The security boundary is therefore a trusted local network plus the receiver's
strict validation and user-reviewed preview, rather than transport authentication.

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
- `Progress.json` is the single atomic persistence boundary for study progress.
  Its revision and interprocess write lock prevent silent concurrent overwrite;
  live cross-process updates are outside the design.
- The `Singel` directory spelling is part of the current persisted path contract.
- Practice category parsing rejects unknown or missing query values.

## Integrations and current limits

WanaKanaSharp supports romanization and kanji detection. CsvHelper is declared by
DataLoaders. No remote service is needed by either application host. Extraction's
external projects, model directory and `LLAMA_SERVER_PATH` form a separate local
integration boundary.

Web and Android Debug builds pass. Browser and physical Android checks exercise
`RJLAN002` two-way Wi-Fi transfer using only IP and port, preview/apply, and matching
saved selections. StorageChecks covers conflict resolution, restart recovery and
malformed frames. Device checks use disposable progress and a temporary app identity
to preserve the installed app, whose signing key differs from the local development
key. Broader practice/device coverage remains outside these sync checks.
