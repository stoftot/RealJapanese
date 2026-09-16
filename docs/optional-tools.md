# Optional modules

These modules have no entries in the default MCP configuration or setup script.
Resolve current vendor releases when opting in; record the actual versions then.

## Diagnostics

One independent module: `dotnet-trace`, `dotnet-dump`, `dotnet-counters` for tracing,
postmortem analysis and runtime counters. Install only when explicitly selected:

```powershell
dotnet tool install dotnet-trace --tool-path .tooling/diagnostics
dotnet tool install dotnet-dump --tool-path .tooling/diagnostics
dotnet tool install dotnet-counters --tool-path .tooling/diagnostics
```

Use each executable's `--help`; validate against a scratch process. Update with
`dotnet tool update <name> --tool-path .tooling/diagnostics`. Uninstall with
`dotnet tool uninstall <name> --tool-path .tooling/diagnostics`. No MCP is required.
Sources: [trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace),
[dump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump),
[counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters).

## Headless static analysis

[InspectCode](https://www.jetbrains.com/help/resharper/InspectCode.html) is optional
because Rider and compiler analysis already cover ordinary development.

```powershell
dotnet tool install JetBrains.ReSharper.GlobalTools --tool-path .tooling/inspectcode
.tooling/inspectcode/jb.exe inspectcode YourSolution.sln --output=results.sarif
```

This vendor package also contains other commands; use InspectCode only, and keep
`dotnet format` as the formatter. Update/uninstall using `dotnet tool` with the same
tool path. No MCP registration or dedicated security tooling is added.

## MAUI DevFlow (experimental)

Follow the current [Microsoft DevFlow documentation](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/devflow/mcp-server?view=net-maui-10.0)
and [official source](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow).
Enabling it may require application instrumentation, which is outside this task.
Create its own module fragment, discover actual tools, allowlist only the UI/tree,
screenshot or logging calls actually needed, set output caps, then verify on a
device. It does not replace source debugging with NetCoreDbg. Remove its MCP entry
before uninstalling its CLI and any explicitly added application instrumentation.

## Android emulator

An emulator or system image is not required by the base module. A physical device and adb are sufficient
for the base module. To opt in, use the SDK manager to install `emulator` and a
compatible `system-images;android-36;google_apis;x86_64` image, accept its license,
create an AVD with `avdmanager`, and verify hardware acceleration and `adb devices`.
Use `sdkmanager --help`, `avdmanager --help`, and `emulator -help` for installed-version
syntax. Google's current Android CLI documents Windows emulator-command limitations,
so use the emulator executable directly where necessary.
[Official emulator documentation](https://developer.android.com/studio/run/emulator-commandline).
Remove the AVD, then uninstall its system-image and emulator packages if unused.

## Rejected additions

Do not add OpenHands, Prompt Master, Playwright MCP, Android Studio, Selenium,
Appium, dotnet-monitor, dedicated security suites, another formatter/browser stack,
or another debugger. No Headroom proxy/wrapper/interception/learning, Playwright
agent skills, Firefox, or WebKit are enabled by this template.

Existing user-wide Codex plugins and IDE-bundled facilities belong to the user;
they are not silently uninstalled by this template. See the verification report
for the checks to perform on the target machine.
