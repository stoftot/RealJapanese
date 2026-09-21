# Study selection and practice regression cases

These cases cover the shared study UI used by both the web and Android hosts. Run
them against disposable progress and preserve the canonical catalogs. Record the
host, viewport, catalog version and the identities of selected study items in the
current task. Random question order does not change the expected sets below.

## STUDY-001 — Search, assign and persist study lists

### Purpose

Protect search behavior, exclusive category assignment, persistence, and the
independent single/combined kanji lists.

### Preconditions

- A running host with disposable, initially empty progress and the normal catalogs.
- For Words, Verbs and Adjectives, record one item whose English has mixed case and
  whose Japanese and kana values can each be searched independently.
- For both Combined and Single kanji, record one item and its three searchable text
  values. Use different items for the two kanji sets.

### Steps

1. Open **Words**. In the Known list search for the recorded item by a distinctive
   English substring, then repeat with different letter case and with surrounding
   spaces. Repeat using a distinctive Japanese substring and a kana substring.
2. Enter a string absent from every English, Japanese and kana value, then clear
   the search.
3. Select the item in Known. Switch to Rehearsing and confirm it is unavailable
   there while Known. Return to Known and deselect it, then select it in
   Rehearsing. Deselect it there before selecting it in Training.
4. Refresh the page and confirm the item remains selected only in Training.
   Deselect it, refresh again, then reselect it in Known and refresh once more.
5. Repeat steps 1–4 for **Verbs** and **Adjectives**.
6. Open **Kanji**, choose Combined, and repeat steps 1–4 with the recorded combined
   item. Switch to Single and repeat with the recorded single item.
7. Switch between Combined and Single several times and inspect the recorded item
   in each set.

### Expected result

- English search is case-insensitive and ignores surrounding search whitespace;
  Japanese and kana searches find the same item. The absent string shows
  **No matches in this list** without changing selection.
- An item is selected in at most one of Known, Rehearsing and Training. The lower
  lists exclude an item assigned above them; after deselection the item can be
  assigned through the next list's own UI.
- Selected counts and highlighting update immediately. Clear, reselect and refresh
  preserve exactly the last saved state.
- Combined and Single kanji retain independent category assignments.

### Cleanup

Deselect the recorded items from every list, or discard only the disposable
progress root or installation used by this case.

## STUDY-002 — Offered practice routes use the selected list

### Purpose

Protect every practice action offered by the selectors, its route, and category
query selection, including an empty category.

### Preconditions

- A running host with disposable progress.
- Put one identifiable item in each of Known, Rehearsing and Training for Words,
  Verbs and Adjectives. Do the same for both Combined and Single kanji.
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
   | Verbs — Learn the verbs | `/verbs/spelling?category=<list>` | All three |
   | Verbs — Verb type categorisation | `/verbs/categories?category=<list>` | All three |
   | Verbs — Conjugation and forms | `/verbs/ConjugationsAndForms` | Category-independent |
   | Adjectives — Learn the adjectives | `/adjectives/spelling?category=<list>` | All three |
   | Adjectives — Type categorisation | `/adjectives/categories?category=<list>` | All three |
   | Adjectives — Type conjugation | `/adjectives/conjugateBase` | Category-independent |
   | Kanji Combined — Learn the kanji | `/kanji/combined/meaning?category=<list>` | All three |
   | Kanji Single — Learn the kanji | `/kanji/single/meaning?category=<list>` | All three |

   Replace `<list>` with the lowercase value `known`, `rehearsing` or `training`.
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
- An empty category shows **Nothing to practise yet** and remains responsive.

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
- After the retry round, practice resumes with the complete original question set.

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

### Expected result

- Across all chunks, every prepared question occurs once in the union with no
  omission or cross-chunk duplicate. This requirement does not prescribe which
  Combined kanji chunk receives an uneven remainder.
- A chunk with no items shows the empty-practice state and does not throw or show
  an invalid progress value.
- Changing chunk resets progress to the start, clears input, and hides the prior
  answer. A retry queued in the old chunk does not appear in the new chunk.
- Returning to the original chunk begins a coherent round for that chunk.

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
