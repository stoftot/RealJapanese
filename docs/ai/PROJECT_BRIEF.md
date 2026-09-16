# Project Brief

## Project summary

RealJapanese is a browser-based Japanese study application with vocabulary, verbs,
adjectives, kanji meanings and Japanese number practice. Study collections and
progress are stored in local JSON files. This is an existing application adopted
into the AI guidance template; evidence comes from the
[solution](../../RealJapanese/RealJapanese.sln), source and datasets.

## Users, goals and capabilities

The apparent user is a Japanese learner practicing against a curated vocabulary
collection. This audience is inferred from the UI; a separate product
specification or owner-approved roadmap was not found.

- Select known, training or rehearsing vocabulary and maintain those collections.
- Practice spelling and flashcards, verb/adjective categories and conjugation,
  single/combined kanji meanings, and generated number questions.
- Reveal answers and repeat difficult questions during a practice session.
- Maintain datasets with separate duplicate-cleanup and AI-assisted kanji utilities.

## Scope and constraints

- ASP.NET Core Blazor Interactive Server pages require a running server connection.
- Singleton repositories share server-side state and progress files. No accounts,
  authentication configuration or per-user storage were found.
- Data lives in `RealJapanese/Data/`, outside the web project. Relative paths and
  write access matter; see [ASP.NET facts](modules/ASPNET.md).
- No database or remote service is configured for the web app. The extraction
  utility separately needs external AI-library projects and a local model/server.
- No automated test projects, CI pipeline or deployment configuration were found.
  Some selector links have no matching page; see [ASP.NET facts](modules/ASPNET.md).

Explicit product non-goals are Unknown. Native mobile, accounts and hosted
multi-user operation are not implemented; this does not establish future exclusions.
Initialization documents the current application without changing its design.

## Enabled technology modules

| Module | Enabled? | Evidence and facts |
| --- | --- | --- |
| .NET | Yes | Five C# projects; four net9.0, extraction net10.0; [DOTNET](modules/DOTNET.md) |
| ASP.NET | Yes | Web SDK, Razor components and Interactive Server startup; [ASPNET](modules/ASPNET.md) |
| MAUI/Android | No | No MAUI project, Android TFM or application manifest; [MAUI_ANDROID](modules/MAUI_ANDROID.md) |

The supplied tooling profile retains core, .NET, Rider, web and Android support.
Installed workloads and scratch probes do not activate an application module.
Setup and relocation procedures remain owned by [tooling](../tooling.md).

## Unknowns requiring future owner input

- Deployment environment and whether multiple independent learners are a goal.
- Dataset provenance/licensing and expected backup policy for saved progress.
- Confirmed released baseline and intended next product version; see [RELEASES](RELEASES.md).
- Portable setup for the extraction utility's external dependencies.
