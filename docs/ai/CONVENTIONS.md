# Conventions

These are observed repository patterns; they do not establish undocumented
author intent. Compiler/package configuration stays in project manifests.

## Organization and components

- The solution sits under `RealJapanese/`; the web project is the nested
  `RealJapanese/RealJapanese/` directory.
- Razor pages are grouped by study domain under `Components/Pages/`. Reusable
  controls and practice bases live under `Components/Shared/`.
- Markup typically uses `@inherits XxxBase` with logic in adjacent `.razor.cs`.
  Some types are partial; do not assume every code-behind is a generated partial.
- Shared controls use `[Parameter]` and `EventCallback`; pages inject typed data
  repositories. Some components/layouts use collocated `.razor.css`.
- Models/conjugation live in DataLoaders; persistence and question transformations
  live in Repositories. See [ARCHITECTURE](ARCHITECTURE.md) for boundaries.

## Existing names and data contracts

- Names such as `Exstensions`, `SingleAnwserBase`, `PraticeSelector`, and
  `Singel` are present in source or persisted paths. Preserve the actual spelling
  when navigating or referencing them; initialization is not a rename operation.
- JSON field names are defined with `JsonPropertyName` on domain records.
  `Word.Id` defaults to `-1` and uses `StringToIntConverter`.
- Per-dataset progress is `SavedData.json` with known, rehearsing and training ID
  collections. IDs are references into vocabulary, not arbitrary display values.
- Category query values come from `WordPracticeCategoryExtensions`, rather than
  separate page-specific strings. Parsing rejects missing/unknown categories.
- Repository extension methods build question DTOs; shared practice bases own
  normalization, chunking, shuffling and retries instead of individual page markup.
- JSON saving preserves readable Japanese text. Data files are mutable tracked
  inputs; preserve unrelated vocabulary/progress when validating changes.

## Validation and mechanical configuration

All five projects enable nullable reference types and implicit usings. No
`.editorconfig`, dedicated analyzer configuration, test project or CI pipeline was
found. Avoid introducing a repository-wide formatting policy from incidental
whitespace patterns.

Use the [validation skill](../../.agents/skills/validate-change/SKILL.md) and
[.NET facts](modules/DOTNET.md) for proportionate checks. Browser behavior requires
the real app and [ASP.NET](modules/ASPNET.md) prerequisites. Utility execution and
data-backed page loads may mutate files; use disposable data for such checks.
Development-tool acceptance fixtures do not cover application behavior.
