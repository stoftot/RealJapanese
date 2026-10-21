# Reviewed vocabulary audio

Audio Studio exports selected word recordings here. The study web and Android
applications do not load or package these exports yet.

`manifest.json` is an array of current exports keyed by each entry's `word.key`
(`dataset/id`). Entries retain the catalog snapshot, engine, source run, output
quality, relative file path, SHA-256, export time and transcript check. Audio files
use immutable names under `<dataset>/<id>/`; replacing a choice updates the
manifest and keeps older payloads. Consumers should follow the manifest rather
than enumerate all recordings.

Generation runs and references remain in `.tooling/audio-studio/`. Pitch profiles
live separately in `tooling/audio-studio/pitch/profiles.json`. See the
[Audio Studio guide](../../../docs/audio-studio.md) for operation and storage.
