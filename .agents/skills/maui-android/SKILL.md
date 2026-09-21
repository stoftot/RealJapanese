---
name: maui-android
description: Build, deploy and investigate MAUI Android apps with device discovery, adb logs/process/package evidence and compatible C# source debugging; optionally inspect UI via configured DevFlow. Use for Android-specific behavior or explicit MAUI toolchain work, not ordinary shared .NET logic alone.
---

# MAUI / Android adapter

Use with the relevant core workflow and [.NET adapter](../dotnet/SKILL.md). Read
[MAUI_ANDROID facts](../../../docs/ai/modules/MAUI_ANDROID.md) for projects, TFMs,
package IDs, build/run targets, deployment and device assumptions. Do not invent
identifiers or use prior scratch probes as application projects.

Support `plan-tests` and `create-tests` with execution mechanics: run normal .NET
automated checks for shared logic through the .NET adapter, and use the device
workflow below for cases selected by `validate-change`. Reuse durable manual
device/UI cases when stable automation is impractical; report human steps still
required. Test design and manual-case selection remain in the core skills.

## Keep tool responsibilities separate

| Mechanism | Responsibility |
| --- | --- |
| `dotnet` / MAUI workload | Build/restore using actual project targets |
| Android SDK / `adb` | Device, package, process, log and platform operations |
| `netcoredbg-mcp` | C# source debugger, subject to actual runtime compatibility |
| Rider MCP / MAUI integration | IDE semantics, project/configuration information |
| DevFlow, if enabled | Running MAUI application/UI interaction |
| Android Emulator, if installed | Optional target; a physical device also works |

OpenJDK 21 is build infrastructure. Interact with Java/JDK tooling directly only
to diagnose toolchain configuration; its presence is not a reason to add Java code.

## Build and discover targets

1. Detect the SDK, installed MAUI Android workload, JDK and Android SDK configuration.
   Read installed help and actual target configuration as needed. Use the existing
   [environment helper](../../../tooling/enter-env.ps1) and
   [tooling guide](../../../docs/tooling.md) for local path/setup details.
2. Build the canonical Android project/TFM/configuration via normal .NET CLI. Check
   output/exit status and actual deployment artifact. A successful build establishes
   compilation; it says nothing yet about device execution.
3. Before every device-dependent workflow, run `adb devices` (use `-l` for details
   when helpful). Inspect authorization/state. Prefer an existing connected physical
   or emulated target. Do not assume one exists from an old report.
4. With multiple targets, select by explicit user preference or documented criteria;
   otherwise resolve the ambiguity before deployment. Use the chosen serial with
   `adb -s` on subsequent target-specific operations. Report offline/unauthorized
   targets as unavailable until resolved.
5. An emulator is optional. If installed and an appropriate AVD exists, it may be
   started when needed and authorized; confirm it becomes ready in `adb devices`.
   Do not install an emulator or system image merely to avoid reporting a gap.

## Deploy and collect platform/runtime evidence

1. Identify the actual package/application ID and entry activity from project,
   manifest and built artifact. Use the documented build/run deployment target or
   appropriate SDK/adb installation method; inspect command results.
2. Consider existing app data before reinstalling/resetting/uninstalling. Do not
   clear data as a speculative first fix. Package/process control must target the
   intended app and support the authorized reproduction.
3. Launch through the actual project/platform entry and verify the expected package
   and process are running. Reproduce the affected behavior on the selected target.
4. Gather bounded `adb logcat` output, preferably scoped by package process, tags,
   priority or time interval. Preserve the relevant exception/context instead of
   clearing the entire log. Inspect process/package/device state with supported
   `adb shell` operations when it answers the current hypothesis.
5. Use file transfer, port forwarding/reverse, package/process control, or connection/
   reconnection operations only when the scenario requires them. Record task-owned
   changes transiently so they can be undone; do not disrupt other targets.
6. Feed platform evidence into `debug-problem`. Separate Android lifecycle/network/
   permission/deployment issues from C# state or UI behavior before choosing a fix.

## C# source debugging and IDE information

Use the .NET adapter's NetCoreDbg build/launch-or-attach/breakpoint/inspection flow
when source-level state is uncertain. **First establish compatibility with the
actual Android runtime and transport.** Desktop CoreCLR acceptance does not prove
MAUI Android support; do not promise that an Android process can be attached to by
this controller. If unsupported, state that source debugging is unavailable and
use targeted tests/platform logs or request the missing compatible capability.

Rider supplies semantic/project/configuration information when useful; do not
assign debugger control to it by assumption. Do not treat either IDE build success
or device process presence as proof of a C# debug session.

## Optional DevFlow UI interaction

Detect an intentionally enabled Microsoft .NET MAUI DevFlow MCP connection and
required running-app instrumentation; do not assume installation or instrument the
app just because this skill is loaded. Inspect the exposed capabilities first.

When supported, inspect UI trees/state, identify elements, tap/click, enter text,
capture screenshots, inspect app logs, and examine selected app/runtime/network
state. Confirm expected app/UI outcomes after interaction.

DevFlow is not the C# source debugger. It does not substitute for source breakpoints,
C# stepping, stack-frame inspection, arbitrary local-variable inspection, or
debugger expression evaluation. Missing DevFlow does not prevent build/adb work;
report any UI verification that could not be performed.

## Complete the evidence handoff

After a fix, rerun the original device scenario plus relevant regression tests
through `validate-change`. Report build outcome, selected target/runtime, deployment,
launch, actual reproduced/verified behavior and any unperformed checks separately.
With no usable target, perform meaningful build/shared-logic checks and state
device/runtime verification unavailable; do not label the behavior verified.

Detach/stop task-owned debug sessions using supported semantics, remove task-owned
forwarding, and clean temporary fixtures/processes. Preserve unrelated app data and
user-owned devices/emulators/sessions. Return evidence to the core workflow for
context and release assessment.
