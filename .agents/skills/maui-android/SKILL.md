---
name: maui-android
description: Build, deploy and investigate MAUI Android apps using the opted-in shared AndroidTesting runner, explicit device/session ownership, app assertions and adb evidence; optionally use compatible C# debugging or configured DevFlow. Use for Android-specific behavior or toolchain work, not ordinary shared .NET logic alone.
---

# MAUI / Android adapter

Use with the relevant core workflow and [.NET adapter](../dotnet/SKILL.md). Read
[MAUI_ANDROID facts](../../../docs/ai/modules/MAUI_ANDROID.md) for projects, TFMs,
package IDs, build/run targets, deployment and device assumptions. Do not invent
identifiers or use prior scratch probes as application projects.

This repository opts into shared AndroidTesting through the root
[AGENTS.md](../../../AGENTS.md). Follow its single revision marker and the
[project Android testing guide](../../../docs/android-testing.md): resolve
`ANDROID_TESTING_HOME` or the documented host fallback, read the current external
README/version/changelog and use `Get-AndroidTestingChanges.ps1`. Review relevant
improvements even if old commands work. Never lower an invalid/future marker;
advance only after appropriate validation and a final current-revision recheck.
For documentation maintenance, use static inspection and read-only revision checks;
do not start services, run device probes or mutate shared state to validate prose.

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
| Shared AndroidTesting / Appium | Reserved emulator orchestration, allocated ports, session ownership and UI evidence through a project suite |
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
   [tooling guide](../../../docs/tooling.md) for local path/setup details. Reuse the
   central installation; report external problems instead of installing, updating,
   repairing or copying its implementation/configuration into this repository.
2. Build the canonical Android project/TFM/configuration via normal .NET CLI. Check
   output/exit status and actual deployment artifact. Preserve the project's
   `tooling/build-android.ps1` ASCII-path workaround. Build once before parallel
   device workers, selecting `android-x64` for the shared emulator or ARM64 for
   a compatible phone. Stop on build failure; never deploy an older APK. A build
   establishes compilation, not device execution.
3. Before an authorized device workflow, inspect devices, resources and reservations
   through the shared tools; use `adb devices -l` for physical-device authorization
   and state. Read any project `android-testing.json`; reuse its actual owned AVDs,
   otherwise select `MAUI_Shared_Android`. Do not borrow dedicated validation devices
   or infer a current serial from an old report.
4. With multiple targets, select by explicit user preference or documented criteria;
   otherwise resolve the ambiguity before deployment. Use the chosen serial with
   `adb -s` on subsequent target-specific operations. Report offline/unauthorized
   targets as unavailable until resolved.
5. Use PowerShell 7 `Invoke-AndroidTests.ps1` to reserve existing emulators and
   start/reuse their services for authorized tests. Never automatically create
   devices, reset/delete data, bypass a reservation or install duplicate tools.
   Physical devices need their own explicit-serial harness/manual workflow; the
   shared runner and reservations currently support emulators only.

## Run app assertions with owned sessions

Follow the guide's Managed/External contracts and configuration rules. Managed
suites use the worker's `SessionUrl` and never create/delete that session. External
harnesses read the exported context, create one session with its full capabilities
and dispose it in teardown. Use the allocated system/MJPEG/Chromedriver/WebView
ports and exact serial; disable test-framework parallelism within each worker.
Pass the delegated reservation ID to shared utilities called from a suite.

RealJapanese has no Android Appium harness yet. Implement meaningful Hybrid UI
assertions before claiming runner integration: catalog-backed practice and saved
progress, IME/Back behavior and lifecycle cases from the manual catalog. Inspect
actual UI/page source; neither a native counter sample nor allocated WebView ports
proves Blazor DOM access. Hybrid context/Chromedriver compatibility and physical
OEM behavior remain separate validation. Do not instrument the app or install
drivers merely to complete documentation maintenance.

## Deploy and collect platform/runtime evidence

1. Identify the actual package/application ID and entry activity from project,
   manifest and built artifact. Use the documented build/run deployment target or
   reserved shared runner for emulators, or the coordinated explicit-serial
   SDK/adb method for physical devices; inspect command results. Do not use raw
   deployment to bypass a busy reservation.
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
7. Preserve worker results, screenshots, page source and logcat under a new
   repository-local output directory; correlate failures with the Appium session
   log. External harnesses capture UI diagnostics before disposing their session.
   Keep unfiltered crash logcat when the process has exited; do not clear buffers.

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

Let the worker/External harness close its own session and the coordinator release
verified reservations. Report blocked cleanup; never delete locks or automatically
repair reservations. Preserve reused Appium/emulators, snapshots and user data;
coordinate before any authorized targeted service stop. Tool errors are reported
to the owner, not repaired in the external installation. Record physical-device
and OEM coverage separately; the Nothing display preset does not run Nothing OS.

After a fix, rerun the original device scenario plus relevant regression tests
through `validate-change`. Report build outcome, selected target/runtime, deployment,
launch, actual reproduced/verified behavior and any unperformed checks separately.
With no usable target, perform meaningful build/shared-logic checks and state
device/runtime verification unavailable; do not label the behavior verified.

Detach/stop task-owned debug sessions using supported semantics, remove task-owned
forwarding, and clean temporary fixtures/processes. Preserve unrelated app data and
user-owned devices/emulators/sessions. Return evidence to the core workflow for
context and release assessment.
