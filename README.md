# RealJapanese

A Blazor web application for Japanese vocabulary, verb/adjective conjugation,
kanji meanings and number practice. Study data and progress are stored in local
JSON files. The repository also contains two data-maintenance console utilities.

Start with the [project map](docs/ai/PROJECT_MAP.md) for code navigation and the
[project brief](docs/ai/PROJECT_BRIEF.md) for implemented scope and unknowns.

## Build and run

The web app targets .NET 9; the full solution also includes a .NET 10 utility.
Use a compatible SDK and install the .NET 9 / ASP.NET Core 9 runtimes for web
execution. From the repository root:

```powershell
dotnet restore RealJapanese/RealJapanese/RealJapanese.csproj
dotnet build RealJapanese/RealJapanese/RealJapanese.csproj --no-restore
dotnet run --project RealJapanese/RealJapanese/RealJapanese.csproj --no-build --launch-profile http
```

The HTTP profile uses `http://localhost:5287`. The app expects
`RealJapanese/Data/` beside the web project and needs write access. Loading
repositories and changing progress can rewrite data; see
[ASP.NET facts](docs/ai/modules/ASPNET.md) before runtime validation.

[.NET facts](docs/ai/modules/DOTNET.md) document full-solution targets and the
extraction utility's external dependencies. No automated application tests were
found.

## Development guidance and optional tools

[AGENTS.md](AGENTS.md) and the [AI development guide](docs/ai/README.md) define
workflows and context ownership. [RELEASES](docs/ai/RELEASES.md) owns release policy;
a confirmed product release baseline has not been established.

The retained tooling template supports core MCP tools, .NET/Rider/debugging,
Playwright/Chromium and optional MAUI/Android. Application modules are .NET and
ASP.NET; installed Android tooling does not make this an Android application.

Tool installation, configuration, relocation and removal remain in the
[tooling guide](docs/tooling.md), [verification procedure](docs/verification.md)
and [optional modules](docs/optional-tools.md). Inspect existing installations and
select needed modules before setup. Tool acceptance and application validation
are separate checks.
