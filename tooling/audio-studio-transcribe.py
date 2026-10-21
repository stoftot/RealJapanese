"""Offline JSON-lines batch transcription for AudioStudio."""

import argparse
import json
import os
from pathlib import Path
import sys
import traceback


def emit(value):
    print(json.dumps(value, ensure_ascii=False, separators=(",", ":")), flush=True)


def transcribe_one(model, decode_audio, numpy, item):
    audio = decode_audio(str(Path(item["path"]).resolve()), sampling_rate=16000)
    if len(audio) == 0:
        raise ValueError("Audio contains no decodable samples.")
    is_reference = bool(item.get("isReference", False))
    if not is_reference:
        # Very short isolated words decode more consistently with modest edge context.
        audio = numpy.pad(audio, (int(0.3 * 16000), int(0.5 * 16000)))
    segments, _ = model.transcribe(
        audio,
        language="ja",
        beam_size=5,
        temperature=0,
        condition_on_previous_text=False,
        vad_filter=is_reference,
    )
    text = "".join(segment.text for segment in segments).strip()
    emit({"id": item["id"], "text": text, "error": None})


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="strict")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models", required=True, type=Path,
                        help="Existing Faster Whisper model cache root; downloads are disabled.")
    parser.add_argument("--cache", required=True, type=Path)
    parser.add_argument("--runtime", required=True, type=Path)
    args = parser.parse_args()

    # Point all known writable caches and temporary files at caller-owned paths
    # before importing libraries that may initialize their own cache helpers.
    os.environ["HF_HOME"] = str(args.cache.resolve())
    os.environ["HF_HUB_CACHE"] = str((args.cache / "hub").resolve())
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
    os.environ["HF_HUB_DISABLE_XET"] = "1"
    os.environ["PYTHONDONTWRITEBYTECODE"] = "1"
    os.environ["PYTHONUTF8"] = "1"
    os.environ["TEMP"] = str(args.runtime.resolve())
    os.environ["TMP"] = str(args.runtime.resolve())
    os.environ["TMPDIR"] = str(args.runtime.resolve())

    try:
        from faster_whisper import WhisperModel
        from faster_whisper.audio import decode_audio
        import numpy

        model = WhisperModel("small", device="cpu", compute_type="int8", cpu_threads=6,
                             download_root=str(args.models.resolve()), local_files_only=True)
    except Exception as error:
        print(f"Could not initialize local Faster Whisper small model: {error}", file=sys.stderr, flush=True)
        return 2

    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue
        item = None
        try:
            item = json.loads(line)
            if not isinstance(item, dict) or not isinstance(item.get("id"), str) \
                    or not isinstance(item.get("path"), str):
                raise ValueError("Input must contain string id and path fields.")
            transcribe_one(model, decode_audio, numpy, item)
        except Exception as error:
            identifier = item.get("id", "") if isinstance(item, dict) else ""
            emit({"id": identifier, "text": None, "error": str(error)})
            print(traceback.format_exc(limit=3), file=sys.stderr, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
