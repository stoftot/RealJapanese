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
- Cross-installation progress moves only through an explicit local transfer.
- Uninstalling the Android app clears its private progress.
- Dataset maintenance remains an explicit utility workflow rather than app startup.

## Explicit authenticated sync on a trusted local network

- **Status:** Accepted
- **Date:** 2026-09-16

### Context

Web and Android installations keep separate local progress, but users need a
simple way to reconcile them without accounts, cloud storage or a permanent server.
The transfer must not expose file paths or silently accept incompatible catalogs.

### Decision

Provide a shared `/sync` page that transfers a frozen progress snapshot directly
between two open app instances over RFC1918 private IPv4. A temporary listener uses
a random port and a one-use 128-bit displayed code; SHA-256 derives the AES-GCM key
for authenticated protocol frames. Sessions last at most five minutes and end on
success, cancellation or leaving the page.

The receiver validates the version, five-dataset shape, IDs, 4 MiB bound and raw
catalog SHA-256 hashes before offering `MergeKeepLocal`, `MergeUseIncoming` or
`Replace`. Apply uses the same atomic progress store and retains the pre-import
state as one-level recovery.

### Consequences

- Sync requires both apps in the foreground on the same trusted Wi-Fi or personal hotspot.
- The feature does not configure firewalls or routers; phone-to-PC sharing is the
  practical fallback when inbound PC connections are blocked.
- The pairing code protects the session from unauthenticated peers, but the feature
  does not turn an untrusted LAN into a trusted environment.
- Two-way reconciliation is receiver merge followed by sharing the result back and
  replacing the original sender.
- There is no discovery service, cloud copy, account or unattended background sync.
