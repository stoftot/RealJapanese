# Development guide

RealJapanese has separate solutions for web, Android and shared code. All commands
below run from the repository root.

## Choose a solution

| Open | Use it for |
| --- | --- |
| `RealJapanese/RealJapanese.Web.sln` | ASP.NET host and web-specific behavior |
| `RealJapanese/RealJapanese.Mobile.sln` | Android app, packaging and device behavior |
| `RealJapanese/RealJapanese.Shared.sln` | Shared Razor UI, repositories, models and storage checks |
| `RealJapanese/RealJapanese.sln` | Existing web plus data-maintenance utilities |

The older aggregate intentionally excludes mobile. Its extraction project has
machine-specific external project references, so it is not the normal solution
for app development.

## Prerequisites

- .NET 10 SDK.
- For Android: the .NET MAUI Android workload, Android SDK, JDK 21, and either an API-24+
  physical device or emulator.
- The mobile project pins its MAUI packages to 10.0.20. Check the local workload
  with `dotnet workload list`; install a missing workload with
  `dotnet workload install maui-android`.

The current development machine has the .NET 10/MAUI 10.0.20 toolchain and Android
SDK. Check phone or emulator availability with `adb devices` for each session.

## Build and run the web app

```powershell
dotnet restore RealJapanese/RealJapanese.Web.sln
dotnet build RealJapanese/RealJapanese.Web.sln --no-restore
dotnet run --project RealJapanese/RealJapanese/RealJapanese.csproj --no-build --launch-profile http
```

Open `http://localhost:5287`. Keep the terminal process running while using the
site because its shared Razor UI runs through Blazor Interactive Server. It needs
the local server, but it does not need internet access.

By default, web catalogs and progress both use `RealJapanese/Data/`. To try a
separate save without touching existing progress, point the progress root at a
different directory before starting the app:

```powershell
$env:StudyData__ProgressRoot = "C:\path\to\temporary-progress"
dotnet run --project RealJapanese/RealJapanese/RealJapanese.csproj --no-build --launch-profile http
Remove-Item Env:StudyData__ProgressRoot
```

`StudyData__CatalogRoot` overrides the catalog root in the same way. A catalog root
must contain the normal `Words`, `Verbs`, `Adjectives`, `Kanji/Singel` and
`Kanji/Combined` layout. A new progress root may be empty.

## Build shared code and run storage checks

```powershell
dotnet restore RealJapanese/RealJapanese.Shared.sln
dotnet build RealJapanese/RealJapanese.Shared.sln --no-restore
dotnet run --project RealJapanese/StorageChecks/StorageChecks.csproj --no-build --no-restore
```

`StorageChecks` reads the real catalogs, verifies their IDs and content remain
unchanged, and exercises atomic persistence, migration, sync merge/recovery and the
local framed transport in temporary directories. Success prints
`Storage checks passed.`

## Transfer progress locally

Open **Sync progress** from Home in both running apps and follow the sharing and
receiving prompts. Both devices must be on the same trusted Wi-Fi or personal
hotspot, and both apps must remain in the foreground. See the
[local sync guide](local-sync.md) for merge choices and network limitations.

## Build and run Android

First confirm that adb can see a target:

```powershell
. .\tooling\enter-env.ps1 -Modules maui
adb devices
```

For a physical phone, enable Developer options and USB debugging, connect it, and
accept the authorization prompt on the phone. An emulator must use Android API 24
or later.

Build with the repository helper:

```powershell
.\tooling\build-android.ps1
```

The repository path contains Unicode characters that cause the Android asset
packager to fail with APT2265. The helper temporarily maps the checkout to a free
R–Z drive letter, builds through that ASCII path, removes the alias afterward, and
writes outputs under `.tooling/android-artifacts/`. For a targeted Release build:

```powershell
.\tooling\build-android.ps1 -Configuration Release -RuntimeIdentifier android-arm64
```

The app is a local MAUI Blazor Hybrid application. It copies packaged vocabulary
into its private `Catalog` directory and stores progress in a separate private
`Progress` directory; the web server does not need to be running.

The Debug build creates a sideloadable APK with its managed assemblies embedded.
Install it on an authorized connected target, then open RealJapanese from the
device launcher:

```powershell
adb install -r ".tooling\android-artifacts\bin\RealJapanese.Mobile\debug\com.realjapanese.mobile-Signed.apk"
```

Debug APKs use development signing and are for local testing. `-r` keeps private
app data when the installed app has the same package ID and signing key. Changing
machines can mean a different debug key. A signer mismatch requires uninstalling
the old app first. Uninstalling clears mobile progress; there is no account or
cloud backup, so transfer progress first if it must be preserved.

Android Debug and ARM64 Release builds have passed. The Debug APK above includes
both ARM64 and x64 support; the Release APK is under
`.tooling/android-artifacts/bin/RealJapanese.Mobile/release_android-arm64/`.
Both use local development signing unless release keys are explicitly configured.
Device checks have covered startup, keyboard/navigation visibility, and two-way
`RJLAN002` Wi-Fi sync with preview/apply and matching saved progress in a temporary
test installation. StorageChecks covers restart persistence and recovery. Broader
study interaction remains outside that coverage; the final pairing-free protocol
has not been retested in an Android Release build.

## Data-maintenance utilities

`CheckDataForDuplicates` and `Extract kanji` can rewrite datasets. They are not
read-only checks. Extraction also depends on external projects and a local model
configuration outside this repository.
The duplicate-cleanup utility stops without changes if `Progress.json` exists;
it only knows how to remap the older per-dataset save files.
