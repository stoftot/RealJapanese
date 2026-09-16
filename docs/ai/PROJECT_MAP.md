# Project Map

## Repository at a glance

The C# projects and four solutions sit under `RealJapanese/`:

| Solution | Use |
| --- | --- |
| [`RealJapanese.Web.sln`](../../RealJapanese/RealJapanese.Web.sln) | Web host plus shared UI and data libraries |
| [`RealJapanese.Mobile.sln`](../../RealJapanese/RealJapanese.Mobile.sln) | Android host plus shared UI and data libraries |
| [`RealJapanese.Shared.sln`](../../RealJapanese/RealJapanese.Shared.sln) | Shared UI/data work and `StorageChecks` |
| [`RealJapanese.sln`](../../RealJapanese/RealJapanese.sln) | Existing web-and-utilities aggregate; intentionally excludes mobile |

Choose the smallest solution for the work. See the [development guide](../development.md)
for commands and prerequisites.

## Main areas

| Path | Role | Relationships |
| --- | --- | --- |
| `RealJapanese/RealJapanese/` | ASP.NET Core Blazor host | References RealJapanese.UI |
| `RealJapanese/RealJapanese.Mobile/` | Android-only MAUI Blazor Hybrid host | References RealJapanese.UI; installs packaged catalogs |
| `RealJapanese/RealJapanese.UI/` | Shared routes, study pages, reusable Razor components and static assets | References Repositories |
| `RealJapanese/Repositories/` | Vocabulary/progress, local sync transport, question conversion and number generation | Depends on DataLoaders and WanaKanaSharp |
| `RealJapanese/DataLoaders/` | JSON IO, serialized models and conjugation logic | Shared by repositories and utilities |
| `RealJapanese/Data/` | Canonical study datasets, web progress and extracted kanji relations | Source for web and packaged mobile catalogs |
| `RealJapanese/StorageChecks/` | Dependency-free storage regression executable | Uses real catalogs read-only and disposable progress |
| `RealJapanese/CheckDataForDuplicates/` | Console cleanup utility | Rewrites word IDs and corresponding progress |
| `RealJapanese/Extract kanji/` | Console extraction/enrichment utility | Uses DataLoaders and external AiLibrary projects |
| `docs/ai/`, `.agents/skills/` | Project facts and workflows | Ownership in [README](README.md) |

## Projects and checks

Paths below are relative to `RealJapanese/`. All projects target .NET 10.

| Project | Type / TFM | Direct project dependencies | Checks |
| --- | --- | --- | --- |
| `RealJapanese/RealJapanese.csproj` | Web executable / `net10.0` | RealJapanese.UI | Browser checks as needed |
| `RealJapanese.Mobile/RealJapanese.Mobile.csproj` | MAUI executable / `net10.0-android` | RealJapanese.UI | Build/device checks as available |
| `RealJapanese.UI/RealJapanese.UI.csproj` | Razor class library / `net10.0` | Repositories | Covered through hosts |
| `Repositories/Repositories.csproj` | Library / `net10.0` | DataLoaders | StorageChecks |
| `DataLoaders/DataLoaders.csproj` | Library / `net10.0` | None | StorageChecks |
| `StorageChecks/StorageChecks.csproj` | Console regression check / `net10.0` | Repositories | Self-checking executable |
| `CheckDataForDuplicates/CheckDataForDuplicates.csproj` | Console utility / `net10.0` | DataLoaders | None found |
| `Extract kanji/Extract kanji.csproj` | Console utility / `net10.0` | DataLoaders; external AiLibrary projects | None found |

No test-framework project, CI pipeline or automated browser/device suite was found.
`tooling/verify-*` and ignored `.tooling/scratch/` fixtures verify tools, not the app.

## Entry points and investigation anchors

| Purpose | Path from repository root |
| --- | --- |
| Web startup, paths and registration | `RealJapanese/RealJapanese/Program.cs` |
| Web document/error host components | `RealJapanese/RealJapanese/Components/App.razor`, `Pages/Error.razor` |
| Mobile startup and registration | `RealJapanese/RealJapanese.Mobile/MauiProgram.cs` |
| Mobile catalog installation | `RealJapanese/RealJapanese.Mobile/StudyDataInstaller.cs` |
| Mobile WebView host | `RealJapanese/RealJapanese.Mobile/MainPage.xaml.cs`, `wwwroot/index.html` |
| Shared routes and navigation | `RealJapanese/RealJapanese.UI/Components/Routes.razor`, `Layout/NavMenu.razor` |
| Shared study pages | `RealJapanese/RealJapanese.UI/Components/Pages/` |
| Shared practice lifecycle | `RealJapanese/RealJapanese.UI/Components/Shared/PracticeBase.cs`, `PracticeShell.razor` |
| Local sync UI | `RealJapanese/RealJapanese.UI/Components/Pages/Sync.razor` |
| Storage paths and persistence | `RealJapanese/Repositories/RepositoryPaths.cs`, `Bases/WordDataBase.cs`, `Sync/ProgressStore.cs` |
| Snapshot validation and local transport | `RealJapanese/Repositories/Sync/ProgressSyncService.cs`, `LocalProgressTransfer.cs` |
| Serialization and conjugation | `RealJapanese/DataLoaders/JsonLoader.cs`, `JsonSaver.cs`, `Models/` |
| Storage regression entry point | `RealJapanese/StorageChecks/Program.cs` |

## Important flows

- Both hosts render routes and pages from RealJapanese.UI.
- Selector → progress category → repository → host-specific atomic `Progress.json`.
- Repository construction → one catalog load → stable in-memory ID assignment →
  independent progress load; construction does not rewrite the catalog.
- Packaged mobile catalog → app-private `Catalog/`; progress remains under the
  separate app-private `Progress/` tree.
- `/sync` → frozen snapshot → one successful framed private-network fetch →
  validated merge preview → atomic progress import with one-level recovery.
- Extraction → source vocabulary → kanji relations → local model enrichment → JSON.

[ARCHITECTURE](ARCHITECTURE.md) owns boundaries and state implications;
[CONVENTIONS](CONVENTIONS.md) owns existing component/naming patterns.
Technology commands and runtime facts live in `modules/`.

## Generated and external areas

- `bin/`, `obj/`, `.idea/`, `*.user`, `.tooling/` and `.playwright-cli/` are local
  build/IDE/tooling state. Old artifacts do not prove current validation.
- `RealJapanese.UI/wwwroot/lib/bootstrap/` is vendored browser code.
- `RealJapanese/Data/` is application input and web progress, not disposable output.
- Android `FileSystem.AppDataDirectory` content is runtime state outside the repository.
- Extraction's absolute external references are authoritative in its `.csproj`;
  do not copy machine paths into context or assume they exist on another machine.
