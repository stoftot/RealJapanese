# AI development infrastructure

This is the repository's durable context and workflow layer for AI-assisted
development, primarily with Codex, initialized for the existing RealJapanese
application. It retains the reusable template's optional tooling and workflows:
tooling provides capabilities; this layer guides their use.
Start orientation at [PROJECT_MAP](PROJECT_MAP.md). The existing
[tooling guide](../tooling.md) owns installation and machine setup.
The [development guide](../development.md) explains solution selection and normal
web, shared and Android build/run commands.

## Ownership

| Source | Owns |
| --- | --- |
| [AGENTS.md](../../AGENTS.md) | Permanent agent behavior |
| [PROJECT_BRIEF.md](PROJECT_BRIEF.md) | Purpose, scope, users, constraints |
| [PROJECT_MAP.md](PROJECT_MAP.md) | Where important things are |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Components, boundaries, flows, invariants |
| [CONVENTIONS.md](CONVENTIONS.md) | Repository-specific engineering conventions |
| [DECISIONS.md](DECISIONS.md) | Durable non-obvious choices and rationale |
| `docs/ai/modules/*.md` | Project-specific technology facts |
| `.agents/skills/*/SKILL.md` | Repeatable procedures |
| [RELEASES.md](RELEASES.md) | Release rules and format |
| [releases/](../../releases/) | Actual release outcomes |
| [development.md](../development.md) | Human setup, solution selection and normal build/run commands |

Reference authoritative documentation instead of copying it. `[TODO: ...]` means
an owner input is needed; `Unknown` means evidence is missing; `None` is an observed
absence. Replace placeholders with actual repository facts over time.

## Start using it

- **Existing application:** ask Codex to use `$initialize-project` in existing
  repository adoption mode. It inspects code/configuration and existing docs,
  populates context, and checks discovered build/test targets where feasible.
- **New application:** use `$initialize-project` in new project mode with your
  idea, intended users, main use cases, and constraints. It defines enough scope
  and architecture to start implementation, marking proposed layout as proposed.
- **Normal work:** use `$implement-change`; unclear failures use `$debug-problem`.
  `$validate-change` chooses checks; `$review-change` reports findings;
  `$maintain-project-context` keeps durable facts synchronized.

Skills have YAML `name` and `description` metadata in repository `.agents/skills`.
Codex supports explicit selection and description-based discovery; other agents
can read the same instructions directly. See [official skill documentation](https://learn.chatgpt.com/docs/build-skills).

## Copying or moving the template

`initialize-project` includes a shared local-tooling preflight for both modes and
relocation-only requests. It follows the [tooling copy/move procedure](../tooling.md#copying-or-moving-the-template)
to recreate relocated Python tool environments, refresh shell/IDE paths and check
retained capabilities. A clean source copy omits machine-generated ignored folders;
a whole-folder copy may need local repairs. Established application context is
preserved unless facts changed.

## Technology modules

| Adapter / fact file | Scope | Dependency |
| --- | --- | --- |
| `dotnet` / `DOTNET.md` | C#/.NET, formatting, Rider semantics, source debugger | None on another adapter |
| `aspnet-web` / `ASPNET.md` | ASP.NET launch and real-browser evidence | .NET adapter |
| `maui-android` / `MAUI_ANDROID.md` | MAUI build, deployment, device/runtime investigation | .NET adapter |

All three pairs are supplied; application activation is recorded in the brief.
Fill the applicable module's facts from actual project/build/launch configuration.
Keep generic procedures in its skill. Optional tools such as InspectCode,
diagnostics, DevFlow, and an emulator are detected before use and never required
by core workflows. Missing tools produce bounded fallbacks and explicit gaps.

### Remove support cleanly

1. Remove the selected `.agents/skills/<adapter>/` directory and matching
   `docs/ai/modules/<FACTS>.md` file from the table above.
2. Remove its enabled-module entry from the brief and any concrete project/map
   references that no longer apply. This README's catalog describes optional pairs;
   core procedures always test for applicability and presence.
3. ASP.NET and MAUI can each be removed independently. Removing .NET also means
   removing those dependent adapters, or first rewriting them for a different
   explicitly supported base. All six core skills remain useful on their own.
4. If removing installed tools too, follow [tooling removal](../tooling.md#clean-removal).
   Regenerate MCP configuration before deleting required servers; preserve shared
   machine installations. Removing documentation alone does not uninstall tools.

The existing external `web` installer supports standalone browser tooling; that
does not change the ASP.NET adapter's conceptual dependency on .NET. The supplied
MCP configuration requires some selected servers at startup; this is a toolchain
profile choice, not a core skill dependency. Configure only retained servers when
adopting the neutral core. Do not carry an unavailable required server into it.

## Releases and extension

[RELEASES.md](RELEASES.md) defines the initial version and the single unreleased
record. Completed, validated, version-worthy changes accumulate there; escalating
the pending version preserves notes. Publishing/tagging is outside V2.

Add a skill only for a distinct reusable procedure; extend an existing procedure
when responsibilities overlap. Give future modules one facts file and one adapter,
record applicability in the brief, and keep all core references conditional.
There is intentionally no permanent task-history or AI-observability system.
Reusable tooling acceptance procedures remain owned by the tooling docs;
ordinary work does not create session logs or turn those reports into a task journal.
