# Local sync regression cases

These cases complement `StorageChecks` with actual web/Android interaction and
network evidence. Use the [sync guide](../../docs/local-sync.md) for the transfer
procedure and the [development guide](../../docs/development.md) for host setup.
Record the tested direction, host/device versions and observations in the current
task. A pass in one direction does not establish the reverse direction.

## SYNC-001 — Automatic transfer between web and Android

### Purpose

Protect discovery, matching-code confirmation and preview/apply across the two hosts.

### Preconditions

- Matching app versions/catalogs on a running web host/browser and Android device.
- Both installations remain foregrounded on the same trusted network with multicast
  and the selected transfer endpoint reachable.
- Disposable progress on both installations. The receiver lacks one known item
  present on the sender; record its identity and both initial states.

### Steps

1. Open **Sync progress** on both installations. Share from Android; on web use
   **Automatic → Find devices** and select the sharing device.
2. Compare all six digits in the pairing popups and confirm on both installations
   only if they match.
3. Wait for the receiver's review popup. Inspect the proposed addition using the
   default `MergeKeepLocal` mode before applying it.
4. Apply the preview and inspect the receiver's known collection for the test item.
5. Re-establish disposable initial states, with an item unique to the web sender,
   and repeat with web sharing and Android receiving.

### Expected result

- The sharing installation is discoverable, and both pairing codes match.
- The receiver opens a review of the expected addition before applying changes.
- Applying makes the test item visible in the receiver's known collection.
- The receiver clears its discovered-device list after connecting; another search
  is needed for a fresh list. A very short transfer need not show a visible progress bar.

### Cleanup

Stop any remaining sharing session and restore or discard only the disposable
test progress/installations created for this case.

## SYNC-002 — Manual transfer between web and Android

### Purpose

Protect the user-entered endpoint path used when automatic discovery is unavailable.

### Preconditions

- Use the versions, test data and initial-state setup from SYNC-001.
- A private IPv4 endpoint displayed by the sender is reachable from the receiver;
  multicast discovery is not required.

### Steps

1. Switch both **Sync progress** pages to **Manual**. Start sharing from Android.
2. On web, enter the private address and port displayed by the sender and connect.
3. Perform the code comparison, preview, apply and collection checks from steps
   2–4 of SYNC-001.
4. Re-establish disposable initial states and repeat with web sharing and Android
   receiving, using the web sender's displayed endpoint.

### Expected result

- Each receiver can connect using the entered endpoint without selecting a
  discovered device.
- Both sides require matching-code confirmation, and the receiver reviews the
  expected addition before applying it, just as in Automatic mode.
- The applied test item appears in the receiver's known collection.

### Cleanup

Use the cleanup from SYNC-001. Record an unreachable endpoint as blocked network
validation with its direction; success in the other direction does not replace it.

## SYNC-003 — Dismissing a received preview preserves progress

### Purpose

Protect the UI boundary between receiving a snapshot and applying saved changes.

### Preconditions

- Use the matching installations and disposable progress from SYNC-001.
- Record the receiver's initial collection state and use a sender snapshot with
  a visible proposed addition. Either connection mode may be used.

### Steps

1. Transfer and confirm codes until the receiver's review popup appears.
2. Inspect the proposed change, then choose **Cancel** without applying it.
3. Inspect the receiver's collection and confirm the proposed item was not added.
4. Receive a fresh preview and repeat steps 2–3 using the popup close button, then
   the shaded background; on a host with a keyboard also repeat using Escape.
5. Repeat with the other host receiving. Record any dismissal method that could
   not be exercised on a target.

### Expected result

- Each exercised dismissal closes the review and discards the preview.
- The receiver's saved collection remains at its initial state after each dismissal.
- Another transfer can reach a fresh review. Unexecuted host/method combinations
  remain explicit validation gaps.

### Cleanup

Use the cleanup from SYNC-001.
