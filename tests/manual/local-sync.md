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

## SYNC-004 — Pairing rejection, cancellation and leaving the page

### Purpose

Protect the UI approval boundary and release sharing/connection resources when a
user cancels or navigates away. Transport denial is automated; these steps check
the dialogs and whether a fresh UI operation remains usable.

### Preconditions

Use the two disposable installations and recorded initial progress from SYNC-001.
Use Manual mode to retain the displayed endpoint for the cancellation check.

### Steps

1. Start sharing and connect. Confirm **Codes match** on only one installation.
   Verify it shows that it is waiting; the receiver must not show a merge preview.
2. On the other installation choose **Codes do not match**. Verify neither save
   changed and no preview is offered. Start a fresh session and connect again.
3. Dismiss pairing using the close button, then repeat with Escape on a keyboard
   host. Verify both peers leave the pending approval state and can start again.
4. Start sharing again, record its port, then navigate Home on the sender. Attempt
   to connect to that old endpoint on the receiver. It must fail without a preview.
5. Return to Sync, start a fresh share, connect and confirm both codes. Reach a
   preview, then cancel it. Repeat with the other host as sender.

### Expected result

One-sided approval, rejection, dismissal and navigation never apply progress.
Dialogs do not remain stuck, old sessions stop accepting connections, and fresh
pairing and preview still work. A closed endpoint may fail immediately or time out.

### Cleanup

Use the cleanup from SYNC-001; cancel any pending connection on both installations.

## SYNC-005 — Merge choices, stale preview and recovery through the UI

### Purpose

Protect selection of import modes, displayed conflict counts, stale-preview error
recovery and the recovery-copy dialog. StorageChecks owns the underlying merge math.

### Preconditions

Use matching disposable installations. Receiver: word A Known, word B Training.
Sender: word B Known, word C Rehearsing. No other selections. Record the identities.
For the stale-preview step, use a second browser tab on the receiver's web host.

### Steps

1. Receive the snapshot. Check Words counts (Added, Changed, Removed, Conflicts):
   Keep local `(1,0,0,1)`, Use incoming `(1,1,0,1)`, Replace `(1,1,1,1)`.
   Change modes in the dialog and verify the counts update before applying.
2. Leave a Replace preview open. In another browser tab on the same receiver,
   select word D Training. Return and choose **Apply this preview**.
3. Verify the stale preview is rejected, D remains selected and the dialog offers
   **Refresh preview**. Refresh, review the changed counts and apply Replace.
4. Verify only B Known and C Rehearsing remain. Restart the receiver, open Sync and
   choose **Preview recovery copy**. Cancel once and verify no change.
5. Preview recovery again and apply it. Verify the pre-import state, including D,
   is restored. On Android repeat mode selection and recovery; the second-tab
   stale-preview step applies to the web host only.

### Expected result

Displayed counts follow the selected mode. Stale work is rejected visibly without
losing the newer save; refresh permits a reviewed import. Recovery survives restart
and changes progress only after explicit apply.

### Cleanup

Close the extra test tab and use the cleanup from SYNC-001.

## SYNC-006 — Invalid endpoint and incompatible catalogs

### Purpose

Protect actionable connection/validation errors and the ability to retry afterward.

### Preconditions

Disposable web hosts with separate progress. For catalog mismatch, make a complete
temporary catalog copy for one host and add harmless JSON whitespace to Words.json;
never edit the canonical catalogs. Raw catalog bytes must match for sync.

### Steps

1. In Manual mode attempt an invalid address (`not-an-ip`), public address
   (`8.8.8.8`) and port `0`. Verify rejection and usable controls after each attempt.
2. Connect the hosts with different catalog bytes and confirm matching codes.
   Verify the receiver reports incompatible catalogs without an applicable preview.
3. Restart the temporary sender with the matching original catalogs and repeat.
   Verify normal pairing and preview work; cancel before applying.

### Expected result

Each invalid input or incompatible snapshot produces a visible error without
changing either save. A corrected attempt works without reloading the receiver.

### Cleanup

Stop temporary hosts, discard their copied catalogs/progress and use SYNC-001 cleanup.
