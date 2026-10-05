# Genki companion regression cases

## GENKI-001 — Lesson recaps and stored-question practice

- **Purpose:** protect all lesson recaps, the published-question practice loop and
  the clear unavailable-practice states.
- **Preconditions:** application running with its packaged curriculum. The shipped
  `Genki/questions.jsonl` is empty, so practice is unavailable in a clean install.
  For round, retry and answer-comparison checks, use a disposable web or Android
  installation with a reviewed question in its private catalog; never add fixtures
  to the canonical bank. No saved vocabulary is required for lesson recaps.
- **Steps:** open Genki and visit lessons 1 and 12. In a clean install, confirm the
  empty-bank notice links to Words and practice buttons are disabled. With a
  private reviewed fixture bank, start a point, enter a Japanese sentence with the
  browser or native IME, compare it with the supplied model, choose retry, finish
  the round and navigate to another lesson. Repeat on Android with the soft
  keyboard visible when a device is available.
- **Expected:** all 12 lessons and their recaps are reachable. Empty-bank state
  explains that no questions are published and points learners to vocabulary
  study. With a fixture bank, approved models and kana are shown after comparison;
  feedback remains neutral about other possible answers, retry replays the same
  prompt once, input clears, and navigation resets the round. No horizontal
  clipping or keyboard-obscured essential controls.
- **Automation:** `GenkiComponentTests` and `GenkiBrowserTests` cover the empty bank,
  fixture-backed practice, comparison, retry, navigation and responsive behavior.
  Native Android IME, Back, offline cold start/package behavior and human
  readability still require device observation.

## GENKI-002 — Curriculum, forms and generated language

- **Purpose:** catch curriculum, conjugation and natural-language errors that
  structural validation cannot prove.
- **Preconditions:** reviewed lesson JSON, source vocabulary and the third-edition
  source textbook. For batch inspection use an isolated state directory and a
  small schema/word selection with a run limit; do not run the full corpus as a
  regression check.
- **Steps:** for each changed point, compare its scope and cited pages with the
  source grammar and expression notes. Review a controlled sample of generated
  combinations for English/Japanese/kana agreement, verb arguments, counting,
  tense, register and optionality. Check that vocabulary help supplies required
  lexical knowledge without supplying the conjugated answer, and that accepted
  alternatives remain available for learner review. For form changes, check the
  literal conjugation oracles for verb families, て/た forms, ある and 来る,
  i/na/いい adjective forms, and the Genki-specific desire, stem, counter and
  request-ending forms.
- **Expected:** explanations cover the intended source point; prompts naturally
  elicit that grammar; generated samples do not combine incompatible meanings or
  introduce future forms; forms match independent Japanese and kana expectations.
  Full exhaustive generation remains an explicit operator action after the
  implementation and controlled samples have been reviewed.
- **Automation:** `GenkiTests`, `GenkiPipelineTests` and `GenkiFormsTests` cover
  source references, schemas, typed substitutions, pipeline behavior and form
  rendering; `VerbConjugationTests`, `AdjectiveConjugationTests` and
  `AdjectiveLanguageOracleComponentTests` cover literal language expectations.
  `GenkiSourceCoverageIntegrationTests` checks schema forms and fixed vocabulary
  against the source catalogs without generating questions.
  Human language judgment remains necessary for newly authored lexical meanings,
  tags, schemas, recaps and pragmatics.

## GENKI-003 — PC Studio setup and controlled generation

- **Purpose:** protect the local configuration, bounded work controls, visible
  progress, restart behavior and explicit learner-bank publication in Genki Studio.
- **Preconditions:** Windows with PowerShell 7, the .NET 10 SDK and a current browser. Use
  `Start Genki Studio.cmd`; the app binds to loopback at `http://127.0.0.1:5278`.
  Point Data root at the source vocabulary. Set State root and Publish path to
  disposable locations outside the source tree. Model tagging and generation
  additionally require the optional AiLibrary checkout and configured local GGUF
  models. Without them, setup, coverage scans and review remain available.
- **Steps:** save the configuration and reload the page to check the paths persist.
  Select a small word group and bounded tag run when model support is installed;
  inspect uncertain/failing results. Choose one grammar pattern and two compatible
  words, set a low limit, and run a coverage scan. Review the partial visited and
  unseen counts; raise the limit and rescan the scope until it reports exact
  coverage. Compare the pattern's Not queued and Queued filters. If model support
  is available, queue a one-candidate generation run, inspect its progress and
  saved stage results, then exercise pause/resume or cancel. Restart Studio and
  confirm interrupted work is paused until explicitly resumed. Review publication
  and cancel; compare the disposable bank before and after. If completed questions
  are available, confirm publication to the disposable path and inspect the result.
  Press Ctrl+C in the launcher console to stop its server.
- **Expected:** startup queues and resumes no work. The configuration remains
  saved after reload. A partial scan leaves unseen candidates uncounted; completed
  coverage is exact for its recorded scope, and changes to its inputs make the
  previous scan stale. Queue status tracks generation work while scan jobs update
  scan coverage separately. Pausing or cancelling retains completed stages.
  Publication review names the configured destination, cancellation leaves the
  bank unchanged, and confirmation writes only to the disposable destination.
  Studio is reachable only through loopback and stopping the launcher ends its
  server without stopping an unrelated process.
- **Automation:** `GenkiStudioTests` covers isolated coordinator/configuration and
  publication boundaries. `GenkiStudioBrowserTests` covers saved paths, bounded
  model-free scans, filters, review pagination, cancelled publication and localhost host/origin
  checks. Live model quality, operating-system browser launch and manual shutdown
  observation remain host checks.
