# Standalone TTS comparison

## TTS-001 — Single-word generation and reference comparison

**Purpose:** Detect wrong readings, missing/extra speech, poor reference matching
and destructive output behavior that valid WAV files alone cannot establish.

**Preconditions:** Follow [TTS setup](../../docs/tts-comparison.md). Use existing
models and a complete single-speaker reference excerpt with a checked transcript.
Keep the AI library checkout read-only to this consumer. Use a fresh run directory.

**Steps:**

1. Run `--dry-run`; confirm IDs 0–10, 11 unique verbs and 33 expected recordings.
   Confirm no new output directory, model process, or catalog/progress write.
2. Run generation with all engines and no fallback flag. Confirm the same owned
   worker remains loaded between healthy requests within an engine batch and is
   disposed at the end of that batch. A failed worker may be replaced.
   Open `index.html`.
3. Compare each processed clip to its raw recording. Confirm exactly the named
   dictionary-form word, the displayed kana reading, audible initial/final sounds,
   natural Japanese accent, no repeated word/carrier sentence, and no intrusive
   clicks, hiss or clipping. Review warnings and the Irodori upper-duration bound.
4. Alternate the reference with Fish and Irodori. Judge speaker identity, pitch,
   pacing and conversational tone. BERT is a fixed voice: compare naturalness and
   pronunciation without treating it as reference cloning. Try another fixed
   BERT voice in a separate run if needed.
5. Attempt the same output directory again. Expect refusal and identical existing
   output hashes. Inspect the manifest for actual reference modes, inputs, settings,
   DLL hashes and errors. Verify catalog/progress files stayed unchanged.
6. With a separate settings file lacking Fish's transcript, run Fish only. Expect
   an explicit failed/missing result. With `--allow-unconditioned` and another new
   output directory, expect a clearly labeled fallback, never a reference claim.
7. Cancel a longer run. Expect worker disposal and retention of completed clips;
   rerun into a new directory. Remove only disposable comparison output if desired.

**Expected result:** All requested clips exist and pass listening criteria, modes
are truthful, failures are explicit, and existing inputs/output are preserved.
Automated PCM/catalog tests and waveform metrics do not pass steps 3–4. ASR may
flag possible errors but cannot establish pitch accent or voice identity. Record
execution outcomes in the current task, not in this reusable case.
