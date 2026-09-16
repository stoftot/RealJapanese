# Project Map

## Repository at a glance

The root holds AI guidance and optional tooling. The C# solution is
[`RealJapanese/RealJapanese.sln`](../../RealJapanese/RealJapanese.sln), one directory
below it. Its `MainSite` entry is a solution folder, not a sixth project.

## Main areas

| Path | Role | Relationships |
| --- | --- | --- |
| `RealJapanese/RealJapanese/` | ASP.NET Core Blazor UI and host | Calls Repositories |
| `RealJapanese/Repositories/` | Vocabulary/progress, question conversion and number generation | Depends on DataLoaders and WanaKanaSharp |
| `RealJapanese/DataLoaders/` | JSON IO, serialized models and conjugation logic | Shared by repositories and utilities |
| `RealJapanese/Data/` | Study datasets, saved progress and extracted kanji relations | Mutable application data tracked in Git |
| `RealJapanese/CheckDataForDuplicates/` | Console cleanup utility | Rewrites word IDs and corresponding progress |
| `RealJapanese/Extract kanji/` | Console extraction/enrichment utility | Uses DataLoaders and external AiLibrary projects |
| `docs/ai/`, `.agents/skills/` | Project facts and workflows | Ownership in [README](README.md) |
| `tooling/`, `.codex/` | Setup/verification scripts and MCP profile | [Tooling guide](../tooling.md) |
| `releases/` | Release outcome records | [RELEASES](RELEASES.md) |

## Projects and tests

Paths below are relative to `RealJapanese/`.

| Project | Type / TFM | Direct project dependencies | Tests |
| --- | --- | --- | --- |
| `RealJapanese/RealJapanese.csproj` | Web executable / net9.0 | Repositories | None found |
| `Repositories/Repositories.csproj` | Library / net9.0 | DataLoaders | None found |
| `DataLoaders/DataLoaders.csproj` | Library / net9.0 | None | None found |
| `CheckDataForDuplicates/CheckDataForDuplicates.csproj` | Console / net9.0 | DataLoaders | None found |
| `Extract kanji/Extract kanji.csproj` | Console / net10.0 | DataLoaders; external AiLibrary.Core and AiLibrary.LlamaServer | None found |

No unit, integration or browser test suite was found. `tooling/verify-*` and
ignored `.tooling/scratch/` fixtures verify tools, not RealJapanese.

## Entry points and investigation anchors

| Purpose | Path from repository root |
| --- | --- |
| Host startup and singleton registration | `RealJapanese/RealJapanese/Program.cs` |
| Profiles and ports | `RealJapanese/RealJapanese/Properties/launchSettings.json` |
| Document, routing and navigation | `RealJapanese/RealJapanese/Components/App.razor`, `Routes.razor`, `Layout/NavMenu.razor` |
| Study pages | `RealJapanese/RealJapanese/Components/Pages/`: Words, Verbs, Adjectives, Kanji, Numbers |
| Practice lifecycle and reusable controls | `RealJapanese/RealJapanese/Components/Shared/PracticeBase.cs`, answer/flashcard bases and `PracticeShell.razor` |
| Selection and file persistence | `RealJapanese/RealJapanese/Components/Shared/WordComponentBase.cs`, `RealJapanese/Repositories/Bases/WordDataBase.cs` |
| Question DTOs and conversions | `RealJapanese/Repositories/DTOs/`, `Exstensions/` |
| Serialization and conjugation | `RealJapanese/DataLoaders/JsonLoader.cs`, `JsonSaver.cs`, `Models/` |
| Data utility entry points | `Program.cs` in each console project; `Extract kanji/Ai.cs` |

## Important flows

- Selector → progress category → repository → `SavedData.json`.
- Practice route/query → question generation → shared answer/flashcard lifecycle.
- Repository construction → data/progress loading → ID assignment and dataset rewrite.
- Extraction → source vocabulary → kanji relations → local model enrichment → JSON.

[ARCHITECTURE](ARCHITECTURE.md) owns boundaries and state implications;
[CONVENTIONS](CONVENTIONS.md) owns existing component/naming patterns.
[DOTNET](modules/DOTNET.md) and [ASPNET](modules/ASPNET.md) own build/run facts.

## Generated and external areas

- `bin/`, `obj/`, `.idea/`, `*.user`, `.tooling/` and `.playwright-cli/` are local
  build/IDE/tooling state. Old artifacts do not prove current validation.
- `RealJapanese/RealJapanese/wwwroot/lib/bootstrap/` is vendored browser code.
- `RealJapanese/Data/` is application input/progress, not disposable output.
  Extracted files under `Kanji/FromOtherData/` also serve as utility inputs.
- Extraction's absolute external references are authoritative in its `.csproj`;
  do not copy machine paths into context or assume they exist on another machine.
