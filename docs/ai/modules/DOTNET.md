# .NET project facts

## Targets and dependencies

- Solution: [RealJapanese/RealJapanese.sln](../../../RealJapanese/RealJapanese.sln),
  with Debug/Release and Any CPU configurations.
- Web, Repositories, DataLoaders and CheckDataForDuplicates target `net9.0`;
  Extract kanji targets `net10.0`.
- No `global.json`, central package management, custom Directory.Build files,
  repository NuGet configuration or package lock files were found. Full-solution
  compilation needs an SDK capable of building .NET 10 plus .NET 9 reference packs.
  Running the web app requires .NET 9 and ASP.NET Core 9 runtimes.
- Nullable reference types and implicit usings are enabled in all five projects.
- DataLoaders declares CsvHelper 33.1.0; Repositories and Extract kanji declare
  WanaKanaSharp 0.2.0. Project manifests own package versions.
- [PROJECT_MAP](../PROJECT_MAP.md) owns dependencies. Extraction references
  AiLibrary.Core and AiLibrary.LlamaServer at absolute paths outside this checkout.
  Full-solution builds need those projects and their restored dependencies.

## Canonical CLI targets

From the repository root; use the web target when extraction dependencies are
unavailable:

```powershell
dotnet restore RealJapanese/RealJapanese/RealJapanese.csproj
dotnet build RealJapanese/RealJapanese/RealJapanese.csproj --no-restore

# Includes utilities and external AI-library dependencies:
dotnet restore RealJapanese/RealJapanese.sln
dotnet build RealJapanese/RealJapanese.sln --no-restore

# Compile cleanup without executing its data rewrite:
dotnet build RealJapanese/CheckDataForDuplicates/CheckDataForDuplicates.csproj
```

Debug is default; use `-c Release` for Release checks. No automated test targets or
test frameworks were discovered. An empty `dotnet test` invocation or tooling
probe must not be counted as application coverage.

When relevant, use `dotnet format` with `--verify-no-changes` on the web project or
available solution. No `.editorconfig` or intentionally configured application
analyzer/diagnostics tool was found. Existing compiler warnings are not
initialization changes.

## Launchable projects

- Web: [ASPNET](ASPNET.md) owns profiles, working directory and writable data.
- CheckDataForDuplicates rewrites vocabulary and remaps progress IDs. Its
  `../../../../Data/` paths assume execution from the framework build-output
  directory, unlike the normal project-directory `dotnet run` default.
- Extract kanji has the same relative data-directory assumption. It requires
  external AI-library projects, the model directory specified in `Program.cs`,
  and `LLAMA_SERVER_PATH` as consumed by `Ai.cs`. Compilation alone does not
  validate inference or generated data quality.

Neither utility is a read-only validation command. Use disposable data for
runtime checks and inspect working-directory assumptions before execution.

## Optional tooling

The retained template provides SDK, Rider and NetCoreDbg procedures in the
[.NET skill](../../../.agents/skills/dotnet/SKILL.md) and
[tooling guide](../../tooling.md). Its .NET 10 profile does not retarget the .NET 9
application. Detect installed runtimes, commands, IDE endpoints and exposed MCP
tools on each machine; copied acceptance receipts do not prove current availability.
