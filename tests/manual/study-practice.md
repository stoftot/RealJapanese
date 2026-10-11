# Study selection and practice regression cases

These cases cover the shared study UI used by both the web and Android hosts. Run
them against disposable progress and preserve the canonical catalogs. Record the
host, viewport, catalog version and the identities of selected study items in the
current task. Random question order does not change the expected sets below.
The [coverage catalog](README.md#automation-and-remaining-manual-scope) maps these
cases to automated unit, component and browser tests. Focus manual runs on native
Android behavior and human usability observations not established by those tests.

## STUDY-001 — Search, assign and persist study lists

### Purpose

Protect search behavior, exclusive category assignment, persistence, and the
independent single/combined kanji lists.

### Preconditions

- A running host with disposable, initially empty progress and the normal catalogs.
- For Words & expressions, Nouns, Verbs and Adjectives, record one item whose English has mixed case and
  whose Japanese and kana values can each be searched independently.
- For both Combined and Single kanji, record one item and its three searchable text
  values. Use different items for the two kanji sets.

### Steps

1. Open **Words & expressions**. In the Known list search for the recorded item by a distinctive
   English substring, then repeat with different letter case and with surrounding
   spaces. Repeat using a distinctive Japanese substring and a kana substring.
2. Enter a string absent from every English, Japanese and kana value, then clear
   the search.
3. Select the item in Known. Switch to Rehearsing and confirm it is unavailable
   there while Known. Return to Known and deselect it, then select it in
   Rehearsing. Deselect it there before selecting it in Training.
4. Refresh the page and confirm the item remains selected only in Training.
   Deselect it, refresh again, then reselect it in Known and refresh once more.
5. Repeat steps 1–4 for **Nouns**, **Verbs** and **Adjectives**.
6. Open **Kanji**, choose Combined, and repeat steps 1–4 with the recorded combined
   item. Switch to Single and repeat with the recorded single item.
7. Switch between Combined and Single several times and inspect the recorded item
   in each set.
8. On an editable selector, select two items in Known and a different item in
   Rehearsing. Collapse a group or search so one selected Known item is hidden.
   Press **Move selected to Rehearsing** above Known. Clear the search and inspect
   both lists, then reload. Move the Rehearsing selection to Training, back to
   Rehearsing, and finally to Known using the corresponding arrow buttons.
9. Confirm empty source lists have disabled move buttons. Open **Words** and
   confirm its saved-word mixer has no move buttons. At desktop and phone widths,
   check the left/right placement above each editable list and room for two group
   headers and five ordinary word entries inside the scrolling list.

### Expected result

- English search is case-insensitive and ignores surrounding search whitespace;
  Japanese and kana searches find the same item. The absent string shows
  **No matches in this list** without changing selection.
- An item is selected in at most one of Known, Rehearsing and Training. The lower
  lists exclude an item assigned above them; after deselection the item can be
  assigned through the next list's own UI.
- Selected counts and highlighting update immediately. Clear, reselect and refresh
  preserve exactly the last saved state.
- Nouns are available in their own selector and retain independent Known,
  Rehearsing and Training assignments.
- Combined and Single kanji retain independent category assignments.
- Moves include every selected source item, including hidden items, and keep them
  selected in the destination alongside its existing selections. Unselected items
  and other categories remain unchanged. The saved result survives reload/restart.
- Known has a right arrow to Rehearsing; Rehearsing has a left arrow to Known and
  a right arrow to Training; Training has a left arrow to Rehearsing. Buttons have
  complete readable labels without overlapping at phone widths.

### Cleanup

Deselect the recorded items from every list, or discard only the disposable
progress root or installation used by this case.

## STUDY-002 — Offered practice routes use the selected list

### Purpose

Protect every practice action offered by the selectors, its route, and category
query selection, including an empty category.

### Preconditions

- A running host with disposable progress.
- Put one identifiable item in each of Known, Rehearsing and Training for Words & expressions,
  Nouns, Verbs and Adjectives. Do the same for both Combined and Single kanji.
- Keep one additional category empty in a separate disposable dataset or after
  recording enough state to restore this setup.

### Steps

1. For each row in the matrix, open the selector, choose each applicable study
   list, select **Start practice**, and record the resulting address and first
   question. Use browser Back to return before testing the next action.

   | Selector and offered action | Expected route | Applicable list |
   | --- | --- | --- |
   | Words — Learn the words | `/words/spelling?category=<list>` | All three |
   | Words — Flash cards | `/words/flashcards?category=<list>` | All three |
   | Words & expressions — Learn the words | `/vocabulary/spelling?category=<list>` | All three |
   | Words & expressions — Flash cards | `/vocabulary/flashcards?category=<list>` | All three |
   | Nouns — Learn the nouns | `/nouns/spelling?category=<list>` | All three |
   | Nouns — Flash cards | `/nouns/flashcards?category=<list>` | All three |
   | Nouns — Conjugations and forms | `/nouns/conjugate` | Category-independent |
   | Verbs — Learn the verbs | `/verbs/spelling?category=<list>` | All three |
   | Verbs — Verb type categorisation | `/verbs/categories?category=<list>` | All three |
   | Verbs — Conjugation and forms | `/verbs/ConjugationsAndForms` | Category-independent |
   | Adjectives — Learn the adjectives | `/adjectives/spelling?category=<list>` | All three |
   | Adjectives — Type categorisation | `/adjectives/categories?category=<list>` | All three |
   | Adjectives — Type conjugation | `/adjectives/conjugateBase` | Category-independent |
   | Kanji Combined — Learn the kanji | `/kanji/combined/meaning?category=<list>` | All three |
   | Kanji Single — Learn the kanji | `/kanji/single/meaning?category=<list>` | All three |

   Replace `<list>` with the lowercase value `known`, `rehearsing` or `training`.
   Words practice links also carry `sources=<dataset names>` from the mixer;
   only saved words from the eleven grammatical categories should appear. The fallback
   Words source is excluded from this mixer; test it in Words & expressions instead.
2. For category-dependent rows, confirm the question belongs to the chosen list
   and not one of the two other prepared lists.
3. Open one category-dependent action for the deliberately empty list.
4. On every destination, use at least one visible control to establish that the
   rendered card is interactive rather than a static or failed navigation page.

### Expected result

- Every offered action reaches the expected route without **Page not found**, a
  host error page, or a disconnected/noninteractive card.
- Category-dependent practice draws only from the category in its query. The
  category-independent conjugation exercises open without a category query.
- Noun conjugation practice works with an empty study list and asks for endings
  in romaji: four polite forms, four short forms and the te-form. Polite and
  short prompts are distinct, and every question remains accessible with nine chunks.
- An empty category-dependent exercise shows **Nothing to practise yet** and remains responsive.

### Cleanup

Restore or discard only the disposable progress used by this case.

## STUDY-003 — Single-answer rounds, reveal and retry

### Purpose

Protect answer normalization, keyboard progression, input focus, and the bounded
retry round for revealed answers.

### Preconditions

- A category containing at least three questions with known answers on a
  single-answer route such as `/words/spelling?category=known`.
- Record the displayed question order while executing the case; question order
  may be shuffled between runs.

### Steps

1. For the first question, enter the correct answer with changed letter case,
   leading/trailing spaces and an internal space. Observe the transition without
   pressing Enter.
2. Confirm the next question receives focus and its answer input is empty.
3. Enter an incorrect answer and press Enter. Press Enter again without changing
   the input.
4. On another question, select **Show answer** repeatedly, then advance once.
5. Complete every remaining question in the original round correctly.
6. Complete the retry round and record every question it contains.
7. Answer every retry correctly without revealing it, then inspect the next round.

### Expected result

- Case and whitespace differences are ignored for a correct answer. A correct
  answer auto-advances exactly once, clears the input and keeps answer focus.
- The first Enter on an incorrect answer reveals the answer; the next Enter with
  unchanged input advances.
- Each revealed question occurs exactly once in the retry round even when reveal
  is requested repeatedly. Questions answered correctly without reveal are absent
  from that retry round.
- After the retry round, practice resumes with the remaining question set. Items
  automatically moved to another study list are excluded; when no items moved,
  the full original set remains available.

### Cleanup

Navigate away. Discard disposable progress only if the setup changed study-list
membership.

## STUDY-004 — Chunk boundaries and session reset

### Purpose

Protect chunk coverage for uneven sets, empty chunks when there are more chunks
than items, and isolation of per-chunk answer/retry state.

### Preconditions

- A disposable category with five items whose displayed practice questions are
  distinct and recorded.
- Use one shared-base route such as Words spelling and repeat the union check on
  Combined kanji meaning.

### Steps

1. Set **Number of chunks** to 2. Visit both selected chunks and record every
   question encountered in one complete round of each.
2. Compare the union with the five prepared questions and check for duplicates
   across chunks.
3. Set **Number of chunks** to a value greater than five. Visit every selected
   chunk, including each empty chunk, and record the union again.
4. In a nonempty chunk, enter text and reveal the answer so the question is due for
   retry. Before finishing the round, switch to a different nonempty chunk.
5. Inspect the progress indicator, answer visibility and input, then finish the new
   chunk far enough to reach any retry round.
6. Switch back to the original chunk and start a fresh round there.
7. Without switching chunks, reveal one question repeatedly, finish the original
   round, reveal it again in its retry, and finish that retry. Answer it correctly
   on the next retry. Repeat with split counts from 1 through 10 in Training.
8. In Known Words spelling, prepare five words, split into two chunks and select chunk two.
   Miss one question and finish the original round. Answer its retry correctly.
   Record the next complete round and both chunk controls.
9. With three Known words, split into three chunks and select chunk three. Miss
   its only question and answer the retry correctly, then select chunk one.

### Expected result

- Across all chunks, every prepared question occurs once in the union with no
  omission or cross-chunk duplicate. This requirement does not prescribe which
  Combined kanji chunk receives an uneven remainder.
- A chunk with no items shows the empty-practice state and does not throw or show
  an invalid progress value.
- Changing chunk resets progress to the start, clears input, and hides the prior
  answer. A retry queued in the old chunk does not appear in the new chunk.
- Returning to the original chunk begins a coherent round for that chunk.
- Repeated reveals queue a question only once per retry round. Another miss keeps
  it in the next retry. Finishing retries in Training restores the original chunk
  membership and size; retry counts do not redefine the full chunk.
- Automatic moves leave the active round and outstanding retries intact. Once all
  retries finish, the remaining pool is divided again using the same split count
  and selected chunk number. In step 8, the next chunk has two remaining words,
  including one drawn from the former first chunk, and excludes the moved word.
- Step 9 keeps chunk three selected with the empty-practice state. Selecting chunk
  one still opens its remaining question. Changing the split count resets the
  selection to chunk one and clears abandoned retry/answer state.

### Cleanup

Restore or discard only the disposable progress/dataset used by this case.

## STUDY-005 — Flashcard controls and keyboard shortcuts

### Purpose

Protect reveal, remembered-difficulty retries, global shortcuts, focus, and
shortcut cleanup across navigation.

### Preconditions

- `/words/flashcards?category=known` has at least three recorded questions.
- A host with a hardware keyboard or emulator keyboard injection.

### Steps

1. With the primary flashcard button focused, press Space to reveal the answer,
   then Space again to advance.
2. Before revealing another card, press Backspace. Confirm the answer is revealed;
   then advance.
3. Reveal a third card normally, then press Backspace to mark **Gave wrong answer**
   and advance.
4. Finish the original round and record the retry round.
5. Navigate back to Words, reopen Flash cards, and repeat one Space and one
   Backspace action. Repeat the navigation cycle once more.
6. While a text-entry control elsewhere has focus, press Space and Backspace.

### Expected result

- Space performs one primary action: reveal when hidden and advance when shown.
  Backspace performs **Forgot** before reveal and **Gave wrong answer** after reveal.
- The Forgot and wrong-answer questions each occur once in the retry round; the
  correctly advanced card does not.
- The action button retains useful focus after transitions. Each shortcut causes
  exactly one transition after every navigation cycle, with no duplicate handler.
- Global flashcard shortcuts do not consume Space or Backspace in text entry.

### Cleanup

Navigate away and discard disposable progress only if setup changed membership.

## STUDY-006 — Multiple-answer meaning and spelling questions

### Purpose

Protect unordered semicolon-separated meanings and the ordered spelling/type
contract used by verb and adjective spelling.

### Preconditions

- A Single kanji question with at least two distinct semicolon-separated English
  meanings, recorded in advance.
- One verb and one adjective spelling question with known romanized spelling and
  short type (`u`, `ru`, `i`, `na` or `ir`).
- Prepare at least three distinct questions in each exercised category so an
  accidental double advance is observable rather than hidden by wrapping.

### Steps

1. Open Single kanji meaning. Enter its meanings in reverse order, varying case
   and adding surrounding spaces to each submission.
2. Before supplying the last missing meaning, submit an already accepted meaning
   again. Then submit the last missing meaning once.
3. Confirm the transition and the state shown for the next question.
4. Open Verb spelling. Submit the type before the romanized spelling, then use
   **Show answer** and advance. On its retry, submit romanized spelling first and
   short type second.
5. Repeat step 4 for Adjective spelling.

### Expected result

- Single kanji meanings are accepted in any order with case and surrounding
  whitespace ignored. An accepted meaning moves from missing to given.
- Repeating an accepted meaning gives no second credit and does not advance. The
  last distinct missing meaning advances exactly once, with clean state and focus
  on the next question.
- Verb and adjective spelling require two ordered submissions: romanized spelling
  first, short type second. Reversing the order does not credit the answers;
  entering them in order advances exactly once after the type.

### Cleanup

Navigate away and restore or discard disposable study-list membership.

## STUDY-007 — Responsive layout, text scale and accessible controls

### Purpose

Protect usable selector/practice layouts at phone and desktop widths and the
keyboard/accessibility semantics of interactive controls.

### Preconditions

- A desktop browser that can set a 320 CSS-pixel viewport and a viewport at least
  1024 CSS pixels wide.
- A selector with populated lists and a nonempty typed practice route.
- Browser accessibility inspection or a screen reader, plus keyboard-only input.

### Steps

1. At 320 px width open Words, then Kanji. Cycle every study-list toggle and both
   kanji-set options. Search and select an item in each visible active list.
2. Confirm only the active category column is presented at 320 px. Scroll its list
   and reach every selector and **Start practice** control without horizontal
   page scrolling.
3. Open practice. Move the **Text size** range to its minimum and maximum with the
   keyboard. Exercise both chunk number fields and the answer/actions.
4. Repeat at a desktop width of at least 1024 px, confirming all three category
   columns are available together.
5. Tab through the page. Inspect or have a screen reader announce the main
   navigation, study-list and kanji-set toggle groups, search input, text-size
   range, both chunk number fields, answer input and action buttons.

### Expected result

- At 320 px the active selector column, toolbar, practice settings and actions fit
  without clipped content or horizontal page scrolling. At desktop width all
  category columns remain usable.
- Text visibly scales from 80% through 160% while controls remain operable and
  content remains readable at both endpoints.
- Every interactive control is keyboard reachable, has a visible focus indicator,
  and exposes a useful accessible name and current state. Toggle changes and range
  changes are announced and visible.

### Cleanup

Restore the browser viewport and discard any disposable selection changes.

## STUDY-008 — Japanese language oracle for conjugation and numbers

### Purpose

Protect representative literal Japanese/romaji outputs independently of internal
implementation strings.

### Preconditions

- A running host for the offered adjective conjugation and Numbers pages.
- For generator inputs not offered by the current Numbers page, a disposable
  debugger, REPL or scratch runner that references `Repositories` and calls the
  public `NumbersGenerator` API without writing repository files. Record these as
  direct library observations, not UI observations.

### Steps

1. On `/adjectives/conjugateBase`, cycle the finite round until the following
   prompts appear and compare their revealed romanized answers with the oracle:

   | Prompt | Expected answer |
   | --- | --- |
   | `い - present affirmative` | `idesu` |
   | `い - present negative` | `kunaidesu` |
   | `い - past affirmative` | `kattadesu` |
   | `な - present affirmative` | `nadesu` |
   | `いい - present negative` | `yokunaidesu` |

   These prompts represent endings, not complete words. For example, the negative
   ending replaces `い` with `くないです`; it does not prepend an extra `い`.

2. On `/numbers`, check any listed oracle input that the UI presents. For the
   remaining inputs, directly call `GenerateCounting` or `GenerateAge` as indicated:

   | Direct input | Expected answer |
   | --- | --- |
   | `GenerateCounting(100)` | `hyaku` |
   | `GenerateCounting(301)` | `sanbyakuichi` |
   | `GenerateCounting(100000)` | `juuman` |
   | `GenerateAge(1)` | `issai` |
   | `GenerateAge(8)` | `hassai` |
   | `GenerateAge(10)` | `jussai` or `jissai` |
   | `GenerateAge(20)` | `hatachi`, `nijussai` or `nijissai` |

3. For each UI prompt, also enter the expected answer and confirm it is accepted.
   For each direct call, inspect both `Question` and `Answer` and retain the input
   alongside the observed result in the current task.

### Expected result

- Present affirmative i-adjectives retain their final `i` before `desu`; they do
  not collapse to `desu`. The other representative adjective forms match the
  table exactly after the UI's case/whitespace normalization.
- Counting and age answers match the accepted readings in the table. The direct
  library observations are reported separately from UI coverage.

### Cleanup

Close the scratch process without saving repository files and leave canonical
catalogs and progress unchanged.

### Oracle references

Use the Japan Foundation's [adjective grammar](https://www.erin.jpf.go.jp/materials/key-phrases/download/erin_lesson12_key-phrases_explanation.pdf)
and [Marugoto counter appendix](https://a1.marugotoweb.jp/en/assets/pdf/grammar/A1_appendix_01_en.pdf),
plus Tokyo University of Foreign Studies' [number composition](https://www.coelang.tufs.ac.jp/mt/ja/gmod/contents/explanation/016.html)
and [counter alternatives](https://www.coelang.tufs.ac.jp/mt/ja/gmod/contents/explanation/017.html).
Romanized answers use the application's vowel spelling and omit spaces.

## STUDY-009 — Book and lesson selection

### Purpose

Protect collapsible source groups, selection scope, repeated lesson entries and
access to every grammatical vocabulary category on web and Android.

### Preconditions

- Disposable progress with current catalogs, plus a fixture containing Book A
  lessons 1, 2 and 10, Book B lesson 1, and one self-added word.
- Include one Book A word with origins in both lessons 1 and 2.

### Steps

1. Open Words & expressions, Nouns, Verbs and Adjectives. Confirm their source groups
   and practice links are available, including on a narrow screen.
2. In the fixture selector, collapse and reopen Book A and lesson 2. Confirm no
   study assignments change and lessons appear in numeric order.
3. Select Book A lesson 1. Confirm Book B lesson 1 and General remain unchanged,
   and Book A shows a partial selection. Collapse lesson 2 and select Book A.
4. Confirm every eligible Book A word is selected, including collapsed descendants,
   with the repeated word counted once. Deselect Book A and select General.
5. Search for one Book A word, then select its book header. Confirm only matching
   words change. Clear the search and verify the earlier collapse state returns.
6. Reload/restart and confirm assignments persist. Repeat in Training and Rehearsing,
   respecting their eligible-word exclusions. Start spelling and flashcard exercises.
7. On Android, use touch to expand/select and inspect controls with a screen reader.
   Repeat the saved-state checks after an update from an older disposable installation
   using MOBILE-001; known/training/rehearsing words must follow their new categories.

### Expected result

Expansion never changes assignments. Book and lesson selection has the stated
scope, partial state is announced, repeated identities stay consistent, and search
limits bulk actions to matches. General remains independent of book sources.
All category routes load usable exercises from the chosen study list. Every old
selected word keeps its category after migration and restart.

### Cleanup

Discard only the disposable fixture/progress or test installation.

## STUDY-010 — Saved vocabulary mixer

### Purpose

Verify independent backend-category toggles combine saved study lists without
changing progress, and preserve the chosen sources through practice and navigation.

### Preconditions

- Disposable catalogs/progress with two categories sharing the same numeric word ID.
- Saved Known, Rehearsing and Training entries and at least one unsaved entry.
- A saved noun, verb or adjective to combine with Words & expressions categories.
- A saved fallback Words entry to confirm it remains editable only in Words & expressions.

### Steps

1. Open Words. Confirm all eleven grammatical category toggles are present and initially
   enabled, with no General words toggle. Each study list contains only its saved entries;
   unsaved words and fallback Words entries are absent.
2. Turn one category off and on. Confirm only its words disappear and return in all
   three lists, including entries sharing an ID with another category.
3. Search and collapse a book/lesson. Confirm there are no item or group assignment
   controls. Compare saved progress before and after these mixer actions.
4. Enable two categories, select Training and start spelling, then flashcards.
   Confirm both exercises use only the chosen sources' Training words. Return with
   the back link and reload; the chosen sources and returned study list remain active.
5. Disable all sources and reload. Confirm lists and practice stay empty.
6. Open a mixer or direct exercise link containing `sources=Words`. Confirm it has no
   saved words. A link containing `sources=Words,Nouns` includes only saved nouns;
   practice and back links omit the fallback key. Confirm its saved progress is untouched.
7. Repeat toggles, practice and back navigation at 320 px and on Android by touch.
   Confirm the combined Words & expressions editor remains available in navigation.

### Expected result

Backend categories and IDs stay separate. Mixer actions never write progress.
Toggles are keyboard-operable and announce their pressed state. Practice includes
only the saved words in the enabled categories and selected study list.

### Cleanup

Discard only the disposable fixture/progress or test installation.

## STUDY-011 — Daily rehearsal and host-local date boundary

### Purpose

Protect automatic progress from Learn the words/nouns/verbs/adjectives/kanji
exercises and confirm that each host uses its own local calendar day.

### Preconditions

- Disposable progress with an identifiable Known and Rehearsing item in Words,
  Nouns, Verbs, Adjectives, Single kanji and Combined kanji.
- A web host whose server time zone can be controlled and an Android device whose
  device time zone can be controlled. Use different zones from UTC and record both.
- Read-only access to the task-owned `Progress.json` in the disposable progress
  root (or an exported snapshot) to inspect each item's `Rehearsal` fields; the UI
  does not display the correct-day count.
- Do not use user progress or change catalog files. Automated repository coverage
  is in `RehearsalProgressTests`; this case checks app and host integration.

### Steps

1. On each host, open each Learn the X route: Words, Nouns, Verbs, Adjectives,
   Single kanji and Combined kanji. Select the Known item. Reveal the answer or
   submit a wrong answer, then inspect disposable progress and confirm it has
   immediately moved to Rehearsing with zero earned correct days. With one item,
   navigate away or close/reopen the app before retrying; confirm it is still
   Rehearsing and absent from a freshly opened Known practice session.
2. With another Known item, miss its answer and stay in the session. Reach that
   same item in its retry round and answer correctly without revealing. Confirm
   it stays Rehearsing with zero earned correct days on the reset date.
   Finish all outstanding retries; confirm the next full round excludes the moved
   item. With no remaining items, confirm the empty-practice state appears.
3. On one Rehearsing item, answer correctly twice on the same local date. Confirm
   the history counts one correct day. Advance the host clock across a local
   midnight that does not coincide with UTC midnight, answer again, and confirm
   the second day is credited according to the host's local date.
4. On another Rehearsing item, earn a correct day, then submit a wrong or revealed
   answer on the same local date and later answer correctly. Confirm the miss
   resets the count and blocks credit for the rest of that local date.
5. Continue one Rehearsing item across seven distinct, not necessarily consecutive,
   local dates, confirming repeated answers on a date count once. Confirm it moves
   to Known after the seventh date and is absent from the next full Rehearsing round.
6. In Training, use a Learn the X route and confirm answers do not change category
   or create rehearsal history. Repeat with flashcards, type categorisation and
   conjugation drills for Known/Rehearsing items; confirm those exercises do not
   change rehearsal history.
7. Compare the web dates with the web server's local timezone and Android dates
   with the device's local timezone. Restore the original clock/timezone settings.

### Expected result

- A wrong or revealed Known answer immediately moves to Rehearsing and persists
  the reset. Leaving the page or restarting before retrying does not undo it.
  Staying in the session preserves its retry; a correct retry on the reset date
  earns no daily credit.
- Rehearsing progress counts at most one correct day per local date; the dates need
  not be consecutive. A miss resets credit and blocks later same-day credit. Seven
  distinct correct dates promote to Known.
- The web uses server local time and Android uses device local time. Training,
  flashcards, type categorisation and conjugation drills do not affect this progress.
- Automated `RehearsalProgressTests` cover repository calendar, reset, migration
  and sync/recovery behavior. A passing automated test does not establish the web
  server or Android device timezone boundary; record those host observations
  separately, and mark Android unexecuted when no device run was performed.

### Cleanup

Restore disposable progress and the original host/device time and timezone settings.
