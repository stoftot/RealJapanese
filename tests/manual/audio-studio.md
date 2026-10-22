# Audio Studio

These cases are reusable specifications. They do not record that a case has been
executed. Follow [Audio Studio setup and storage guidance](../../docs/audio-studio.md)
and use disposable work/export directories for state-changing checks.

## AUDIO-001 — Catalog selection and saved run choices

**Purpose:** Protect catalog search/group behavior, run restoration and the choice
save workflow.

**Preconditions:** Launch Audio Studio with a disposable work and export root.
Have at least one exported word and at least one word without exported audio.

**Steps:**

1. Search by Japanese, kana, English and one of the four displayed groups. Confirm
   the displayed per-word category is Nouns, Verbs, Adjectives or Words & expressions.
2. Toggle each group and select individual words. Search and use the missing-audio
   filter; confirm selections remain selected when hidden by those filters.
3. Start a run with two to four words and at least one model. While it is active,
   reload the page and try to start another run. Open Runs and confirm the active
   run is restored and its progress continues polling.
4. After generation completes, choose a generated model for a word and choose
   “Don't export this word” for another. Click rapidly between model choices for
   one word, wait for saves to finish, then reload the run.
5. Inspect a run containing a mismatched transcript, no recognized speech or a
   failed transcription. Compare those clips with a matching or pending check.

**Expected result:** Only the four user-facing groups appear as word categories.
An active run is visible after reload and blocks a duplicate generation. The last
rapidly selected model choice is saved; model choices unavailable from a missing
audio URL or failed clip cannot be selected. The deliberate “Don't export” choice
is retained. Unsuccessful checks display a prominent red-tinted banner above the
audio player, with a warning icon, a large bold “Check failed” heading and the
specific reason. Matching and pending checks have no failure banner. Transcripts,
audio controls and export choices remain available as before.

## AUDIO-002 — Export confirmation, model filter and missing-audio refresh

**Purpose:** Verify safe export of selected clips, filter behavior and catalog
refresh after export.

**Preconditions:** Complete a run with two to four words. Leave one word without a
choice, choose an available model for at least one other word, and use an isolated
export root.

**Steps:**

1. Start export and inspect the unselected-word confirmation. Cancel it once and
   confirm no export was created.
2. Export again and acknowledge the warning. Open Exported and test search plus
   All, BERT, Fish and Irodori model filters.
3. Return to Generate with “Only words without exported audio” enabled, or refresh
   that view. Confirm the exported word is no longer shown as missing.
4. Inspect the disposable export directory and manifest before and after exporting
   a replacement choice for a word.

**Expected result:** Unselected words are skipped only after acknowledgement;
cancelling leaves exports unchanged. Search and each model filter show only
matching items. The catalog's missing-audio state updates after export. Exported
payload files have unique names and the manifest is replaced atomically; existing
study catalogs and progress are unchanged.

## AUDIO-003 — Reference upload, transcript source and playback

**Purpose:** Verify local crop preparation, transcript attribution and reference
playback.

**Preconditions:** Have a disposable work root, a short local recording with clear
speech and a configured FFmpeg executable. Keep the recording within local test
storage.

**Steps:**

1. Open References and select the recording. Before its preview finishes loading,
   set the crop length to exactly 3 seconds, then wait for preview metadata and
   confirm the field still reads 3 seconds. Set the start time and prepare the
   reference.
2. Play the saved crop and inspect its transcript and source label. Edit the
   transcript, save it and inspect the source label again.
3. Select this reference in Generate, then inspect its transcript-source label in
   the reference selection.

**Expected result:** The user-entered 3-second crop is retained after metadata
loads and is locally converted to mono 24 kHz PCM WAV; it can be played. The
machine transcript is visibly identified as `AI library Whisper small; not
human-verified`; after editing, its source is shown as “User edited”. The full
source attribution appears both in the reference card and generation selection.
No recording is sent to a remote service.

## AUDIO-004 — Engine and per-clip output-quality matrix

**Purpose:** Exercise all four stored output qualities for each selected engine
and word, per-clip quality selection, export fidelity and legacy-run backfill.

**Preconditions:** Follow the setup guide with the three model environments, model
files, configured shared `whisper-small` model, FFmpeg and a prepared reference. Use
two to four representative words, including at least one longer expression. Use
disposable work and export roots; model generation may take substantial time. Have
a disposable run from the earlier single-format behavior with retained raw clips
for the backfill portion.

**Steps:**

1. Start one bounded run with all three models and the four-word sample. Use a
   reviewed reference for Fish and Irodori. Confirm every generated model clip
   stores Opus 32 kbps mono, MP3 64 kbps mono, WAV 24 kHz/16-bit mono and Original
   WAV, and that each variant has its own transcript/check result.
2. For a word with multiple models, choose different playback qualities per
   model. Use the clip dropdown order Opus, MP3, WAV, Original WAV. Reload the run
   and confirm each word/model quality choice is retained independently. Play each
   selected variant and compare it with the raw run audio.
3. Choose a model for each word and export. Compare the exported payload bytes
   with the exact selected run variant; inspect the manifest's output quality,
   hash, transcript and validation metadata. Change a clip's playback quality,
   reload, and confirm the new choice is used by export.
4. Open the disposable earlier single-format run and click **Prepare all
   qualities**. Confirm it uses saved raw recordings without generating speech
   again, preserves existing variant files, checks and model/quality choices, and
   creates only missing formats. Pause during conversion and confirm the current
   clip's missing formats are checkpointed together before Paused; a conversion
   failure should leave saved artifacts intact, show a run note and leave the run
   Partial for retry.

**Expected result:** Each selected engine returns a playable clip or a visible
failure, and every available model clip contains all four variants with separate
transcripts and validation. The engines run in sequential batches; variants are
checked sequentially through one local Whisper worker. Whisper uses the configured
shared model without downloading it. Processed outputs use edge trimming and peak
normalization, while raw model audio remains available in the run. Quality choices
persist independently for each word/model pair, and export payload bytes and
manifest metadata match that exact selected variant. Backfill calls no TTS model;
missing formats for one clip are checkpointed atomically, while failures remain
visible in run notes and leave a Partial run. ASR checks text only and do not
establish pronunciation, pitch accent or reference similarity.

## AUDIO-005 — Cancellation and interrupted-run recovery

**Purpose:** Ensure cancellation or application restart preserves completed work
and exposes interrupted runs for explicit resume.

**Preconditions:** Use disposable work/export roots and a run long enough to observe
at least one generated clip. Do not use a user's normal export directory.

**Steps:**

1. Start generation and cancel it after at least one clip completes. Confirm the
   cancel control changes to a pending state and run progress continues updating
   until the run reaches a terminal cancelled state. Inspect its retained clips.
2. Start another bounded run. Stop the Audio Studio process while it is active,
   then launch the app again using the same work root.
3. Open Runs, inspect the interrupted run and resume it. Confirm the run uses its
   original settings and reference snapshot and does not replace completed outputs.

**Expected result:** Cancellation remains visibly pending while the active
operation stops; polling does not freeze at “Cancelling”. Completed outputs are
preserved. Startup marks an active run as interrupted and retains completed clips;
it does not resume automatically, but the user can explicitly resume the run. A
cancelled run cannot be resumed.
No cleanup touches study progress, catalogs, another project, or the external
speech-library checkout.

## AUDIO-006 — Human pronunciation, pitch and voice review

**Purpose:** Check qualities that waveform processing and automatic text checks
cannot establish.

**Preconditions:** Have completed clips and the reference recording available.
For the BERT check, use a run generated with the stored pitch-profile collection
available. Use headphones or another reliable listening setup and a reviewer able
to judge Japanese pronunciation.

**Steps:**

1. Listen to every selected raw and processed clip. Compare the spoken word with
   its Japanese text and kana. Check for extra speech, repetition, clipped
   consonants/endings, clicks, silence, clipping and unnatural timing.
2. Compare Fish and Irodori with the reference for perceived voice, pacing and
   tone. Assess BERT as a fixed-voice baseline, not as reference cloning.
3. Judge pitch accent by listening and appropriate Japanese-language expertise;
   do not infer correctness from the ASR transcript or the displayed model pitch
   label. For BERT, confirm a stored-profile clip identifies its source as a
   machine estimate. Fish and Irodori do not use explicit stored profiles.

**Expected result:** Reviewer notes identify any clips unsuitable for export and
the reason. Passing text recognition or producing a valid waveform alone is not
evidence of correct pitch accent, naturalness or voice identity. Stored BERT
profiles are machine estimates, not approved learning references.

## AUDIO-007 — Pause, resume and persisted checkpoints

**Purpose:** Verify safe pause points during generation and transcription,
checkpoint preservation across resume/restart, and terminal cancellation.

**Preconditions:** Use disposable work/export roots and a bounded run with several
words and at least one engine. Ensure there are pending clips and checks. Use a
local model setup that takes long enough to pause during each phase.

**Steps:**

1. Start the run and request Pause while a clip is generating. Wait for the current
   clip operation, conversion and save to finish and for the status to become
   Paused. Record hashes of completed clip files and their completed check state.
2. While paused, confirm another run or reference operation can start. Confirm
   Export is unavailable for the paused run. Finish or cancel that other work,
   then resume the paused run.
3. Let generation finish and request Pause while the status is Checking. Wait for
   the current Whisper file to finish and the status to become Paused. Record the
   completed check files/state and verify export remains unavailable.
4. Stop and restart Audio Studio with the same work root. Confirm the run remains
   Paused and resumable; resume it and let it finish. Compare earlier clip hashes
   and completed-check records with the values recorded before each pause.
5. Pause another bounded run, cancel the paused run, and inspect its terminal
   status and resume control.

**Expected result:** Pause completes the in-flight generation clip including
conversion/save, or the in-flight Whisper file, then unloads the model and releases
generation capacity. No subsequent model request starts until resume. A paused run
retains settings, its reference snapshot, completed outputs and checks; resume
skips the saved work, including after application restart. Previously completed
clip hashes remain unchanged. Paused and Interrupted runs cannot be exported.
Cancelling a paused run marks it terminal `Cancelled` and removes its resume
action. This case is a manual acceptance specification, not an execution claim.
