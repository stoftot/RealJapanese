---
name: dotnet
description: Supply .NET execution and evidence for a core workflow, using SDK build/test/run/format, Rider semantics and NetCoreDbg source debugging. Apply to actual C#/.NET projects or explicit .NET toolchain work, not technology-neutral tasks.
---

# .NET adapter

Use with `implement-change`, `debug-problem`, `validate-change` or `review-change`;
this adapter does not replace their scope, diagnostic reasoning or completion rules.
Read [DOTNET facts](../../../docs/ai/modules/DOTNET.md) and the relevant project/build
configuration. Read only needed setup details from [tooling](../../../docs/tooling.md).
Do not run placeholder targets or assume a disposable tooling probe is an app.

## SDK execution and formatting

1. Detect `dotnet`, inspect `dotnet --info`, and match the actual project SDK/TFMs
   and any `global.json`. The supplied module targets the .NET 10 SDK; that does
   not authorize retargeting an existing application or claiming it is installed
   on every host. Diagnose version/workload mismatches before source changes.
2. Select the canonical solution/project, configuration and meaningful test scope
   from module facts/configuration. Use normal CLI as the deterministic mechanism
   for restore, build, test and run. Inspect completion status and relevant output.
3. `dotnet restore`, `build`, `test`, `run`, `publish`, `clean`, `new`, `workload` and
   other SDK operations are available categories, not a checklist. Choose only
   what the task needs: scaffolding, publishing build output, cleaning or workload
   changes require a relevant reason and existing authorization. `dotnet publish`
   creates artifacts; it does not grant authority to distribute them.
4. For formatting/style verification use `dotnet format` on the selected target
   with `--verify-no-changes` where supported. For requested corrections apply
   repository `.editorconfig` and supported style/analyzer fixes, limiting scope
   when possible and inspecting the diff. Formatting does not replace build/tests
   or appropriate static analysis.
5. Launch actual entry projects only when runtime evidence is needed; confirm
   readiness and exercise the affected behavior. Return precise evidence to the
   core workflow. Stop only processes launched for the task when finished.

## Rider MCP: semantic/project information

1. Detect current Rider MCP tools and confirm the intended solution/project identity
   (and root argument where supported). A live IDE may have a different solution.
2. Use it when raw search leaves meaningful ambiguity: project dependencies, symbol
   lookup, declarations/usages, call relationships, diagnostics/inspections, or
   relevant run/build configuration. Request focused results.
3. A closed/unavailable Rider must not block ordinary CLI work. Fall back to source,
   project references, compiler diagnostics and tests; identify any semantic gap.

Do not treat Rider MCP as the source-debugger control plane. Its ordinary semantic
capabilities do not imply launch-under-debugger, attach, breakpoints, stepping or
locals inspection. Even if a future session explicitly exposes debugger tools,
do not silently substitute them for this module's NetCoreDbg workflow.

## NetCoreDbg through `netcoredbg-mcp`: source debugging

Use when runtime state remains materially uncertain. Detect the current MCP tool
schemas and engine/runtime compatibility first. The intended capability surface is:

- Launch or attach; stop/terminate or detach as actually supported.
- Add/remove/list source breakpoints; continue and pause.
- Step over, into and out.
- List threads; inspect stacks, scopes and variables; evaluate expressions.

The existing external-tool docs own exact allowlisted tool names; discover schemas
instead of inventing calls. Missing capabilities are an explicit limit.

1. Build the selected debug target and locate its actual executable/assembly and
   matching symbols/source. Select known launch arguments/environment or identify
   the exact intended process for attach.
2. Launch or attach using the exposed controller. For attach, inspect stop/detach
   semantics first: do not assume stopping a session preserves the attached process.
   If safe detach is unavailable, report the limit and prefer a task-owned launch
   when it can reproduce the issue.
3. Set targeted breakpoints at the suspected boundary (start stopped when necessary
   to avoid missing an early breakpoint), then reproduce and confirm the stop reason.
4. Inspect the relevant thread, stack frame, scope and a few variables. Keep paging/
   depth bounded. Refresh frame/scope handles after continuing or stepping.
5. Evaluate focused expressions when useful, considering that evaluation may execute
   code with side effects. Step only enough to discriminate the hypothesis; use
   continue/breakpoints for long paths. Pause and inspect thread stacks for a
   relevant running-state question if supported.
6. Remove task breakpoints and terminate task-owned processes or detach cleanly as
   supported. Verify session state; do not leave suspended processes accidentally.
7. Rerun the original scenario after a fix and hand evidence back to validation;
   one breakpoint hit does not establish regression coverage.

If the debugger is absent or incompatible, use tests, targeted logs or diagnostics
where sufficient and state unverified runtime facts. Desktop CoreCLR verification
does not establish Android runtime support; the MAUI adapter checks that boundary.

## Optional analysis / diagnostics

Never assume these tools are installed; detect commands/local manifests and
intentional configuration before use. Consult installed help for actual syntax.

- **JetBrains InspectCode:** use for deeper headless ReSharper analysis or a
  machine-readable inspection report when useful. Ordinary work uses compiler,
  tests, formatter and available IDE diagnostics without requiring it.
- **`dotnet-counters`:** inspect relevant runtime/resource counters.
- **`dotnet-trace`:** capture a bounded trace for a performance/hang investigation.
- **`dotnet-dump`:** collect/analyze a dump for crash/postmortem or hang state.

Choose diagnostics for questions poorly suited to breakpoints; identify the exact
target and capture only needed data. Prefer normal tests/build/debugger for ordinary
failures. Keep temporary outputs out of durable docs; use Headroom per root guidance
for unusually large textual output. Return observations, assumptions and remaining
limits to the calling core workflow.
