# Manual regression tests

This is the durable catalog for repeatable application scenarios that need browser,
device or human observation. [validate-change](../../.agents/skills/validate-change/SKILL.md)
selects relevant cases and executes them with available tooling where reliable;
a manual specification does not imply that human execution is always necessary.
Keep run results and unexecuted steps in the current task, not in these files.

## Catalog and existing coverage

| Domain | Cases |
| --- | --- |
| [Kana](kana.md) | KANA-001–003: character selections/settings, answers/review, local persistence, responsive charts and offline fonts |
| [Genki](genki.md) | GENKI-001–002: lesson recaps, sentence production/retry, vocabulary expansion and curriculum/language review |
| [Local sync](local-sync.md) | SYNC-001–006: Automatic/Manual transfer, cancellation, pairing rejection, merge/recovery and errors across web and Android |
| [Study selection and practice](study-practice.md) | STUDY-001–008: search, categories, routes, answers/retries, chunks, flashcards, multiple answers, layout and language oracles |
| [Android lifecycle](mobile-lifecycle.md) | MOBILE-001–003: offline catalogs, update/restart persistence, IME/Back/lifecycle and controlled startup failure |
| [Data maintenance](data-maintenance.md) | DATA-001–002: controlled extraction/model validation and interrupted legacy cleanup |

The initial cases reuse behavior documented in the [sync guide](../../docs/local-sync.md).
That guide remains the owner of user instructions and network/compatibility limits.
This catalog is selective, not a claim of complete application regression coverage.

Automated coverage lives in the xUnit unit/integration projects, bUnit component
project and Playwright browser project. Use the commands in the
[testing guide](../../docs/testing.md). Do not repeat their assertions manually
unless a different host or human observation adds evidence.
[Tooling verification](../../docs/verification.md) owns tool acceptance procedures,
which do not establish application correctness.

## Automation and remaining manual scope

The specifications below remain reusable acceptance procedures. This table records
current ownership, not execution history. Automated web coverage does not establish
Android WebView, OS lifecycle or two-device Wi-Fi behavior. Known-defect cases
assert intended behavior but require explicit execution; see the testing guide.

| Case | Automated coverage | Remaining manual scope / reason |
| --- | --- | --- |
| KANA-001–003 | `KanaCatalogTests`, `KanaSessionTests`; `KanaBrowserTests` persistence/isolation, answers/review, options, storage errors, 320/1280 px and local font loading | Android WebView persistence, native IME/Back, offline cold start and human font/readability assessment |
| GENKI-001–002 | `GenkiTests`, `GenkiComponentTests`, `GenkiBrowserTests`: coverage, safe generation, progression, comparison, retry, navigation and responsive layout | Android offline packaging/IME and human language review for new content |
| STUDY-001 | `SelectorComponentTests`; `RepositoryPersistenceTests`; `StudyBrowserTests` selection/reload/restart; `NounPracticeBrowserTests` noun assignment | Android UI/save integration; browser restart check samples Words while shared repositories cover all datasets |
| STUDY-002 | `PracticeRouteComponentTests` selector routes, category data and empty states; `NounPracticeBrowserTests` noun spelling and flashcards | Android navigation integration; component rendering alone does not establish the native host |
| STUDY-003 | `SingleAnswerPracticeComponentTests`; `StudyBrowserTests` real Enter/focus | Android IME and soft-keyboard behavior |
| STUDY-004 | `ChunkingPracticeComponentTests`; unit chunk contracts; explicit retry-leak regression | Repeat on Android after a shared fix; known defects are not passing coverage |
| STUDY-005 | `FlashcardComponentTests`; `FlashcardBrowserTests` Space/Backspace and SPA disposal | Android hardware/emulator keyboard behavior |
| STUDY-006 | `MultipleAnswerPracticeComponentTests` accepted-order/duplicate/reset behavior | Native keyboard/focus integration |
| STUDY-007 | `SharedPracticeControlTests`; `LayoutBrowserTests` 320/1280 px, column visibility, scale and overflow | Human readability, visible focus quality and screen-reader announcements; browser assertions cannot judge these fully |
| STUDY-008 | Literal conjugation/number unit oracles and `AdjectiveLanguageOracleComponentTests`, including explicit known-defect cases | Human language review when adding new catalog content or accepted readings |
| SYNC-001 | Real loopback transfer and private-interface discovery integration tests; shared web approval/apply flow | Automatic discovery over actual Wi-Fi and Windows/Android cryptographic interoperability, both directions |
| SYNC-002 | Two-host Playwright sync through Manual mode | Actual web/Android reachability and transfer in both directions |
| SYNC-003 | `SyncPreviewBrowserTests`: Cancel, close, backdrop and Escape, no-write and fresh transfer; `SyncDialogComponentTests` | Native WebView dialog behavior and other-device dismissal combinations |
| SYNC-004 | `SyncPairingBrowserTests`: one approval, denial, dismissal, navigation stops listener, renewed transfer; socket cancellation tests | Android navigation/background and peer combinations |
| SYNC-005 | `SyncRecoveryBrowserTests`: all mode counts, second-circuit stale preview, refresh/apply, cancel and recovery after restart; storage merge/recovery matrix | Android dialog and cold-start integration |
| SYNC-006 | `SyncErrorBrowserTests`: invalid/public addresses, port zero, mismatched catalog bytes and corrected retry | Android network error presentation/recovery |
| MOBILE-001 | Shared catalog/progress restart contracts covered in integration tests | Native packaging, first install, offline operation and same-signer update require disposable Android installation |
| MOBILE-002 | Shared practice state covered at component/browser levels | IME, Back, rotation and foreground lifecycle require native device automation or observation; no device automation runner is configured |
| MOBILE-003 | No native fault-injection automation | Needs a disposable package or controllable packaged-file provider; current installer directly uses MAUI FileSystem |
| DATA-001 | No portable external-model extraction automation | External AiLibrary projects/model/server and controlled responses are required; current utility has no isolated provider seam |
| DATA-002 | `UtilityCleanupIntegrationTests` explicit Windows file-lock consistency regression | Optional fault-injection confirmation on other supported filesystems; normal cleanup/guard/remap is already automated |

Classes are in the corresponding [`tests/`](../) projects. Keep full procedures
for the residual host combinations and for diagnosis; prioritize the unautomated
steps when selecting manual validation. Do not claim that a known failing
regression or unavailable platform was validated successfully.

## Case conventions

Group related cases in one feature/domain file. Use stable IDs such as `SYNC-001`
and retain IDs when updating a case; do not reuse a retired ID for different behavior.
Each case contains:

- **Purpose:** the regression it protects.
- **Preconditions:** environment, test data and initial state needed to repeat it.
- **Steps:** ordered, observable actions.
- **Expected result:** concrete pass/fail observations.

Include cleanup or restoration steps where a case changes state. Use disposable
test installations/progress and preserve user-owned catalogs and saved progress.
Reference canonical setup and behavior docs instead of copying their details.
New cases are added/updated through [create-tests](../../.agents/skills/create-tests/SKILL.md)
when warranted by a clear plan; do not record implementation history here.
