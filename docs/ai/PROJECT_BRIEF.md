# Project Brief

## Project summary

RealJapanese is a Japanese study application delivered through two local hosts:
an ASP.NET Core Blazor web application and an Android-only .NET MAUI Blazor Hybrid
application. Both hosts reuse the same Razor UI, study models and repositories.
Vocabulary catalogs and study progress are stored as local JSON files.

## Users, goals and capabilities

The apparent user is a Japanese learner practicing against a curated vocabulary
collection. This audience is inferred from the UI; a separate product specification
or owner-approved roadmap was not found.

- Select known, training or rehearsing vocabulary and maintain those collections.
- Practice spelling and flashcards, verb/adjective categories and conjugation,
  single/combined kanji meanings, and generated number questions.
- Reveal answers and repeat difficult questions during a practice session.
- Use the Android application offline without running the ASP.NET host.
- Transfer progress directly between two open installations on the same trusted
  Wi-Fi network or personal hotspot, with an explicit merge preview.
- Maintain datasets with separate duplicate-cleanup and AI-assisted kanji utilities.

## Scope and constraints

- The web host uses Blazor Interactive Server and therefore needs its local ASP.NET
  process while in use. It does not require internet access.
- The Android host renders the shared Razor UI inside a local `BlazorWebView`; it
  has no backend-server or internet dependency for study features.
- Web and Android progress remain separate at rest. There are no accounts or cloud
  backup; users can explicitly transfer progress over a trusted local network.
- The Android application supports API level 24 and later and uses package ID
  `com.realjapanese.mobile`.
- The canonical catalog remains under `RealJapanese/Data/`. The Android package
  embeds the five study catalogs and copies them to app-private storage.
- The extraction utility separately needs external AI-library projects and a local
  model/server. It is not part of either application runtime.
- There is no CI pipeline or automated browser/device suite. `StorageChecks` is a
  dependency-free regression executable for catalog and progress persistence.

## Enabled technology modules

| Module | Enabled? | Evidence and facts |
| --- | --- | --- |
| .NET | Yes | All projects target .NET 10; [DOTNET](modules/DOTNET.md) |
| ASP.NET | Yes | Web SDK, Interactive Server host and shared Razor UI; [ASPNET](modules/ASPNET.md) |
| MAUI/Android | Yes | Android-only MAUI Blazor Hybrid host; [MAUI_ANDROID](modules/MAUI_ANDROID.md) |

Setup and day-to-day commands are in the [development guide](../development.md).
Tooling installation and relocation remain owned by [tooling](../tooling.md).

## Unknowns requiring future owner input

- Dataset provenance/licensing and expected backup policy for saved progress.
- Whether accounts, cloud backup or unattended synchronization are future goals.
- Confirmed released baseline and intended next product version; see
  [RELEASES](RELEASES.md).
- Portable setup for the extraction utility's external dependencies.
