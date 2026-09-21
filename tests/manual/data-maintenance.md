# Data maintenance integration cases

The duplicate-cleanup guard, legacy ID remapping and stale-ID rejection are
automated by `StorageChecks`. The following cases require a controlled external
model or IO failure, so they remain manual integration procedures. Never run a
maintenance utility against canonical data for validation.

## DATA-001 — Extraction preserves symbol identity and unique relations

### Purpose

Protect the boundary between source vocabulary, existing kanji relations and
generated model data, including reruns and malformed/model-mismatched responses.

### Preconditions

- The extraction utility's external project references and local model/server are
  available; see [development](../../docs/development.md#data-maintenance-utilities).
- Create a disposable complete data tree containing a word with repeated kanji
  (for example `日曜日`), one already-known kanji relation, and one new kanji.
  Record the IDs and expected distinct relations in each source dataset.
- Choose a disposable working directory whose resolved `../../../../Data` is
  exactly that copied tree. Verify the absolute path before launching anything.
- For negative variants, use a controllable test model/server that can return
  malformed JSON and then a schema-valid answer with the wrong kanji symbol.

### Steps

1. Run extraction from the verified disposable working directory. Inspect the
   generated catalog and relation files, including the repeated-kanji word.
2. Run again on exactly the same inputs and inspect every relation list again.
3. Restore the initial fixture. Have the controlled model return malformed JSON,
   then exercise its retry/error path with a valid response for the requested symbol.
4. Restore again and return a schema-valid answer for a different symbol. Inspect
   whether it is rejected or whether the wrong symbol enters the saved catalog.

### Expected result

Existing IDs remain stable. Each new symbol has one new ID; relations point to
the correct source IDs once each, including after a repeated run. Invalid responses
must not be saved as valid records, and a generated record's symbol must equal
the requested symbol. Treat an accepted wrong symbol or duplicated relation as a
failure; do not use current output as the oracle. Report unavailable controlled
response variants separately from a successful real-model run.

### Cleanup

Stop only the test-owned model/processes and discard the copied tree after
verifying its absolute path remains within the disposable test directory.

## DATA-002 — Interrupted legacy cleanup preserves a usable catalog/save pair

### Purpose

Expose partial rewrites at the boundary between the word catalog and legacy
progress. The normal successful remap is already automated.

### Preconditions

- Disposable `Data/Words/Words.json` and `SavedData.json`, no `Progress.json`.
- Two duplicate records with old IDs 10 and 11; the save selects ID 11.
- A controlled fixture that lets the utility read SavedData.json but prevents its
  later write (for example a read-sharing-only file handle on Windows).
- Verify that `../../../../Data` from the chosen working directory resolves only
  to this disposable tree, and retain both original files byte-for-byte.

### Steps

1. Activate the save-write failure and run the real cleanup utility against the
   fixture. Record the process outcome and both output files.
2. Verify every saved ID still resolves to the intended word in the resulting
   catalog, or that both original files remain intact.
3. Release the failure, restore both fixture files, rerun and verify the duplicate
   collapses to ID 0 and the selection points to ID 0.

### Expected result

A failed run must not leave a newly numbered catalog paired with old saved IDs.
The subsequent clean run produces the consistent remapped pair. Report a partial
rewrite as a defect rather than altering either production file to satisfy the case.

### Cleanup

Release all fixture handles, terminate only task-owned processes and discard the
verified disposable tree. Do not apply the utility to current unified saves.
