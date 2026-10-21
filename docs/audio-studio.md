# Audio Studio

Audio Studio is a standalone local ASP.NET application for generating, reviewing
and exporting Japanese vocabulary recordings. It reads the twelve catalogs under
`RealJapanese/Data/` (698 entries in total) without changing study data or study
progress. The regular web and Android study hosts do not consume Audio Studio
exports.

## Prepare and launch

Run from the repository root. Audio Studio targets .NET 10 and uses the existing
speech SDK bundle copied by the repository's TTS preparation script:

```powershell
.\tooling\prepare-tts.ps1 -LibraryBin 'E:\path\to\Ai library\artifacts\speech-sdk'
```

The bundle must include the speech SDK DLLs expected by that script and the
Japanese pronunciation-profile APIs (`JapanesePronunciationGenerator` and
`SpeechSynthesisRequest.PronunciationProfile`). Audio Studio
references the standalone `GenerateAudioUsingTTS` project and those copied
binaries; it does not build or modify the external AI library. Create
`.tooling/tts-settings.json` from
[`settings.example.json`](../RealJapanese/GenerateAudioUsingTTS/settings.example.json)
and set the local library, model, engine Python, FFmpeg and reference-recording
paths. Keep machine-specific settings and model files out of Git.

The project-local transcription helper is
[`tooling/audio-studio-transcribe.py`](../tooling/audio-studio-transcribe.py).
It is intentionally retained in place while a requested migration is on hold.
The app invokes it with
`.tooling/tts-transcriber/Scripts/python.exe`; install the pinned packages from
[`tooling/tts-transcriber-requirements.txt`](../tooling/tts-transcriber-requirements.txt)
and make the Faster Whisper `small` model available under
`.tooling/tts-asr-models` before use. The helper runs locally with downloads
disabled; it does not send recordings to a remote transcription service.

Start the local host:

```powershell
dotnet run --project RealJapanese/AudioStudio --no-launch-profile
```

Open <http://127.0.0.1:5291>. The host binds to loopback by default and rejects
non-local requests. Mutating API calls require the `X-Audio-Studio: 1` request
header and a same-origin request. Browser requests are supplied by the app.

For isolated checks, override paths with .NET configuration environment variables.
Work and export directories must remain inside the repository and outside the
external speech-library checkout. For example:

```powershell
$env:AudioStudio__Settings = (Join-Path (Get-Location) '.tooling\tts-settings.json')
$env:AudioStudio__WorkRoot = (Join-Path (Get-Location) '.tooling\audio-studio-checks\work')
$env:AudioStudio__ExportRoot = (Join-Path (Get-Location) '.tooling\audio-studio-checks\exports')
$env:AudioStudio__Url = 'http://127.0.0.1:5291'
$env:AudioStudio__SkipDefaultReference = 'true'
dotnet run --project RealJapanese/AudioStudio --no-launch-profile
```

The environment overrides are `AudioStudio__Settings`, `AudioStudio__WorkRoot`,
`AudioStudio__ExportRoot`, `AudioStudio__Url` and
`AudioStudio__SkipDefaultReference`. Use disposable paths for testing; do not point
them at user data, the study progress files, or the external model library.

## Catalog and generation

The catalog combines Nouns, Verbs, Adjectives, Words, Adverbs, Expressions,
Pronouns, Determiners, Counters, Conjunctions, Particles and Suffixes. Its four
user-facing groups are **Nouns**, **Verbs**, **Adjectives**, and **Words &
expressions**. A word's storage identity is the composite `dataset/id`; this
identity keeps repeated IDs in different source files distinct and is not shown
as a category in per-word views. Search and category selection work across the
catalog. The “Only words without exported audio” filter is based on the current
export manifest.

Choose words, one or more of BERT, Fish and Irodori, an optional reference and one
output format to start a run. Runs and their progress are saved. The app keeps a
model worker loaded for its whole engine batch, processes engines sequentially,
then validates all generated clips in a single local Whisper batch. A second run
or reference operation cannot start while generation is active.

Each clip's automatic transcript is a conservative text check against the
catalog's Japanese or kana text. A match does not establish correct speech,
pronunciation, pitch accent, naturalness or similarity to a reference. Review and
listen to every clip before exporting it.

## References

When no saved reference exists, first startup attempts to prepare the configured
default Podcast 41 excerpt at 38.35 seconds for 9.8 seconds. Its transcript is an
automatic candidate, not a human-verified transcript. A reference can also be
uploaded and cropped to 1–30 seconds in References. Audio is converted locally to
mono 24 kHz PCM WAV, played back in the reference list and transcribed locally.
Review or replace its transcript there before using it with Fish. The reference
list and generation selector display the transcript source, including whether it
is user edited or machine transcribed; editing a reference marks its source as
`User edited`.

BERT uses its fixed voice. Fish requires a transcript when a reference is selected.
Fish and Irodori can condition on the chosen reference; that is not a guarantee of
speaker identity or voice quality.

## Choices, exports and files

After generation and checks complete, select one available model clip for each
word or choose “Don't export this word”. Choices are persisted by the run. Export
requires an explicit confirmation when any words have no choice; those words are
skipped. Successful exports refresh the catalog's missing-audio state. The
Exported view can be searched and filtered to All, BERT, Fish or Irodori.

Run manifests and retained clips are stored under
`.tooling/audio-studio/runs/`; prepared references are under
`.tooling/audio-studio/references/`. Exports default to
`RealJapanese/Data/Audio/`. Each export writes a uniquely named immutable audio
payload, then atomically replaces the export manifest. These folders are Audio
Studio state; do not treat them as study progress or edit the vocabulary source
files to manage them.

Every output choice receives edge trimming and constant-volume peak normalization.
`Original WAV` keeps the processed WAV at the model's sample rate; it does not
replace or discard the raw model output, which remains available separately in
the run. The other choices are WAV at 24 kHz/16-bit mono, MP3 at 64 kbps mono, and
Opus at 32 kbps mono. Processing preserves internal pauses, retains 100 ms before
and 150 ms after detected speech, targets a −3 dBFS peak with at most 3× gain, and
rejects silent audio. Listen to both raw and processed clips when a quiet consonant
or word ending may have been trimmed.

Cancelling a run preserves completed clips. If the app process stops while a run
is active, the next startup marks it `Interrupted` and retains its completed
outputs. Runs cannot resume; start a new run for retry words. Study-host catalogs
and progress remain untouched.

## Pitch profiles and spoken forms

The word-linked profile file is
[`tooling/audio-studio/pitch/profiles.json`](../tooling/audio-studio/pitch/profiles.json).
It contains 698 usable machine-estimated JP-Extra phone/tone profiles for the
698-word catalog. Regenerate candidates with:

```powershell
dotnet run --project RealJapanese/AudioStudio --no-launch-profile -- --prepare-pitch
```

The candidates are reproducible and editable profile data, not human-verified
pronunciation or pitch judgments. Preparation preserves profiles marked as
user-edited or human-verified. During generation, BERT consumes the matching
stored profile when available and records a copy beside that run's clip. Fish and
Irodori cannot use these explicit profiles; their outputs remain model-estimated.
Listening and knowledgeable Japanese review are still needed before using an
accent as a learning reference.

Audio Studio constructs spoken forms without changing the source catalogs. It
removes `～` or `〜` placeholder markers from speech input and
uses `ティーシャツ` as the spoken kana for `Tシャツ`, while displaying the
original catalog text and reading. Placeholder words are spoken in isolation;
their accent may change when used in context, so check the accompanying speech
note and review contextual readings manually.

## Regression coverage

Use the reusable [Audio Studio manual cases](../tests/manual/audio-studio.md) for
the browser workflow, model/format combinations, cancellation and human listening
checks. The cases are specifications, not an execution record. No automated or
manual run outcome is implied by this guide.
