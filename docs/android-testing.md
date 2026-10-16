# Shared Android testing for RealJapanese

The repository opts into the centrally installed AndroidTesting environment through
[AGENTS.md](../AGENTS.md), which owns the single reviewed-revision marker. This
guide owns discovery, runner usage and the remaining integration work. The
[MAUI module](ai/modules/MAUI_ANDROID.md) owns application facts; the external
README owns the shared tool's commands and contracts.

## Discover and review without starting services

Use native Windows PowerShell; the test runner requires PowerShell 7. From the
repository root, resolve the installation without copying or installing anything:

```powershell
$androidTestingRoot = $env:ANDROID_TESTING_HOME
if ([string]::IsNullOrWhiteSpace($androidTestingRoot)) {
    $androidTestingRoot = [Environment]::GetEnvironmentVariable('ANDROID_TESTING_HOME', 'User')
}
if ([string]::IsNullOrWhiteSpace($androidTestingRoot)) {
    $androidTestingRoot = 'C:\Users\chris\DevTools\AndroidTesting'
}
foreach ($file in @('README.md', 'version.json', 'CHANGELOG.md', 'Get-AndroidTestingChanges.ps1', 'Invoke-AndroidTests.ps1')) {
    if (!(Test-Path -LiteralPath (Join-Path $androidTestingRoot $file) -PathType Leaf)) {
        throw "AndroidTesting is missing $file at $androidTestingRoot; report the setup gap."
    }
}
Get-Content -LiteralPath (Join-Path $androidTestingRoot 'README.md')
Get-Content -LiteralPath (Join-Path $androidTestingRoot 'version.json')
& (Join-Path $androidTestingRoot 'Get-AndroidTestingChanges.ps1')
# UNREVIEWED only: explicitly include the baseline, without inventing prior review.
& (Join-Path $androidTestingRoot 'Get-AndroidTestingChanges.ps1') -SinceRevision 0
```

An explicit but invalid environment path is an error, not permission to silently
fall back. The absolute fallback is specific to this host; another machine must
provide its own verified installation. Use absolute script invocations through
the resolved root even if the current shell has stale PATH entries. The existing
`tooling/enter-env.ps1 -Modules maui` reads SDK/JDK environment settings for builds;
it does not install AndroidTesting or opt other repositories in.

When the marker is a valid integer, pass that actual integer as `-SinceRevision`
and read the returned entries and referenced guidance. Investigate invalid/future
markers, inconsistent metadata or an unexpected installation path; never reset
or lower the record. Review useful improvements even if old commands still work.
After affected integration validation, recheck the current revision immediately
before updating the marker. Review any intervening changes first. A metadata
report, including `Status=Current`, is not a passing application test.

For guidance-only maintenance, inspect files, parse examples/configuration and use
the read-only revision checker. Do not invoke environment smoke tests, device
discovery helpers that initialize runtime state, fresh-process transcript helpers,
ADB (which can start its server), the runner, or service start/stop commands merely
to validate prose. Report external defects; never repair the central installation.

## Existing project and device selection

- The Android app is `RealJapanese/RealJapanese.Mobile/RealJapanese.Mobile.csproj`,
  `net10.0-android`, package `com.realjapanese.mobile`, minimum API 24. It embeds
  managed assemblies and fourteen catalogs in a standalone APK and renders the
  shared Razor UI in `BlazorWebView`.
- There is no project `android-testing.json`, registered RealJapanese project/device
  identity, or Android Appium suite. Existing xUnit/bUnit/Playwright tests cover
  desktop/shared behavior; they are not an External Android harness.
- The existing shared fallback is `MAUI_Shared_Android`: Google APIs API 36,
  x86_64, 1080 x 2392, density setting 387, VSYNC 120 Hz, 4 cores and 4096 MB RAM.
  These are configured values, not observations of a running device. The retained
  `AndroidTestingValidation` compact device belongs to the tool's validation project.
  Do not select it for RealJapanese.

Before an authorized device run, inspect current `Get-AndroidDevices.ps1`,
`Get-AndroidReservations.ps1` and `Get-AndroidResources.ps1` output through the
resolved tool root. Reuse the fallback, or devices explicitly registered to this
project if configuration is added later. Resolve the current ADB serial each run;
never assume `emulator-5554`. The runner reserves before starting/reusing devices
and Appium. Its resource checks and reservation failures must be respected; do not
bypass them with raw ADB, manual sessions, lock deletion or memory-pressure overrides.
Do not automatically register/create devices, download images, reset/delete data,
change shared presets, or install duplicate SDK/Appium/driver tooling.

An eventual `android-testing.json` should select existing `Devices` by `Profile`
and `AvdName`, `DefaultDevices`, implemented `Suites` (`Name`, `Script`, `Parameters`),
`DefaultSuites`, and `Application` (`ApkPath`, `PackageName`). Use a real registered
`ProjectId` for project-owned devices; never invent a GUID or copy the validation
project's identity. Paths are relative to the config file. A shared-only selection
needs no new project registration. Use `-DeviceProfile` and `-SuiteName` for subsets.
Do not add an active config pointing at a missing suite or relying on the default
Settings smoke suite: that does not test RealJapanese.

## Build once and run an implemented suite

Keep the existing [build helper](../tooling/build-android.ps1): its temporary ASCII
drive alias works around the Unicode checkout path and always removes that alias.
Do not replace it with the external README's generic `bin/Debug/...` example.
Build once before device concurrency; do not share concurrent writes to bin/obj.

For a future emulator run, after implementing and reviewing a Managed suite, set
`$suite` to its actual project-local path. This example is a handoff, not an
available/passing test suite. It stops if the suite or fresh build output is absent:

```powershell
if (!$suite -or !(Test-Path -LiteralPath $suite -PathType Leaf)) {
    throw 'An implemented RealJapanese suite is required before device testing.'
}
$suite = (Resolve-Path -LiteralPath $suite).Path
# Invoke in a child process so the helper's failure exit cannot be overlooked.
& pwsh -NoProfile -File .\tooling\build-android.ps1 -Configuration Debug -RuntimeIdentifier android-x64
if ($LASTEXITCODE -ne 0) { throw 'Build failed; do not deploy an older APK.' }
$apk = (Resolve-Path -LiteralPath '.tooling/android-artifacts/bin/RealJapanese.Mobile/debug_android-x64/com.realjapanese.mobile-Signed.apk' -ErrorAction Stop).Path
$runOutput = Join-Path (Get-Location).Path ('.tooling/android-tests/' + [guid]::NewGuid().ToString('N'))
& (Join-Path $androidTestingRoot 'Invoke-AndroidTests.ps1') `
    -AvdName MAUI_Shared_Android -ApkPath $apk -PackageName com.realjapanese.mobile `
    -SuiteScript $suite -SessionMode Managed -ThrottleLimit 1 -OutputDirectory $runOutput
```

Confirm the output of that successful build; the helper also lists older APKs, so
do not select the newest arbitrary file from its listing. An untargeted Debug build
uses `debug/`, while RID-targeted builds use `debug_android-x64/` or
`release_android-arm64/`. Keep the signed APK's real package ID in the invocation;
the runner resolves its launcher activity. Never hard-code the generated MAUI
activity name. On signer mismatch, stop and preserve progress; uninstalling the
existing app is not an automatic fix. Use an explicitly authorized disposable
test identity/build when needed, keeping its package ID consistent everywhere.

## Session ownership and assertions

| Mode | Suite/harness responsibility |
| --- | --- |
| `Managed` | PowerShell suite accepts `param([string]$ContextPath,[hashtable]$TestParameters=@{})`, reads the JSON and sends commands to `SessionUrl`. The worker creates and deletes its session. Do not create an AndroidDriver/second session or delete the worker's session. Throw on failed assertions; do not `exit` or leave background tests running. |
| `External` | A project harness creates its one AndroidDriver/session and disposes it in `finally`/teardown. The worker reserves devices/ports without creating a session. Explicitly consume `ANDROID_TEST_CONTEXT`, `ANDROID_SERIAL`, `APPIUM_SERVER_URL`, and context `AppiumUrl`/full `Capabilities`; clients do not consume these automatically. Disable framework parallelism within a worker. |

Both modes must use the context's exact `Serial`/`appium:udid` and allocated
`appium:systemPort`, `appium:mjpegServerPort`, `appium:chromedriverPort` and
`appium:webviewDevtoolsPort`. Do not substitute fixed ports. Native commands use
`adb -s $context.Serial` and check exit codes. Shared utilities called inside a
suite receive its `AvdName`, `Serial` and delegated `ReservationId`; they must not
try to acquire a competing reservation. Use `OutputDirectory` for worker results.
The shared server uses a loopback root URL (normally `http://127.0.0.1:4723`) and
`/appium/sessions` for discovery. Use the context URL, not `/wd/hub`.
Retain `appium:noReset=true` to preserve data and `appium:forceAppLaunch=true` to
foreground the app. Do not add `fullReset`, clear app data or uninstall as a routine
fixture step. The test plan must account for existing saved progress.

The shared `suites/DotnetTests.ps1` adapter is a future External option only after
a .NET Android harness implements the contract and is built once. Its
`Parameters.Project` resolves relative to the config directory, and it runs
`dotnet test --no-build --no-restore` with separate worker TRX output. The tool's
own documentation marks that adapter untested; our current test projects do not
implement the contract. Managed HTTP suites need no local Appium client packages.

RealJapanese needs observable assertions inside its actual WebView, beyond package
installation, process presence, an empty WebView or a Settings/counter smoke pass.
Use the existing [mobile cases](../tests/manual/mobile-lifecycle.md) and
[coverage catalog](../tests/manual/README.md) to select a bounded scenario:

- `MOBILE-001`: catalog-backed selection and practice work offline; recorded study
  assignments survive cold restart and a same-signer update. All fourteen catalogs
  must remain usable for the full case.
- `MOBILE-002`: the first native Back dismisses the IME without changing route or
  partial answer; later Back traverses history once; navigation returns, rotation
  and foreground changes keep the WebView usable, and saved progress persists.
- Select relevant Kana/study and paired-sync cases from the catalog. Shared desktop
  logic tests do not establish mobile keyboard, storage, multicast or Wi-Fi behavior.
  `MOBILE-003` remains optional until safe startup-failure injection exists.

Inspect actual accessibility/page source before choosing locators. This is a Hybrid
app, so native MAUI `AutomationId` examples are not proof of DOM access. WebView
context discovery, debugging exposure and matching Chromedriver support need
explicit project validation; allocated Chrome/WebView ports do not supply that
support. Do not auto-download drivers or instrument production code during this
guidance update. If interaction is unavailable, report the exact manual steps left.

## Diagnostics, cleanup and coverage limits

Use a new repository-local `.tooling/android-tests/<run-id>` output directory per
run. Preserve summary/result/context JSON, worker stdout/stderr, screenshots, page
source and logcat on failure, and correlate session IDs with the shared Appium log.
External harnesses must capture page source/screenshots before disposing their own
session; worker ADB diagnostics cannot recover an already-closed session's UI.
Capture unfiltered logcat after a crash (the package PID may be gone), without
clearing the device log. Record build, device/API, actual assertion and cleanup
results separately from shared environment health.

The worker/External harness closes its own session, and the coordinator releases
reservations only after verified cleanup. A crash or unknown cleanup leaves a
blocked reservation: inspect its owner/process/session and report it. Never delete
locks or force takeover. Recovery is an explicit, separately justified action
under the external README, not automatic startup/cleanup. Leave reused Appium,
emulators, data, snapshots and other projects' services intact; coordinate before
any authorized targeted stop. A test plan must explicitly authorize disposable
data changes; never reset/uninstall user data for convenience.

The shared runner/reservations support emulators only. A physical phone needs a
separate explicit-serial ADB/Appium/manual workflow with device-owner coordination,
collision-free ports for any Appium session, and an ARM64-compatible build. Do not
pass a phone serial to this runner or assume its reservations protect the phone.
Record Android API/build fingerprint/security patch, actual OEM OS version, logical
size/density, navigation mode, font/display scale, locale and relevant permissions.
The revision-2 `nothing-phone-3a` preset is a display approximation using Google APIs
Android, not Nothing OS; physical PPI is not measured logical DPI and 120 Hz VSYNC
does not prove adaptive refresh. Keep existing devices; no new preset device is
needed for this project. Real Nothing OS, OEM Back/IME/lifecycle behavior and actual
LAN/multicast conditions require physical-device evidence. The emulator also does
not establish support across the app's entire API-24-and-later range.

## Validation still required

The guidance is usable, but Android runner integration remains unverified until a
RealJapanese suite/harness implements the selected contract, can interact with the
Hybrid UI, and runs meaningful app assertions with diagnostics and clean teardown
on an existing authorized device. No active config or placeholder suite is supplied.
The root marker stays UNREVIEWED until that relevant validation completes. Existing
manual cases remain the device coverage source; application compilation, test
execution and shared-tool sample results must never be inferred from this document.
