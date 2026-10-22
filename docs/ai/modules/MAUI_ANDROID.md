# MAUI / Android project facts

## Application configuration

| Fact | Value |
| --- | --- |
| Project | `RealJapanese/RealJapanese.Mobile/RealJapanese.Mobile.csproj` |
| Target | `net10.0-android`; Android only |
| App model | .NET MAUI Blazor Hybrid with local `BlazorWebView` |
| Package ID | `com.realjapanese.mobile` |
| Minimum Android | API 24 / Android 7.0 |
| Package format | APK |
| MAUI packages | 10.0.20 |
| Shared UI | RealJapanese.UI Razor class library |
| APK assembly mode | Assemblies embedded for independent sideloading |

The app executes shared Razor components locally and needs neither the ASP.NET
host nor internet access for study features. `MauiProgram.cs` registers the same
repository services as web against mobile-specific storage roots.

The Android host observes IME visibility through window insets and passes it to
the local document through `KeyboardNavigationObserver`. Mobile-only CSS hides
the bottom navigation and removes its reserved space while the keyboard is open;
closing the keyboard restores navigation even if a text input retains focus.
Kana practice also reduces its card height while the keyboard is open, keeping
the answer field and actions available in the smaller viewport.
The observer is detached when the native WebView handler changes.

`MainActivity.DispatchKeyEvent` routes a complete Back key press through MAUI once.
This prevents the BlazorWebView key-down handler and the activity Back callback
from both navigating. `MainPage` traverses WebView history when available and
otherwise leaves root Back behavior to Android. The IME receives Back before
activity key dispatch so its first dismissal preserves the practice route.

## Packaged data and private state

The project packages the fourteen catalog inputs listed by `ProgressStore.DatasetNames`:
twelve vocabulary categories plus single and combined kanji. It also packages the
reviewed Audio Studio manifest and its referenced Opus, MP3 and WAV payloads for
offline Listening practice. It does not package any `SavedData.json` file or the
legacy progress version marker.

At startup, `StudyDataInstaller` replaces catalog copies under
`FileSystem.AppDataDirectory/Catalog`. It copies immutable referenced audio
payloads under `Catalog/Audio` before atomically replacing the installed manifest;
audio is never written under progress. New audio exports become available after
the package is rebuilt and installed. Repositories save under the independent
`FileSystem.AppDataDirectory/Progress` tree. Reinstalling/upgrading the same package
with the same signing identity normally preserves this private data; uninstalling
the app clears it. There is no account or cloud backup; progress can be explicitly
transferred to another open installation over a trusted local network.
The manifest disables Android backup with `android:allowBackup="false"`.

## Build, install and validation status

RealJapanese opts into the shared AndroidTesting environment via the repository
[AGENTS.md](../../../AGENTS.md). [Android testing](../../android-testing.md) owns
portable discovery, revision review, coordinated runs, diagnostics and cleanup.
Reuse existing SDK/JDK/Appium installations; do not run the legacy Android setup
or updater to validate this integration.

No `android-testing.json`, RealJapanese device registration or Android Appium
harness currently exists. Use the existing `MAUI_Shared_Android` fallback for
future authorized emulator runs with an implemented app suite. Its configured
image is API 36 Google APIs x86_64; build `android-x64` with the repository helper.
RID-targeted Debug output is
`.tooling/android-artifacts/bin/RealJapanese.Mobile/debug_android-x64/com.realjapanese.mobile-Signed.apk`;
verify it belongs to the successful current build. The runner can reserve ports
and deploy an APK, but actual BlazorWebView interaction remains unverified.
The xUnit/bUnit/Chromium suites do not consume an Android runner context.

The shared runner supports emulators only; physical phone checks require the
explicit-serial workflow and an ARM64-compatible build. The shared AVD's Nothing
Phone (3a) display settings and revision-2 preset do not provide Nothing OS or
physical-device/OEM coverage. No additional device creation is required.

### Existing build and device evidence

Prior project validation used .NET 10, the `maui-android` workload 10.0.20 and an
Android SDK path. The canonical `tooling/build-android.ps1` Debug build passed and
produced `.tooling/android-artifacts/bin/RealJapanese.Mobile/debug/com.realjapanese.mobile-Signed.apk`.
The project embeds managed assemblies in the APK so it can be sideloaded without
.NET fast-deployment files.

An ARM64 Release build also passed, including trimming and packaging. Its APK is
under `.tooling/android-artifacts/bin/RealJapanese.Mobile/release_android-arm64/`.
This is still locally signed for development; store publishing/signing is separate.

The checkout path contains Unicode characters that the Android asset packager
cannot reliably consume. The helper temporarily maps the repository to a free
ASCII drive letter, builds there, and always removes the alias. It accepts
`-Configuration Release` and optional `-RuntimeIdentifier android-arm64` or
`android-x64`.

On-device checks have covered startup and keyboard opening/Back dismissal on an
Android phone: bottom navigation hides while the IME is visible and returns when
it closes. These checks used a temporary app identity because the existing
installation's signing certificate differed from the local development key.
Local sync checks cover `RJLAN003` phone-to-web and web-to-phone transfers over
Wi-Fi in Automatic and Manual modes: discovery, matching codes on both screens,
cancellation, preview/apply, and persisted matching selections. Device evidence
uses a temporary Debug package; Release compilation is checked separately.
Automated integration and browser tests additionally cover conflict resolution,
restart recovery, authenticated-record tampering and cross-session replay on
desktop .NET/Chromium. They do not add Android runtime evidence.
Native practice and lifecycle scenarios are defined in the
[manual regression catalog](../../../tests/manual/README.md), including keyboard,
rotation, cold restart and same-signer update preservation. Record their actual
execution and remaining gaps in the current task, not as a durable run log.

Automatic discovery uses an Android Wi-Fi multicast lock and the manifest's
`CHANGE_WIFI_MULTICAST_STATE` permission, released when finding/announcing ends.
Manual mode uses the same paired transport without needing multicast discovery.

Use the [development guide](../../development.md) for beginner commands and the
[MAUI Android skill](../../../.agents/skills/maui-android/SKILL.md) for evidence-led
build/deploy/debugging. A successful build is weaker evidence than installing and
exercising the app on an API-24-or-newer device or emulator.

Debug APKs use development signing and are suitable for local testing, not store
release. Installing an update with `adb install -r` preserves app data only when
the package ID and signer match. On a mismatch, stop and preserve progress; use
the matching signer or an explicitly authorized disposable test identity.
Uninstalling the existing package removes local progress and is not an automatic
test recovery step. Historical evidence above does not validate the shared runner.
