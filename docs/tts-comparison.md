# Standalone verb TTS comparison

`RealJapanese/GenerateAudioUsingTTS` reads `RealJapanese/Data/Verbs/Verbs.json`
and generates separate dictionary-form recordings with Style-Bert-VITS2 JP-Extra,
Fish S2 Pro and Irodori. The default is **IDs 0–10 inclusive: 11 verbs, 33 clips**.
Use `--last-id 9` for exactly ten verbs. It does not connect audio to either host,
edit the catalog/progress, or write into the AI library checkout.

## Prepare the library binaries

Requires .NET 10, installed engine Python environments, downloaded models, and an
existing FFmpeg executable for extracting the reference. The AI library owner
must supply a consistent prebuilt speech SDK bundle with `Seed`, `FishOptions`
and `IrodoriOptions` support and PCM WAV reference decoding. This tool neither
builds that library nor installs/patches its Python environments.

From this repository root, copy the **existing** bundle:

```powershell
.\tooling\prepare-tts.ps1 -LibraryBin 'E:\path\to\Ai library\artifacts\speech-sdk'
dotnet build RealJapanese/GenerateAudioUsingTTS/GenerateAudioUsingTTS.csproj
```

The script copies five required DLLs into ignored `.tooling/tts-ai-library`.
The project uses binary references, never external `ProjectReference`s. Compile
only this project; the older aggregate solution also contains an unrelated
extraction utility with external project references. Re-run preparation only
when deliberately adopting another compatible library bundle.

## Configure and generate

Copy [settings.example.json](../RealJapanese/GenerateAudioUsingTTS/settings.example.json)
to `.tooling/tts-settings.json` and replace its absolute paths. Set the source
recording and an excerpt between 1 and 30 seconds containing one clear speaker.
Set `ReferenceTranscript` to the exact words in **that cropped excerpt** for Fish.
Irodori uses the recording without a transcript. Record automatic transcription
as such in `ReferenceTranscriptSource`; it is not a human-verified transcript.

```powershell
# Inspect IDs/configuration without loading models or creating output.
dotnet run --project RealJapanese/GenerateAudioUsingTTS --no-build -- --dry-run

# Generate all three models into a new timestamped directory.
dotnet run --project RealJapanese/GenerateAudioUsingTTS --no-build

# A focused experiment; output names are never reused/overwritten.
dotnet run --project RealJapanese/GenerateAudioUsingTTS --no-build -- --engines fish --first-id 0 --last-id 2
```

`--settings <json>` selects another configuration. `--output <directory>` must
be a **new child** of `.tooling/tts-runs`; symlink/junction output paths are
rejected. `--engines` takes unique comma-separated names. Ctrl+C cancels the
current request, disposes its model worker and preserves completed files.

The default requires reference conditioning for Fish/Irodori. A missing Fish
transcript or failed inference is recorded as an error, not a successful clone.
Only `--allow-unconditioned` permits a labeled fallback. Such clips and the
reason appear in the manifest and listening report. The engine stops after its
first failed sample if it has produced no successful clips, avoiding repeated
reloads of a broken environment. Other selected engines are still attempted.
Exit 0 means every requested audio file was generated; exit 1 means partial or
failed generation; exit 130 means cancellation. It does not certify pronunciation
or similarity to the reference.

## Single-word settings and limits

- **BERT:** original Japanese text plus the catalog kana reading; fixed M1 voice
  by default (select M1/M2/F1/F2), neutral style, 1.05 duration scale, reduced
  noise/duration variation, and no line splitting or extra pause. BERT cannot
  clone the reference speaker. Its fixed voice is a comparison baseline, not a
  measured best match to the recording.
- **Fish:** kana alone to disambiguate reading, cropped reference plus transcript,
  128 maximum generated tokens, temperature 0.65, top-p 0.7 and repetition penalty
  1.2. These conservative starting settings are not a guarantee against extra
  speech. A token cap is a safety bound, not an exact duration target.
- **Irodori:** kana alone, cropped reference and a separate Japanese caption
  requesting clear, calm, natural one-word delivery. Predicted duration is kept
  at scale 1 and bounded to 0.5–2.5 seconds; checkpoint default sampling steps
  remain in use. Listen for a clipped ending if the upper bound is reached.
- **All:** seed 41 (configurable), one request per verb, no sentence/carrier phrase,
  no appended punctuation or spoken instruction. Models run sequentially to
  avoid unnecessary GPU reloads and memory contention. GPU budgets are Desktop
  for BERT/Irodori and Generation for Fish; `UseGpu: false` explicitly uses CPU.
  A fixed seed improves repeatability but is not a guarantee across runtimes/devices;
  the library's Windows AMD checks specifically found Fish output could vary with
  the same seed.

Each run contains:

| File | Purpose |
| --- | --- |
| `index.html` | Local side-by-side listening report with modes and warnings |
| `manifest.json` | Input words, configuration, actual per-clip requests, DLL hashes, audio metrics and failures |
| `reference.wav` | Mono PCM16 24 kHz excerpt, preserving its transcript alignment |
| `<engine>/raw/000.wav`…`010.wav` | Unmodified engine output at its native rate |
| `<engine>/000.wav`…`010.wav` | Edge-trimmed, peak-normalized comparison clips |
| `runtime/` | Local temporary/cache roots; generated artifacts, not source |

Postprocessing detects activity in 10 ms RMS windows at −50 dBFS, keeps 100 ms
before and 150 ms after it, preserves all internal pauses, and targets a −3 dBFS
peak with at most 3× gain. Silent/invalid PCM is rejected. Long or short output and
source clipping are flagged. Long speech is never chopped to pretend it was one
word. Keep the raw file when judging whether a quiet initial/final consonant was
lost. A valid waveform proves neither correct Japanese pitch accent nor voice
similarity; use [TTS-001](../tests/manual/tts-comparison.md).

Runtime workers inherit `PYTHONDONTWRITEBYTECODE=1`, offline model flags and
temporary/cache paths outside the library. No weights are downloaded during TTS.
On this Windows host, restricted process execution can cause MSBuild discovery
or AMD MIOpen failures; use ordinary local execution with the same paths rather
than editing the library or silently switching engines.

## Optional local transcription

The [Faster Whisper project](https://github.com/SYSTRAN/faster-whisper) provides
local speech recognition. This helper uses CPU int8 inference, leaving GPU memory
for TTS. It does not send audio to a service. Installation/model downloads require
network access and are separate from the offline TTS tool:

```powershell
python -m venv .tooling/tts-transcriber
.\.tooling\tts-transcriber\Scripts\python.exe -m pip install -r tooling/tts-transcriber-requirements.txt
.\.tooling\tts-transcriber\Scripts\python.exe tooling/transcribe-tts-reference.py 'C:\path\reference.webm' --start 25 --seconds 55 --output .tooling/tts-reference/candidates.json --download
```

The first `--download` fetches the multilingual Whisper small model into
`.tooling/tts-asr-models`. Later runs omit it and require cached files. The helper
writes timestamped Japanese segments, word probabilities and `humanVerified:
false`. Select a complete short phrase with silence around its boundaries, then
transcribe the exact crop again to check boundary consistency. Copy its text and
timestamps into the TTS settings. This checks ASR consistency, not correctness by
listening. PyAV 16.0.1 is pinned because PyAV 19 removed an API used by this
Faster Whisper version. Keep ASR environments, model files and recordings out of Git.

## Regression checks

The ordinary unit suite compiles the pure catalog/argument and PCM helpers from
source; it does not reference AI library projects or require models:

```powershell
dotnet test tests/RealJapanese.Tests --filter 'FullyQualifiedName~Tts'
```

Real model behavior and listening criteria are in the manual case. Web/Android
testing is not evidence for this standalone utility and is not required for its
changes.
