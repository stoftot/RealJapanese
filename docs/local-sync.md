# Local progress sync

RealJapanese can transfer progress directly between two open app installations.
There is no account or cloud copy.

## Transfer progress

1. Put both devices on the same trusted Wi-Fi network or personal hotspot and keep
   both apps open in the foreground.
2. Open **Sync progress** from Home on the app whose progress you want to share.
   Start sharing and leave that page open.
3. On the other app, leave **Automatic** selected and choose **Find devices**. Pick
   the sharing device from the list. Device names are supplied by nearby apps, so
   use the code comparison rather than trusting a name by itself.
4. Compare the six-digit code on both screens. Confirm on both devices only when
   every digit matches.
5. Review the proposed changes, choose a merge mode, then apply them.

If automatic discovery finds nothing, switch both pages to **Manual**. Enter one
of the private IP addresses and the port displayed by the sharing device, then
compare and confirm the same six-digit code. Automatic and manual modes use the
same pairing and integrity checks; manual mode only replaces device discovery.

`MergeKeepLocal` is the default: incoming progress is added, while the receiver's
category wins when the same item differs. `MergeUseIncoming` gives the incoming
category priority. `Replace` makes the receiver exactly match the shared snapshot,
including removals.

For a two-way merge, merge on the receiver first. Then share the merged result back
and choose `Replace` on the original sender. The receiver can restore the state from
immediately before its latest import; a later import replaces that recovery point,
while ordinary study changes do not discard it.

## Network and compatibility limits

Sharing lasts for five minutes or until success, cancellation or leaving the page.
Code confirmation expires after 90 seconds. Connection setup and progress transfer
also have short timeouts, and repeated failed pairing attempts end the sharing
session. The app accepts only private IPv4 or loopback peers and never scans address
ranges.

Both connection modes establish an ephemeral pairing and require matching codes on
both screens before progress is sent. This detects an altered or substituted
connection when the codes are compared correctly, but progress contents are not
encrypted. Other devices on the network may be able to read the transfer. Use a
network you trust, inspect the preview carefully and do not forward the port through
a router.

Automatic discovery uses RealJapanese's own small UDP multicast protocol at
`239.255.77.77:47777`; it is not mDNS. Operating-system multicast permissions,
firewall rules and guest-network isolation can block it. Manual mode remains
available in that case. In the web version, the local web server performs discovery
and transfer network operations, so firewall permission applies to that process.

RealJapanese does not change firewall settings. If a phone cannot connect to a PC
that is sharing, share from the phone and receive on the PC instead. Guest Wi-Fi
may isolate devices even when both appear connected.

The receiver accepts at most 4 MiB and requires the same snapshot schema, valid
catalog IDs and identical raw catalog files. Update both installations to matching
catalogs if validation fails. The `RJLAN003` protocol is incompatible with prior
versions, so update both installations together. See
[Local sync protocol](local-sync-protocol.md) for technical details and limits.
The transfer exposes no API for arbitrary filesystem paths or remote writes; only
a validated progress snapshot can be previewed and applied.

Current progress lives in one atomic `Progress.json`. On first use, an installation
without that file reads the five older `SavedData.json` files and leaves them
untouched. Older app versions will therefore not see progress changed by the new
version. Separate running processes do not receive live updates; a conflicting
writer is refused instead of silently overwriting newer disk state.
