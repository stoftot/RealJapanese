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
The observer is detached when the native WebView handler changes.

## Packaged data and private state

The project packages exactly five catalog inputs: words, verbs, adjectives, single
kanji and combined kanji. It does not package any `SavedData.json` file.

At startup, `StudyDataInstaller` replaces catalog copies under
`FileSystem.AppDataDirectory/Catalog`. Repositories save under the independent
`FileSystem.AppDataDirectory/Progress` tree. Reinstalling/upgrading the same package
with the same signing identity normally preserves this private data; uninstalling
the app clears it. There is no account or cloud backup; progress can be explicitly
transferred to another open installation over a trusted local network.
The manifest disables Android backup with `android:allowBackup="false"`.

## Build, install and validation status

The machine currently has .NET 10, the `maui-android` workload 10.0.20 and an
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
Local sync checks also cover phone-to-web and web-to-phone transfers over Wi-Fi,
preview/conflict resolution, persisted matching selections, and recovery after
restarting the temporary Android app. Broader practice interaction remains outside
these checks. The user's existing installation and private progress were preserved.

Use the [development guide](../../development.md) for beginner commands and the
[MAUI Android skill](../../../.agents/skills/maui-android/SKILL.md) for evidence-led
build/deploy/debugging. A successful build is weaker evidence than installing and
exercising the app on an API-24-or-newer device or emulator.

Debug APKs use development signing and are suitable for local testing, not store
release. Installing an update with `adb install -r` preserves app data only when
the package ID and signer match. A signer mismatch requires uninstalling the old
package, which also removes local progress.
