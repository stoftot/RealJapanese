# RealJapanese

A .NET 10 Blazor web and Android MAUI Hybrid application for Japanese vocabulary,
verb/adjective conjugation, kanji meanings and number practice. Study data and
progress are stored locally; two open installations can explicitly reconcile
progress over a trusted local network.

Local transfer uses a short-lived, unencrypted connection addressed by IP and port.
Use it only on a network you trust and review the validated import preview before
applying changes.

Start with the [project map](docs/ai/PROJECT_MAP.md) for code navigation and the
[project brief](docs/ai/PROJECT_BRIEF.md) for implemented scope and unknowns.
Upcoming changes are listed in the [0.1.0 release notes](releases/0.1.0.md).

## Build and run

All projects target .NET 10. From the repository root:

```powershell
dotnet restore RealJapanese/RealJapanese/RealJapanese.csproj
dotnet build RealJapanese/RealJapanese/RealJapanese.csproj --no-restore
dotnet run --project RealJapanese/RealJapanese/RealJapanese.csproj --no-build --launch-profile http
```

The HTTP profile uses `http://localhost:5287`. The app expects
`RealJapanese/Data/` beside the web project and needs write access for progress.
See the [local sync guide](docs/local-sync.md) to transfer progress and
[ASP.NET facts](docs/ai/modules/ASPNET.md) before runtime validation.

[.NET facts](docs/ai/modules/DOTNET.md) document solution targets and the extraction
utility's external dependencies. `StorageChecks` is the dependency-free executable
regression suite for persistence and local transfer.

## Development guidance and optional tools

[AGENTS.md](AGENTS.md) and the [AI development guide](docs/ai/README.md) define
workflows and context ownership. [RELEASES](docs/ai/RELEASES.md) owns release policy.

The retained tooling template supports core MCP tools, .NET/Rider/debugging,
Playwright/Chromium and MAUI/Android workflows.

Tool installation, configuration, relocation and removal remain in the
[tooling guide](docs/tooling.md), [verification procedure](docs/verification.md)
and [optional modules](docs/optional-tools.md). Inspect existing installations and
select needed modules before setup. Tool acceptance and application validation
are separate checks.
