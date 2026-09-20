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

- **Status:** Superseded by "Plain one-fetch sync on a trusted local network"
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

## Plain one-fetch sync on a trusted local network

- **Status:** Superseded by "Discovery with temporary code-confirmed integrity protection"
- **Date:** 2026-09-16

### Context

The pairing code and authenticated encrypted frames added input and coordination
to a deliberately short-lived local transfer. The user chose a simpler receiver
flow that needs only the sender's address and port, while retaining strict snapshot
validation, a reviewable preview and the prohibition on remote writes.

### Decision

Replace the pairing protocol with `RJLAN002`, a plain framed TCP protocol. The
receiver sends one fixed empty fetch request and accepts a bounded snapshot reply
of at most 4 MiB. The sender listens for at most five minutes and stops after the
first successful fetch. Both ends accept only literal RFC1918 private IPv4 or
loopback addresses.

The protocol has no encryption or sender authentication. Anyone on the LAN who
knows the displayed address and port can fetch while sharing is active, and a
network peer can observe, alter or substitute the snapshot. A trusted network,
strict schema/catalog/ID validation and the user's preview are the security
boundary. Imports remain local actions: the transport exposes no remote-write or
arbitrary-path operation.

### Consequences

- Receiving requires only the sender's IP address and port.
- Both installations must be updated together; the earlier pairing protocol and
  `RJLAN002` are incompatible.
- Sharing remains foreground-only, transient and limited to one successful fetch.
- Two-way reconciliation, merge modes, atomic import and one-level recovery are
  unchanged.
- There is no discovery service, cloud copy, account or unattended background sync.

## Discovery with temporary code-confirmed integrity protection

- **Status:** Accepted
- **Date:** 2026-09-20

### Context

The user wants nearby-device selection with manual address entry as a fallback,
and the same matching-code confirmation in both modes. Preventing undetected
changes matters; hiding the study progress from network observers does not.

### Decision

Offer an Automatic/Manual switch on the shared sync page. Automatic uses bounded
app-specific UDP multicast discovery with untrusted device labels. Both modes
establish temporary `RJLAN003` sessions, compare a six-digit code derived from a
committed ephemeral key exchange, and require approval on both screens. Derived
directional keys authenticate cleartext records and bind them to that session.
The [protocol document](../local-sync-protocol.md) owns exact framing and limits.

### Consequences

- Either host can announce/share or find/receive. Manual mode changes only endpoint
  selection; it never bypasses pairing or integrity checks.
- Pairing lasts for the current transfer, with no stored device trust or accounts.
- Contents remain readable to network observers. Correct comparison is essential;
  finite code guessing and denial of service remain limitations.
- Discovery permissions, guest isolation and PC firewall rules can affect discovery
  or connectivity; the app does not alter firewall/router settings.
- Existing snapshot validation, local preview/apply and recovery remain mandatory.
- Both installations need this protocol version; earlier transfers are incompatible.
