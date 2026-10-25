# External tooling

RealJapanese owns its MCP configuration, application scripts, tests and evidence.
MCP environments, the native debugger and reusable tooling implementations live
outside this checkout. Existing application context stays in [docs/ai](ai/README.md);
tooling migration or relocation does not initialize it again.

## Project and shared ownership

| Location | Responsibility |
| --- | --- |
| `tooling/project-tools.ps1` | Supplies this checkout's root and pins fixed `project-tools/v1` |
| `.codex/config.toml` | Effective MCP servers, endpoints, allowlists, timeouts and output caps |
| `.codex/modules/{core,dotnet,rider}.toml` | Project-owned source fragments for the generated profile |
| `tooling/build-android.ps1` | Application Android build and temporary ASCII drive alias |
| `tooling/prepare-tts.ps1`, `tooling/audio-studio/` | Application speech-library preparation and word-linked pitch profiles |
| `.tooling/` (ignored) | Application working data, scratch scripts, discovery and acceptance artifacts |

Generic setup, environment, launch, discovery and infrastructure verification
implementations are external. Keep application scripts and tests here so they can
evolve with RealJapanese. Never replace the entire `tooling/` or `.tooling/` tree.
The host layout targets Windows x64, PowerShell 7 and Python 3.11+. MCP launch also
supports `powershell.exe`, as used by this project's config.

| Environment override | Default root | Contents |
| --- | --- | --- |
| `AI_MCP_ROOT` | `E:\AI\MCP` | `headroom/venv`, `netcoredbg/venv` and pinned `installed.txt` receipts |
| `AI_SHARED_TOOLING_ROOT` | `E:\AI\Template shared tooling` | Fixed `project-tools/v1`, versioned native `netcoredbg/` and `engine-version.txt`, Playwright/Chromium and any shared Node runtime in `web/` |

Overrides must be absolute. Set them before starting Codex, shells or Rider so
child processes inherit them. Paths containing spaces are supported. The entry
point pins `project-tools/v1`; keep that profile fixed during project work.

Shared installations, profiles and receipts are read-only to project tasks. Do not
install, upgrade, repair, move, delete or overwrite them. No project command calls
host SDK/workload/plugin installers or shared-browser updates. Report missing
dependencies with their exact required paths; owner provisioning is a separate
external operation. Do not recreate copied tool environments or redirect application
Python blindly to an MCP environment. Audio workflows retain their independently
configured AiLibrary/Whisper environment and dependencies; see
[Audio Studio](audio-studio.md) and [TTS comparison](tts-comparison.md).

## Project commands

Run from the repository root in PowerShell 7:

```powershell
./tooling/project-tools.ps1 check -Modules core,dotnet,web,maui
./tooling/project-tools.ps1 configure -Modules core,dotnet,rider
. ./tooling/project-tools.ps1 enter-env
./tooling/project-tools.ps1 discover-mcp -Server headroom
./tooling/project-tools.ps1 discover-mcp -Server netcoredbg
./tooling/project-tools.ps1 playwright -CliArguments @('--help')
```

Dot-source `enter-env` to affect the current shell. Pass browser CLI arguments as
the `-CliArguments` array, including session names and paths:

```powershell
./tooling/project-tools.ps1 playwright -CliArguments @('-s=realjapanese-check', 'snapshot')
./tooling/project-tools.ps1 configure-rider -Url 'http://127.0.0.1:64342/stream'
./tooling/project-tools.ps1 configure -Modules core,dotnet,rider
./tooling/project-tools.ps1 start-rider -RiderPath 'C:\path\to\Rider' -Project '.\RealJapanese\RealJapanese.Web.sln'
```

`check` validates dependencies without provisioning. `configure` composes this
project's fragments and refuses an unmanaged config. Inspect and manually merge
unmanaged or customized configuration without losing unrelated settings; there
are no implicit TOML includes. Select `core,dotnet,rider` when regenerating this
project's current profile. The optional Android queue stanza lives in the retained
`core.toml` fragment: v1 accepts only core/dotnet/rider and loads no arbitrary
Android fragment. Preserve it when regenerating. Discovery uses the actual project config
and writes under `.tooling/`. `configure-rider` updates only the project fragment;
review its discovered allowlist before regenerating, preserving customizations
and deliberately disabled servers.

The old `setup.ps1` maps to read-only `check`, not an installation flow. The old
configure, enter-env, mcp-launch, playwright, discover-mcp, configure-rider,
prepare-verification, verify-codex-evidence, verify-config, verify-browser,
clean-verification and start-rider helpers map to corresponding entry-point tasks
(`mcp-launch` becomes `mcp -Server <name>`). Read the reusable acceptance prompt
with `verification-prompt`.
[Verification](verification.md) owns acceptance arguments and cleanup boundaries.

Codex reads trusted project config. Start at the repository root
(`codex -C <repository>`), since stdio arguments are relative to that root. Use the
normal user-level trust flow and start a fresh chat after MCP configuration or
inherited environment changes. The launcher calls
`tooling/project-tools.ps1 mcp -Server <name>` and binds NetCoreDbg's `--project`
to this checkout, including its spaces and Unicode characters.

Headroom and NetCoreDbg retain `required=true`. Temporary bootstrap `enabled=false`
is not module removal: test initialize/list_tools through each migrated project
launcher using the external Python MCP client, then re-enable only the intended,
passing server in both its fragment and effective config. Leave a failing server
disabled and report its exact failure. Optional Rider retains `required=false` and
the endpoint from Settings → Tools → MCP Server; a closed IDE does not prevent
CLI work. An interpreter executable or old discovery file does not prove startup.

The Android queue uses its installed Node.js stdio entry directly, has
`required=false` and a 90-second timeout, and needs separate MCP initialize/list
tools discovery; the shared `discover-mcp` task supports only the original three
servers. [Android testing](android-testing.md#optional-mcp-queue-and-configuration-ownership)
owns its host paths, advertised tools and queue lifecycle.

## Environment and Android integration

`enter-env` selects external MCP/browser paths and detects existing `JAVA_HOME`
and `ANDROID_HOME`. Android defaults to `%LOCALAPPDATA%/Android/Sdk`; JDK 21
discovery checks `%ProgramFiles%/Microsoft` and `%LOCALAPPDATA%/Programs/Microsoft`.
Configure Rider's SDK/JDK locations to the selected folders. `start-rider` requires
a closed IDE to inherit that environment. Restart shells/IDE after inherited
setting changes.

RealJapanese opts into shared AndroidTesting. Follow [Android testing](android-testing.md)
for `ANDROID_TESTING_HOME` or the documented host fallback, README/version/changelog
review, existing device selection, reservations and session/port ownership. The
single review marker stays in [AGENTS.md](../AGENTS.md). No Android runtime,
Appium implementation or device is copied here. Report external problems to the
owner without mutating shared tools or stopping services. Normal builds use
`tooling/build-android.ps1`, which dot-sources the entry point's `enter-env` task.
The old template Android installer/updater is retired; no replacement project task
installs workloads, updates Android packages or stops shared ADB.

## Exact MCP policy

**Headroom (3):** `headroom_compress`, `headroom_retrieve`, `headroom_stats`.
Use compression explicitly and retrieve the original when detail matters. Its
session-local expiring store is not a durable backup. Compression output is capped
at 6,000 tokens, retrieval at 12,000 and stats at 2,000. Chunk oversized originals
when complete retrieval matters. The upstream CLI can warn about an absent default
localhost proxy; this project starts no proxy, interception or learning job.

**NetCoreDbg controller:**

| Capability | Allowed tools |
| --- | --- |
| Session | `start_debug`, `attach_debug`, `stop_debug`, `terminate_debug`, `get_debug_state` |
| Source breakpoints | `add_breakpoint`, `remove_breakpoint`, `list_breakpoints` |
| Execution | `continue_execution`, `pause_execution`, `step_over`, `step_into`, `step_out` |
| Inspection | `get_threads`, `get_call_stack`, `get_scopes`, `get_variables`, `evaluate_expression` |

The launcher sets `NETCOREDBG_PATH` to the selected shared engine and passes this
project's root through `--project`. Each MCP process owns its session. Inspection
caps stay 2,000–4,000 tokens and execution/step caps 3,000. Page variables with
`start`/`count` and bound stack `levels`. Evaluation can execute target code. Normal
Codex approval policy applies; unattended acceptance uses `--approve-for-me`.
Desktop CoreCLR evidence does not establish Android debugger compatibility.

**Rider semantics:** `analyze_calls`, `build_solution_start`, `build_solution_state`,
`execute_run_configuration`, `get_file_problems`, `get_project_dependencies`,
`get_run_configurations`, `get_solution_projects`, `get_symbol_info`, `search_symbol`.
Outputs retain 4,000-token caps. Pass the intended `rootFolder` and confirm identity.
No shell, file editing, router, formatter, database, profiler or debugger tools are
exposed. Rider semantics do not establish source debugging.

## Copying or moving the template

Preserve established application context and declared dependencies. Copy source,
project config/fragments and the small entry point; omit generated `.tooling/`,
`.idea/`, `.playwright-cli/`, Python caches and build output. Preserve any needed
application working data separately. Do not copy shared code/runtimes into the
destination. On the same host external paths and the profile stay fixed; the
entry point supplies the new checkout root.

On another host, have the owner provision the selected external roots/profile and
set absolute overrides. Verify launcher interpreter paths, actual MCP startup,
NetCoreDbg project scope and browser behavior. Refresh machine-local IDE settings
and validate Rider's endpoint without assuming its port. Old launchers, receipts
or acceptance artifacts do not prove the new host works. Use the relocation
preflight in [initialize-project](../.agents/skills/initialize-project/SKILL.md)
only when adopting or relocating; migration does not repopulate existing context.

Retire copied generic scripts/local tool environments only after checking tracked
and ignored consumers and verifying replacements. Preserve application scripts,
tests, configuration, dependencies and unrelated scratch. Do not edit binary
launchers or reinstall local environments as a substitute for the external layout.

## Clean removal

Stop only task-owned browser/debugger sessions. Configure retained modules before
removing project support; preserve unrelated config. Removing project support
never uninstalls shared profiles, environments, receipts, SDK/JDK/workloads, IDE
plugins, browsers or Android devices.

`./tooling/project-tools.ps1 clean-verification` removes checked project-local
`.tooling/scratch` and `.playwright-cli` trees and refuses linked cleanup targets.
These trees can contain application work: do not run broad cleanup over it. Use
isolated disposable fixtures and clean only task-owned paths. Keep discovery and
acceptance transcripts in ignored project storage, outside durable context/releases.
