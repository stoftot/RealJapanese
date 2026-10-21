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

**Expected result:** Only the four user-facing groups appear as word categories.
An active run is visible after reload and blocks a duplicate generation. The last
rapidly selected model choice is saved; model choices unavailable from a missing
audio URL or failed clip cannot be selected. The deliberate “Don't export” choice
is retained.

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
machine transcript is visibly identified as not human-verified;
after editing, its source is shown as “User edited”. The full source attribution
appears both in the reference card and generation selection. No recording is sent
to a remote service.

## AUDIO-004 — Engine and output-format matrix

**Purpose:** Exercise every engine and output format on a bounded set of words,
including raw and processed audio.

**Preconditions:** Follow the setup guide with the three model environments, model
files, local Whisper small model, FFmpeg and a prepared reference available. Use
two to four representative words, including at least one longer expression. Use
disposable work and export roots; model generation may take substantial time.

**Steps:**

1. For each of the four formats (Original WAV, WAV 24 kHz, MP3 64 kbps and Opus
   32 kbps), generate a bounded run using BERT, Fish and Irodori. Use a reviewed
   reference for Fish and Irodori. Keep the same two-to-four-word sample across
   runs so outputs can be compared.
2. Listen to each generated clip and compare its processed audio with its raw run
   audio. Inspect the automatic transcript check and any error or warning.
3. Select one available clip per word and export it from at least one run. Confirm
   the selected format and model in Exported.

**Expected result:** Each selected engine returns a playable clip or a visible
failure. The engines run in sequential batches, with each worker held for its
batch; validation processes the batch in one local Whisper pass. Processed outputs
use edge trimming and peak normalization, while raw model audio remains available
in the run. WAV, MP3 and Opus exports have the requested encoding. ASR checks text
only and do not establish pronunciation, pitch accent or reference similarity.

## AUDIO-005 — Cancellation and interrupted-run recovery

**Purpose:** Ensure cancellation or application restart preserves completed work
without falsely resuming a run.

**Preconditions:** Use disposable work/export roots and a run long enough to observe
at least one generated clip. Do not use a user's normal export directory.

**Steps:**

1. Start generation and cancel it after at least one clip completes. Confirm the
   cancel control changes to a pending state and run progress continues updating
   until the run reaches a terminal cancelled state. Inspect its retained clips.
2. Start another bounded run. Stop the Audio Studio process while it is active,
   then launch the app again using the same work root.
3. Open Runs, inspect the interrupted run, and start a new run for any retry.

**Expected result:** Cancellation remains visibly pending while the active
operation stops; polling does not freeze at “Cancelling”. Completed outputs are
preserved. Startup marks an active run as interrupted and retains completed
clips; generation does not resume automatically. A retry is a separate new run.
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
