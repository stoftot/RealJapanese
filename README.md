# AI-assisted development repository template

Repository-resident AI guidance and a modular external toolchain for Codex on
Windows x64. This template contains no application.

Start with the [AI development guide](docs/ai/README.md) and
[project map](docs/ai/PROJECT_MAP.md). Use `$initialize-project` to adopt an existing
application or define a new one. The [release policy](docs/ai/RELEASES.md) governs
completed, validated, version-worthy outcomes.

- **Core:** explicit Headroom compression and retrieval over MCP.
- **.NET:** .NET 10, built-in `dotnet format`, Rider MCP, NetCoreDbg and its MCP controller.
- **Web:** Microsoft Playwright CLI and Chromium.
- **MAUI Android:** workload, Microsoft OpenJDK 21, Android SDK and adb.

Start with [setup and removal](docs/tooling.md), [verification procedure](docs/verification.md),
and [optional modules](docs/optional-tools.md).

From the repository root, in PowerShell 7:

```powershell
./tooling/setup.ps1 -Modules core,dotnet,web,maui
./tooling/configure.ps1 -Modules core,dotnet,rider
. ./tooling/enter-env.ps1
codex
```

When copying or moving this template, use the
[relocation procedure](docs/tooling.md#copying-or-moving-the-template);
`$initialize-project` now checks local tooling as part of initialization.

Inspect existing installations before running setup on another machine. Installers
resolve current official releases; installed package receipts stay in ignored
`.tooling/`. Machine installers require an elevated PowerShell. Never infer a
successful setup from installation alone: follow the functional verification guide.

For a technology-neutral repository, select only `core` in both setup and configure.
The web module can be used independently of .NET and MAUI.
