# Android lifecycle regression cases

These cases require an Android emulator or device. Use a disposable app identity
or disposable installation and record the Android version, package version,
signing identity and device form factor in the current task. A browser result does
not substitute for native lifecycle, WebView, packaged-file or IME evidence.

## MOBILE-001 — Offline install and progress preservation

### Purpose

Protect first-run catalog installation, offline study, and private progress across
cold restart and a same-signer application update.

### Preconditions

- A disposable device/emulator on a supported Android version with no existing
  data for the test package.
- An installable build and a second build with the same package ID and signing key
  that Android accepts as an update. Both builds package the five current catalogs.
- Network connectivity can be disabled after installation.

### Steps

1. Install the first build, disable network connectivity, and launch it cold.
2. Open Words, Verbs, Adjectives, Kanji Combined and Kanji Single. In each dataset,
   search for a recorded catalog item and assign it to a recorded study category.
3. Open at least one practice action for each dataset and confirm its selected item
   can be reached. Keep network connectivity disabled.
4. Force-stop the app without clearing its data, relaunch it cold, and inspect all
   five recorded assignments.
5. Install the second same-signer build as an update without uninstalling or
   clearing app data. Launch it cold while still offline and repeat the five
   assignment and practice checks.

### Expected result

- First launch completes and all five packaged catalogs are usable offline:
  Words, Verbs, Adjectives, Kanji Combined and Kanji Single.
- Every recorded assignment survives force-stop/cold restart.
- The accepted same-signer update refreshes/retains usable packaged catalogs while
  preserving every private progress assignment. No web server or internet
  connection is needed for these study checks.

### Cleanup

Re-enable the device's prior network setting and uninstall only the disposable
test package, or clear only its disposable app data.

## MOBILE-002 — IME, Back, rotation and foreground lifecycle

### Purpose

Protect native Back behavior, keyboard-responsive layout, WebView state and study
progress while Android changes configuration or app visibility.

### Preconditions

- A disposable installed app with at least two items in a practice category.
- A phone-sized portrait viewport and a practice route with an answer field.
- Record the selected category, current question and one saved list assignment.

### Steps

1. Navigate Home → a selector → a typed practice route. Focus the answer field and
   enter a partial answer so the IME is visible.
2. Press Android Back once. Inspect the route, partial answer and navigation layout.
3. With the IME closed, press Android Back again and inspect the destination.
   Navigate forward to the same practice route once more.
4. Focus the answer field. Rotate portrait → landscape → portrait while observing
   the WebView, active route, current question and input.
5. Background the app from the practice route, wait for it to be fully nonvisible,
   then foreground it. Complete one answer and inspect the saved assignment.
6. Press Android Back through practice → selector → Home, one level at a time.
7. Cold restart without clearing data and inspect the recorded assignment.

### Expected result

- The first Back dismisses the IME without leaving practice. The phone-width
  navigation hidden for the open keyboard returns when the IME closes, and the
  partial answer remains intact.
- With the IME closed, Back returns through actual in-app navigation history one
  level per press; it does not skip directly from practice past the selector.
- Rotation and background/foreground do not duplicate the WebView, reset the route,
  show a permanent loading view, or lose the active question/input unexpectedly.
- Practice remains interactive afterward, and saved list progress survives every
  lifecycle transition and the final cold restart.

### Cleanup

Return to Home and discard only the disposable package data when the surrounding
mobile validation is complete.

## MOBILE-003 — Optional recoverable startup failure

### Purpose

Protect the startup error message and retry path when a packaged catalog cannot be
opened, without risking user progress or canonical data.

### Preconditions

- Select this optional case only when a disposable test package or harness can
  cause one packaged catalog open/copy to fail and then restore it without editing
  user-owned files, corrupting private progress, or exhausting shared device
  storage. Examples include a deliberately incomplete disposable package followed
  by a corrected same-signer build, or a controllable test-only file provider.
- Seed one recorded progress assignment before introducing the failure when the
  harness permits it.

### Steps

1. Activate the controlled catalog-open/copy failure and launch the app cold.
2. Inspect the loading screen after initialization fails.
3. Remove the failure condition while preserving the test package's private data.
4. Select **Try again**. If the harness requires installing a corrected same-signer
   package first, update in place, relaunch, and then use **Try again** if shown.
5. Open every study dataset and inspect the seeded assignment.

### Expected result

- Initialization stops on the loading screen, shows a clear statement that study
  files could not be opened and saved progress was not reset, stops the spinner,
  and exposes **Try again**.
- After the condition is restored, retry creates one working study WebView and all
  five catalogs open. Repeated retry taps do not create duplicate content.
- The seeded private progress remains unchanged.

### Cleanup

Remove the deliberately incomplete test package or reset the test-only provider.
Discard only the disposable installation and its data.
