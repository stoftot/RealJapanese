# Decisions

## Shared UI with separate web and Android hosts

- **Status:** Accepted
- **Date:** 2026-09-16

### Context

RealJapanese needed an Android application that matched the existing web app,
while keeping mobile and web projects easy to open and develop independently.
Duplicating pages would make fixes and study behavior diverge. Loading the hosted
website inside the Android app would require a running server and network access.

### Decision

Keep host-specific entry projects and move reusable routes, pages, components and
static assets into the `RealJapanese.UI` Razor class library. The web host uses
Blazor Interactive Server. The Android-only host uses .NET MAUI Blazor Hybrid and
runs the shared UI locally in a `BlazorWebView`.

Provide focused Web, Mobile and Shared solutions. Retain the older aggregate for
web and data utilities, but do not add mobile to it.

### Consequences

- Study behavior and suitable UI components are shared by both hosts.
- Host startup, document/error surfaces and platform lifecycle remain separate.
- Android study features work without the ASP.NET host or internet access.
- Changes should use the smallest focused solution that contains the affected host.
- Each platform's experience takes priority over sharing presentation. Host styles
  keep web practice vertically centered and Android practice top-aligned; separate
  layouts or components should be used whenever platform needs differ.

## Separate catalog and progress roots

- **Status:** Accepted
- **Date:** 2026-09-16

### Context

The two applications need the same curated vocabulary but intentionally separate
save files. Android package assets are read-only, while study progress must be
writable and survive normal app restarts and upgrades.

### Decision

Repositories accept independent catalog and progress roots through
`RepositoryPaths`. Web defaults both roots to the existing `RealJapanese/Data/`
tree and permits separate `StudyData` configuration. Android copies only the five
canonical study catalogs into app-private `Catalog/` and saves progress under the
separate app-private `Progress/` tree.

Repository construction treats catalogs as inputs: it assigns any missing IDs
deterministically in memory and does not rewrite the catalog. Missing progress is
an empty save; stale progress IDs are ignored.

### Consequences

- Web and Android progress do not affect each other.
- Installing a newer APK can refresh packaged vocabulary without resetting progress.
- There is no account-based backup or synchronization between installations.
- Uninstalling the Android app clears its private progress.
- Dataset maintenance remains an explicit utility workflow rather than app startup.
