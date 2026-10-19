"""Optional local ASR helper. Installs/downloads nothing except an explicitly requested model.

Run using the isolated tts-transcriber environment described in docs/tts-comparison.md.
ASR output is a candidate transcript, not proof of correct transcription or voice similarity.
"""
import argparse
from dataclasses import asdict
import json
import os
import stat
import sys
from pathlib import Path


def reject_links(path):
    for item in (path, *path.parents):
        if item.is_symlink() or (item.exists() and getattr(item.stat(), "st_file_attributes", 0)
                                 & stat.FILE_ATTRIBUTE_REPARSE_POINT):
            raise ValueError(f"Output/cache paths must not traverse symlinks or junctions: {item}")


def main():
    # Windows redirected terminals may default to a code page without Japanese.
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("audio", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--start", type=float, default=25)
    parser.add_argument("--seconds", type=float, default=45)
    parser.add_argument("--model", default="small")
    parser.add_argument("--download", action="store_true", help="Explicitly allow downloading the ASR model")
    args = parser.parse_args()
    if args.start < 0 or not 1 <= args.seconds <= 120:
        parser.error("Use nonnegative start and 1–120 seconds.")
    root = Path(__file__).resolve().parent.parent
    tooling = root / ".tooling"
    output = args.output.absolute()
    for writable in (output, tooling / "tts-asr-cache", tooling / "tts-asr-models"):
        reject_links(writable)
    output = output.resolve()
    if not output.is_relative_to(tooling.resolve()):
        parser.error("Output must be inside this repository's .tooling directory.")
    if output.exists():
        parser.error("Output already exists; choose a new filename.")
    os.environ["HF_HOME"] = str(tooling / "tts-asr-cache")
    os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"
    os.environ["HF_HUB_DISABLE_XET"] = "1"
    from faster_whisper import WhisperModel
    from faster_whisper.audio import decode_audio

    audio = decode_audio(str(args.audio), sampling_rate=16000)
    excerpt = audio[round(args.start * 16000):round((args.start + args.seconds) * 16000)]
    if len(excerpt) < 16000:
        parser.error("Selected excerpt is empty/too short.")
    model = WhisperModel(args.model, device="cpu", compute_type="int8", cpu_threads=6,
                         download_root=str(tooling / "tts-asr-models"), local_files_only=not args.download)
    segments, info = model.transcribe(excerpt, language="ja", beam_size=5, temperature=0,
                                      condition_on_previous_text=False, word_timestamps=True, vad_filter=True)
    result = {"model": args.model, "source": str(args.audio.resolve()), "start": args.start,
              "seconds": args.seconds, "language": info.language, "humanVerified": False, "segments": []}
    for segment in segments:
        item = asdict(segment)
        item["start"] += args.start
        item["end"] += args.start
        if segment.words:
            item["words"] = [dict(asdict(w), start=w.start + args.start, end=w.end + args.start) for w in segment.words]
        result["segments"].append(item)
        print(f'{item["start"]:.2f}–{item["end"]:.2f}: {segment.text}', flush=True)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8") as file:
        json.dump(result, file, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
