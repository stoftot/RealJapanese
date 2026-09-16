# Conventions

These are observed repository patterns. Compiler and package configuration remain
owned by project manifests.

## Organization and components

- Open `RealJapanese.Web.sln`, `RealJapanese.Mobile.sln` or
  `RealJapanese.Shared.sln` for focused work; the older `RealJapanese.sln` remains
  the web-and-utilities aggregate and does not contain mobile.
- Host-only startup and lifecycle code stays in `RealJapanese/` or
  `RealJapanese.Mobile/`. Reusable routes, pages, components and static web assets
  belong in the `RealJapanese.UI` Razor class library.
- Share presentation only when it suits both platforms. Platform experience takes
  priority over UI reuse; separate layouts or components are appropriate when needed.
  Host styles load after shared styles: web owns `RealJapanese/wwwroot/web.css`
  (including vertically centered practice), Android owns
  `RealJapanese.Mobile/wwwroot/mobile.css` and keeps practice top-aligned.
  Screen width alone does not identify the host.
- Razor pages are grouped by study domain under `RealJapanese.UI/Components/Pages/`.
  Reusable controls and practice bases live under `Components/Shared/`.
- Markup typically uses `@inherits XxxBase` with logic in adjacent `.razor.cs`.
  Shared controls use `[Parameter]` and `EventCallback`.
- Models/conjugation live in DataLoaders; persistence and question transformations
  live in Repositories.

## Data and naming contracts

- Names such as `Exstensions`, `SingleAnwserBase`, `PraticeSelector`, and `Singel`
  are present in source or persisted paths. Preserve their spelling when resolving
  existing contracts unless a deliberate migration covers every reference.
- JSON field names are defined with `JsonPropertyName`. `Word.Id` defaults to `-1`
  and uses `StringToIntConverter`.
- Each host supplies catalog and progress roots. Preserve the relative dataset
  layout (`Words`, `Verbs`, `Adjectives`, `Kanji/Singel`, `Kanji/Combined`) beneath
  both roots.
- Per-dataset progress is `SavedData.json` with known, rehearsing and training ID
  collections. IDs reference catalog entries.
- Android package assets contain catalogs only. Never package web `SavedData.json`.
- Category query values come from `WordPracticeCategoryExtensions`; unknown or
  missing category values are rejected.
- JSON saving preserves readable Japanese text. Preserve unrelated vocabulary and
  progress when validating changes.

## Validation and mechanical configuration

All projects enable nullable reference types and implicit usings. No `.editorconfig`,
dedicated analyzer configuration or CI pipeline was found. Avoid introducing a
repository-wide formatting policy from incidental whitespace patterns.

Use `StorageChecks` for storage changes, the real web host/browser for browser
behavior, and the MAUI Android workflow for device behavior. Compilation does not
establish that an APK installed or ran on a device. Data utilities can rewrite
canonical files and must not be used as read-only verification commands.
