# Local sync protocol

The [sync guide](local-sync.md) describes the user flow. This document specifies
the current implementation in `Repositories/Sync/`, not a claim of an independent
security audit. `RJLAN003` replaces the incompatible earlier transfer protocols.

## Discovery and trust

Automatic mode sends up to three UDP queries over three seconds to
`239.255.77.77:47777` with multicast TTL 1. This is an app-specific protocol, not
mDNS. `RJDISC01` queries carry a fresh 16-byte nonce. Announcers echo that nonce in
unicast replies to the query's source endpoint, with a random advertisement ID,
bounded display name, and TCP port. The finder takes the address from the reply's
source and deduplicates advertisements, preferring a non-loopback address.

Names, addresses, ports and advertisement IDs are untrusted hints. Discovery
does not grant trust or authorize a transfer. Manual mode enters the endpoint
directly and uses exactly the same pairing protocol. Android holds a multicast
lock only while finding or announcing; web performs discovery in its .NET host.

## Pairing handshake

Every TCP frame starts with the eight ASCII bytes `RJLAN003`, a one-byte type,
and a four-byte signed big-endian payload length. Negative or excessive lengths,
wrong types, old versions and incomplete messages fail the connection.

The receiver is the initiator; the sharing device is the responder. Each creates
a fresh P-256 ECDH key and 32 random bytes. An opening is exactly 123 bytes:
the random bytes followed by a canonical 91-byte DER SubjectPublicKeyInfo.
Only named P-256 keys with canonical re-export equality are accepted.

1. Initiator sends type 1: its 32-byte commitment.
2. Responder sends type 2: its 32-byte commitment.
3. Initiator sends type 3: its opening.
4. Responder verifies the initiator's commitment and sends type 4: its opening.
5. Initiator verifies the responder's commitment.

A commitment is SHA-256 of `RJLAN003 || role || opening`, where the one-byte
role is 1 for initiator or 2 for responder. Both commitments precede either
opening. This ordering and the random values prevent adapting keys after seeing
the peer's opening to search for a matching short comparison code.

The transcript is `RJLAN003 || initiator opening || responder opening`.
All fields have fixed lengths. The explicit .NET `DeriveKeyFromHash(SHA256)` ECDH
result feeds HKDF-SHA256, with SHA-256 of that transcript as salt. Separate info
labels derive:

- `RJLAN003 client records`: 32-byte initiator-to-responder HMAC key.
- `RJLAN003 server records`: 32-byte responder-to-initiator HMAC key.
- `RJLAN003 comparison`: eight bytes, interpreted as an unsigned big-endian
  integer modulo 1,000,000, displayed as six zero-padded digits in two groups.

Users must compare the two physical screens and confirm on both. The displayed
code is not the secret key and is not accepted as a network credential. A short
comparison has a finite guessing risk (approximately one in a million per
independent active attempt); a share permits at most five connection attempts.
Confirmation handlers refer to the exact displayed pairing instance, so a late
click cannot approve a replacement request.

## Authenticated records

Record payloads are `sequence || body || tag`: an eight-byte unsigned big-endian
sequence starting at zero independently in each direction, the body, and a
32-byte HMAC-SHA256 tag. The MAC covers the complete 13-byte frame header plus
sequence and body. Directional, transcript-derived keys bind each record to the
current handshake. Exact next-sequence checks and fixed-time tag comparisons
reject altered, reordered, reflected and cross-session replayed records.

- Type 5: initiator's decision; one-byte body 1 for match, 0 for rejection.
- Type 6: responder's decision; same encoding.
- Type 7: responder's snapshot, at most 4 MiB, sent only after both approvals.
- Type 8: initiator's receipt; one-byte body 1, after snapshot MAC verification.

The snapshot stays in cleartext. Pairing protects authenticity and integrity,
not confidentiality or availability. A checksum alone would not provide that
protection. The receiver verifies the record before schema/catalog validation
and preview; importing remains a separate local action.

## Lifetime and failure behavior

Sharing expires after five minutes. A handshake has ten seconds, code comparison
has ninety seconds, and the post-confirmation transfer has fifteen seconds.
Rejection, cancellation, expiry and page disposal close the connection. Ephemeral
key objects are disposed and derived secret byte arrays are cleared on completion.
One pairing is processed at a time; queued requests cannot replace its code.

Once sending a confirmed snapshot starts, the share ends even if no receipt
arrives. The sender then reports that the receiver may already have its preview;
TCP cannot prove delivery after a lost acknowledgement. Earlier failed attempts
can retry within the five-attempt/session limits. Attackers can exhaust those
limits or block traffic, so pairing does not guarantee availability.

Discovery bounds include 1,024-byte datagrams, 64-byte UTF-8 names, 32 search
results and 32 replies per second. Private/loopback IPv4 restrictions narrow the
scope but are not authentication. No file paths, commands, persistent trust keys,
automatic imports, or arbitrary remote-write operations are exposed.

`StorageChecks` exercises approval gating, denial/cancellation, size/framing
limits, modified commitments, payload/tag/type/sequence tampering, cross-session
replay and discovery parsing/request-reply behavior. Actual multicast and
Windows/Android cryptographic interoperability require browser/device checks.
