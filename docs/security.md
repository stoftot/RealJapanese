# Security and privacy overview

This document records the security boundaries that matter if RealJapanese is
shared publicly. It describes the current repository; it is not a formal audit,
penetration test, or claim that the application or its cryptography is suitable
for hostile networks.

## Deployment scopes

Two release models have different risk profiles and should be reviewed
separately:

- **Distributing the Android app or desktop/local web host** gives users code that
  primarily operates on their own device and exposes a temporary local-network
  sync listener only when they choose to share. Package provenance, signing,
  updates, local data handling, and dependency integrity are the main release
  concerns.
- **Operating the web host as an internet-accessible service** creates a
  multi-user server and public network boundary that the current architecture
  was not designed for. Authentication, authorization, tenant isolation,
  encrypted transport, abuse controls, server hardening, monitoring, retention,
  and incident response would need deliberate design before such a deployment.

Publishing source code or downloadable builds does not by itself make the web
host safe to expose to the internet.

## Current boundaries and protections

Study catalogs and progress are local JSON data. The Android host places them
under the app's private data directory and declares `android:allowBackup="false"`.
The web host uses configurable filesystem roots and registers its repositories
as process-wide singletons. There are no accounts, cloud storage, analytics, or
background synchronization in the current application architecture.

Progress writes use a temporary file and replacement, serialize in-process
changes, check the stored revision before committing, and retain one recovery
snapshot for imports. Incoming sync data is size-bounded, parsed into a fixed
schema, checked against the five expected datasets, catalog hashes, and known
IDs, and shown as a local merge/replace preview before the user applies it.

Local sync is an explicit, short-lived action restricted to literal private IPv4
or loopback addresses. `RJLAN003` commits both peers to fresh ephemeral P-256
keys, shows the same six-digit comparison on both devices, requires confirmation
at both ends, and authenticates directional records with HMAC-SHA256. Attempts,
frame sizes, discovery messages, names, results, and time windows are bounded.
Authenticated sequence numbers reject record reordering and replay within the
protocol's model.

The transferred progress remains plaintext on the network. UDP discovery labels
and endpoints are untrusted hints, and discovery never authorizes transfer. The
sync service exposes a fixed progress exchange rather than arbitrary file paths,
commands, persistent trust, or remote imports. The protocol, exact limits, and
failure behavior are owned by [the local sync protocol specification](local-sync-protocol.md).

Repository checks cover persistence conflicts, recovery, malformed and oversized
sync input, approval gating, commitment and record tampering, replay, and
discovery parsing. Browser/device checks have exercised current desktop/Android
interoperation. These checks are useful regression evidence, not independent
security validation.

## Decisions and assumptions

- Sync is temporary and user-initiated. No persistent device trust or background
  listener is needed for the current two-open-app workflow.
- The current requirement prioritizes detecting unwanted changes over hiding
  study progress. Records are authenticated but intentionally unencrypted. Revisit
  this choice before adding credentials, account information, or other sensitive
  content to a snapshot; pairing keys and application secrets are not snapshot data.
- Automatic discovery and manual address entry use the same pairing protocol.
  Finding or naming a device does not establish trust; users compare both screens.
- Receiving a copy does not authorize saving it. Validation and an explicit local
  apply action are mandatory; closing the review discards it. Recovery helps undo
  an unwanted import but is not a substitute for validation or a full backup.

These are product boundaries, not assurances that custom cryptography is free of
defects. The protocol needs independent review before relying on it in a broader
or more hostile deployment.

## Known limitations

- A network observer can read synced study progress. The pairing exchange gives
  integrity and peer comparison, not confidentiality.
- The six-digit comparison has finite guessing risk. Five attempts per sharing
  session limit online guesses but do not make the code equivalent to a long
  secret. Users must compare both screens accurately; inattentive confirmation
  remains a social and usability risk.
- A local peer can spoof or flood discovery, occupy a pairing attempt, exhaust
  the attempt limit, delay traffic, or otherwise deny service. Pairing does not
  promise availability.
- Bounds and strict parsing reduce the input surface but cannot exclude defects
  in the application, serializers, networking stack, cryptographic framework, or
  platform runtime.
- Private-address checks narrow reachability; they are not an identity or trust
  boundary. Guest Wi-Fi, compromised local devices, VPNs, forwarding, and unusual
  network configuration may change who can reach the listener.
- Progress reveals learning history and may reveal relative performance or study
  habits. It should be treated as personal data even though it contains no
  account profile. A future social, comparison, sharing, or telemetry feature
  would require a fresh privacy model and clear user controls.
- Android app-private storage and disabled platform backup reduce ordinary data
  exposure but do not protect a compromised/rooted device, debug extraction,
  screenshots, memory inspection, or data a user exports or transfers.
- The web process currently shares singleton repository and progress state among
  all connected Blazor sessions. Multiple remote users would therefore share the
  same data and actions rather than receive isolated accounts or tenants.
- `UseHttpsRedirection` and production HSTS do not supply certificates, trusted
  proxy configuration, authentication, or end-to-end protection by themselves.
  Serving the control plane beyond loopback without correctly managed HTTPS could
  expose or allow modification of UI actions and progress traffic.

## Review before a public release

For downloadable/local distribution, establish reproducible release builds,
protect signing keys, document the official download and update path, and define
how compromised releases or keys are revoked. Review package identifiers,
release configuration, debug capabilities, permissions, logs, crash reporting,
and whether any build artifact contains local paths, credentials, tokens, or
private datasets. Scan and update NuGet, .NET, MAUI, WebView, JavaScript, and other
shipped dependencies, and preserve a way to notify users about security fixes.

Define a privacy and data-handling policy that matches actual behavior: what is
stored, where it is stored, what local sync reveals, how users delete or recover
progress, and what uninstall or upgrade does. Recheck filesystem permissions and
backup behavior on supported Android versions and release builds. Validate the
signed artifact on representative devices and networks, including hostile or
noisy LAN input, interrupted transfers, stale previews, low storage, and upgrades.

Before operating any internet-hosted web service, redesign repository lifetimes
and storage for per-user isolation. Add an explicit identity and authorization
model, CSRF/session review, rate and resource limits, secure secret management,
TLS termination with trusted-proxy rules, safe operational logging, data
retention/deletion, backups, monitoring, vulnerability response, and deployment
hardening. Review every endpoint and Interactive Server circuit for cross-user
state and denial-of-service behavior. The local sync TCP and UDP services should
remain disabled or separately threat-modeled on an internet-facing host.

Commission focused code review and security testing for the actual release
artifact and deployment configuration. Revisit the threat model whenever data,
accounts, networking, sharing, analytics, or hosting boundaries change. The
[architecture](ai/ARCHITECTURE.md) owns current component and state boundaries;
[decisions](ai/DECISIONS.md) owns the rationale for local authenticated sync.
