# .NET project facts

## Targets, solutions and dependencies

All repository projects target .NET 10. The Android project targets
`net10.0-android`; all others target `net10.0`.

| Solution | Included work |
| --- | --- |
| `RealJapanese/RealJapanese.Web.sln` | Web, UI, Repositories, DataLoaders |
| `RealJapanese/RealJapanese.Mobile.sln` | Mobile, UI, Repositories, DataLoaders |
| `RealJapanese/RealJapanese.Shared.sln` | UI, Repositories, DataLoaders, StorageChecks |
| `RealJapanese/RealJapanese.sln` | Web, UI, libraries and both data utilities; no Mobile or StorageChecks |

There is no `global.json`, central package management, custom Directory.Build file,
repository NuGet configuration or package lock file. Project manifests pin package
versions. The mobile project pins MAUI Controls and MAUI Blazor WebView to 10.0.20.
DataLoaders declares CsvHelper 33.1.0; Repositories and extraction declare
WanaKanaSharp 0.2.0.

Extraction has absolute references to external AiLibrary projects. Building the
older aggregate requires those projects; use a focused solution for ordinary app
work.

## Canonical CLI targets

From the repository root:

```powershell
# Shared libraries and storage regression check
dotnet restore RealJapanese/RealJapanese.Shared.sln
dotnet build RealJapanese/RealJapanese.Shared.sln --no-restore
dotnet run --project RealJapanese/StorageChecks/StorageChecks.csproj --no-build --no-restore

# Web application
dotnet restore RealJapanese/RealJapanese.Web.sln
dotnet build RealJapanese/RealJapanese.Web.sln --no-restore

# Android application; handles this checkout's Unicode path for Android tooling
.\tooling\build-android.ps1
```

Debug is the default unless specified. `StorageChecks` is a self-checking console
program rather than a `dotnet test` project. See the [development guide](../../development.md)
for running and installing each host.

The web and Android Debug builds have passed for the current structure. The Android
build produced `.tooling/android-artifacts/bin/RealJapanese.Mobile/debug/com.realjapanese.mobile-Signed.apk`.
Installation and device behavior remain unverified.

## Launchable projects and utilities

- Web runtime facts are in [ASPNET](ASPNET.md).
- Android build/deploy facts are in [MAUI_ANDROID](MAUI_ANDROID.md).
- `CheckDataForDuplicates` rewrites vocabulary and remaps progress. Do not run it
  against canonical data as a build check.
- Extraction requires its external projects, configured model directory and
  `LLAMA_SERVER_PATH`. Compilation alone does not validate inference/data quality.

## Current toolchain observation

The current host has .NET SDK 10.0.401, the `maui-android` workload from workload
set 10.0.400 with manifest 10.0.20, and an Android SDK path configured. These are
machine observations, not portable repository guarantees. No connected adb target
or installed Android virtual device was found during the current validation.

The repository path contains Unicode characters that cause Android `aapt2` to fail
with APT2265. `tooling/build-android.ps1` is the canonical build entry: it creates a
temporary free R–Z drive alias, builds through that ASCII path into the ignored
`.tooling/android-artifacts/` directory, and removes the alias in `finally`.

The retained [.NET skill](../../../.agents/skills/dotnet/SKILL.md) owns SDK, Rider,
debugger and optional diagnostics procedures. Detect availability on each machine.
