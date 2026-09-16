# Local progress sync

RealJapanese can transfer progress directly between two open app installations.
There is no account or cloud copy.

## Transfer progress

1. Put both devices on the same trusted Wi-Fi network or personal hotspot and keep
   both apps open in the foreground.
2. Open **Sync progress** from Home on the app whose progress you want to share.
   Start sharing and leave that page open.
3. On the other app, choose receive and enter one of the displayed private IP
   addresses and the port.
4. Review the proposed changes, choose a merge mode, then apply them.

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
It uses a temporary random TCP port, accepts only literal private IPv4 or loopback
addresses, and stops after the first successful fetch. Anyone on the LAN who knows
the address and port can fetch while sharing is active. Traffic is unencrypted and
the sender is not authenticated, so another network peer could observe, alter or
substitute progress. Use a network you trust, inspect the preview carefully and do
not forward the port through a router.

RealJapanese does not change firewall settings. If a phone cannot connect to a PC
that is sharing, share from the phone and receive on the PC instead. Guest Wi-Fi
may isolate devices even when both appear connected.

The receiver accepts at most 4 MiB and requires the same snapshot schema, valid
catalog IDs and identical raw catalog files. Update both installations to matching
catalogs if validation fails. The `RJLAN002` protocol is incompatible with older
pairing-based app versions, so update both installations together. The transfer
exposes no API for arbitrary filesystem paths or remote writes; only a validated
progress snapshot can be previewed and applied.

Current progress lives in one atomic `Progress.json`. On first use, an installation
without that file reads the five older `SavedData.json` files and leaves them
untouched. Older app versions will therefore not see progress changed by the new
version. Separate running processes do not receive live updates; a conflicting
writer is refused instead of silently overwriting newer disk state.
