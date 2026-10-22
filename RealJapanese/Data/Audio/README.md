# Reviewed vocabulary audio

Audio Studio exports selected word recordings here. The study web and Android
applications load reviewed exports for Listening practice. Web reads this tree
under its configured `CatalogRoot`; Android packages the manifest and referenced
payloads, then installs them under its private `Catalog/Audio` directory. Audio
Studio itself is not part of either study host at runtime.

`manifest.json` is an array of current exports keyed by each entry's `word.key`
(`dataset/id`). Entries retain the catalog snapshot, engine, source run, output
quality, relative file path, SHA-256, export time and transcript check. Audio files
use immutable names under `<dataset>/<id>/`; replacing a choice updates the
manifest and keeps older payloads. Consumers should follow the manifest rather
than enumerate all recordings.

The shared `StudyAudioCatalog` accepts only entries with a supported relative
`.opus`, `.mp3` or `.wav` path. It matches the snapshot by composite
`dataset/id` plus trimmed Japanese and kana text and excludes payloads that are
missing, empty, unreadable or larger than 16 MiB. The quiz reads one current clip
on demand into a browser audio data URL; it does not cache all audio in memory or
write progress. On Android, packaged immutable payloads are copied before the
manifest is atomically replaced. New exports require rebuilding and installing
the Android package to make them available offline.

Generation runs and references remain in `.tooling/audio-studio/`. Pitch profiles
live separately in `tooling/audio-studio/pitch/profiles.json`. See the
[Audio Studio guide](../../../docs/audio-studio.md) for operation and storage.
