# Kana

Use a disposable browser profile or Android test installation. Kana preferences
are local to that browser/WebView and are outside vocabulary progress sync; see
[architecture](../../docs/ai/ARCHITECTURE.md#kana-selection-and-practice).

## KANA-001 — Selection, settings and local persistence

**Purpose:** Protect character-only content, mixed-script selection and remembered
choices across navigation and cold starts.

**Preconditions:** Start with fresh app/browser storage. For Android use an
authorized test device and preserve the user's existing installation and data.

**Steps:**
1. Open Home → Kana. Clear the Hiragana single set; select あ and い. Use a column
   heading to add/remove a whole column and verify the chosen cells/counts agree.
2. Choose Katakana. Select ア from Single, キャ from Double and ファ from Extended.
3. Open Settings. Select Noto Serif JP and Klee One; disable random order and
   auto-submit. Try deselecting every font; at least one must remain selected.
4. Visit Home and return, then reload/restart the application. Check the chosen
   characters in each set, last script/group and settings.
5. In a separate fresh browser profile/test installation, open Kana.

**Expected result:** The original choices persist in their own profile. The fresh
profile has default vowel selections/settings. The module offers no words,
sentences, kanji, accounts, advertising, site header/footer, audio, confetti,
display/theme or kana-typing settings. The app's existing other modules still work.
Fonts show distinct kana glyphs without network access (on web the local server
must remain reachable). Corrupt/blocked storage produces a clear message and
does not prevent practice for the current visit.

## KANA-002 — Answers, review and round results

**Purpose:** Protect answer/keyboard behavior, first-try scoring and review state.

**Preconditions:** Select only あ and い, turn off Random order, turn on Auto-submit,
Review, High scores and Scoring.

**Steps:**
1. Study and answer あ with `a`. Verify the next card and empty, focused answer input.
2. Submit an incorrect answer for い. Show the answer and press Enter/Next.
3. Check the 1/2 result and best score/time. Review the missed card and answer `i`.
4. Return to Study, reload and verify review is empty. Complete a clean round and
   verify the best score increases; repeat to verify best values are retained.
5. Disable Auto-submit and verify a correct answer waits for Check/Enter.
6. Disable Review, High scores and Scoring; repeat a missed/revealed round.
7. Clear every selected character; open Study.

**Expected result:** Correct answers advance exactly once. Mistakes/reveals cannot
earn first-try credit and are retained for review until a later correct attempt.
Review rounds use only marked cards. Disabled options have no visible scoring,
new high-score recording or new review entries. Empty sets explain how to select
characters. The soft keyboard permits rōmaji answers; Android Back closes the
keyboard and app navigation remains usable. No duplicate answers on Enter.

## KANA-003 — Responsive and offline font presentation

**Purpose:** Protect chart scrolling, font legibility and native host integration.

**Preconditions:** Web at 320px and desktop width; Android test installation with
internet disconnected. Do not disable connectivity on a user-owned active device.

**Steps:**
1. Open each script/group and horizontally scroll the chart to its last column.
2. Open Settings and inspect all nine font previews; select each font for practice.
3. Start practice, open/close the soft keyboard and rotate the device.
4. Navigate to Home and back. Restart the Android test app with no internet.

**Expected result:** The chart scrolls inside the module; the page itself does not
overflow horizontally. Tab labels, controls and focused inputs remain usable.
Opening the Android keyboard reduces the practice card height; the answer field
and Check/Show answer controls remain visible above the keyboard.
All fonts load locally, show the complete selected kana and remain distinct.
Restart restores selections/settings, and practice works offline on Android.
Browser checks do not establish native IME, Back, restart or offline behavior.
