# Development guide

RealJapanese has separate solutions for web, Android and shared code. All commands
below run from the repository root.

## Choose a solution

| Open | Use it for |
| --- | --- |
| `RealJapanese/RealJapanese.Web.sln` | ASP.NET host and web-specific behavior |
| `RealJapanese/RealJapanese.Mobile.sln` | Android app, packaging and device behavior |
| `RealJapanese/RealJapanese.Shared.sln` | Shared Razor UI/data plus unit, integration and component tests |
| `RealJapanese.Tests.slnx` | Complete automated suite, including real-browser tests |
| `RealJapanese/RealJapanese.sln` | Existing web plus data-maintenance utilities |

The older aggregate intentionally excludes mobile. Its extraction project has
machine-specific external project references, so it is not the normal solution
for app development.

## Prerequisites

- .NET 10 SDK.
- For Android: the .NET MAUI Android workload, Android SDK, JDK 21, and either an API-24+
  physical device or emulator.
- The mobile project pins its MAUI packages to 10.0.20. Check the existing workload
  with `dotnet workload list`; report missing/incompatible dependencies rather than
  installing or upgrading the shared environment during project maintenance.

The current development machine has the .NET 10/MAUI 10.0.20 toolchain and Android
SDK. This repository opts into [shared Android testing](android-testing.md);
resolve its tool path and review its README/revision before Android work.

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

## Build shared code and run automated tests

`RealJapanese/RealJapanese.Shared.sln` builds the shared application projects and
the unit, integration and component test projects. `RealJapanese.Tests.slnx` adds
the Playwright browser project for the complete suite. See the
[automated test guide](testing.md) for setup, commands, test ownership, isolation,
parallelism and explicit known-defect cases.

## Transfer progress locally

Open **Sync progress** from Home in both running apps and follow the sharing and
receiving prompts. Both devices must be on the same trusted Wi-Fi or personal
hotspot, and both apps must remain in the foreground. See the
[local sync guide](local-sync.md) for merge choices and network limitations.

## Build and run Android

Follow [shared Android testing](android-testing.md) for tool discovery, revision
review and reserved emulator execution. It reuses `MAUI_Shared_Android` until this
project has its own saved device configuration. Build the existing app with the
helper below; no SDK/emulator/Appium installation or device creation is required.
The project still needs an Android Appium suite capable of driving its Hybrid UI.

For an authorized physical-phone test, first confirm that adb can see the target:

```powershell
. .\tooling\enter-env.ps1 -Modules maui
adb devices -l
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
For emulators, use `Invoke-AndroidTests.ps1` with an implemented app suite as shown
in the Android testing guide; do not bypass reservations with raw installation.
For a physical phone (unsupported by the shared runner), coordinate device ownership
and set `$serial` to its actual authorized serial from `adb devices -l`. Install
the successful current build, then open RealJapanese from the device launcher:

```powershell
adb -s $serial install -r ".tooling\android-artifacts\bin\RealJapanese.Mobile\debug\com.realjapanese.mobile-Signed.apk"
if ($LASTEXITCODE -ne 0) { throw 'Install failed; preserve existing app data.' }
```

Debug APKs use development signing and are for local testing. `-r` keeps private
app data when the installed app has the same package ID and signing key. Changing
machines can mean a different debug key. On a signer mismatch, stop and use the
matching signer or an explicitly authorized disposable test identity. Uninstalling
clears mobile progress and must never be an automatic test fix. There is no account
or cloud backup; preserve or explicitly transfer progress before a deliberate change.

Prior project checks passed Android Debug and ARM64 Release builds; these are not
shared-runner acceptance results. The Debug APK above includes
both ARM64 and x64 support; the Release APK is under
`.tooling/android-artifacts/bin/RealJapanese.Mobile/release_android-arm64/`.
Both use local development signing unless release keys are explicitly configured.
Device checks have covered startup, keyboard/navigation visibility, and two-way
`RJLAN003` Wi-Fi sync in both directions using Automatic discovery and Manual
address entry. Both modes were exercised with matching-code confirmation,
preview/apply and matching saved progress in a temporary test installation;
cancellation and rejected codes were also checked. Automated integration and
browser tests cover restart persistence, recovery, tampered records, replay
rejection and shared study behavior on desktop .NET/Chromium. They do not establish
Android runtime behavior. Android Release compilation is checked separately;
physical-device UI checks use the Debug test package.

## Data-maintenance utilities

`CheckDataForDuplicates` and `Extract kanji` can rewrite datasets. They are not
read-only checks. Extraction also depends on external projects and a local model
configuration outside this repository.
The duplicate-cleanup utility stops without changes if `Progress.json` exists;
it only knows how to remap the older per-dataset save files.
