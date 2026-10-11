# Vocabulary catalogs

The application separates nouns, verbs, adjectives, adverbs, expressions,
pronouns, determiners, counters, conjunctions, particles and suffixes. Each has
a `Word` subclass, typed repository, JSON catalog and independent study progress.
`Words/Words.json` is the general fallback; the bundled entries have all been
classified, so it starts empty. The frontend keeps Nouns, Verbs and Adjectives as
separate study areas. **Words & expressions** (`/vocabulary`) combines the other
eight grammatical categories and general Words in one editor and practice area.
The original category routes remain aliases for this combined area. This is a
presentation grouping: backend categories, catalog IDs and saved progress stay separate.

The **Words** page (`/words`) mixes saved vocabulary using a toggle for each of
the eleven grammatical categories. The fallback Words catalog remains in Words &
expressions and is excluded from the mixer. All toggles start enabled
unless the URL specifies a selection. Each enabled category contributes only its
saved Known, Rehearsing and Training entries to the corresponding read-only list.
Toggling, searching and collapsing never assign or remove saved words. Manage
assignments in Nouns, Verbs, Adjectives or Words & expressions.

The `sources` query carries the selected dataset names through spelling,
flashcards, back links and reloads; `none` means no categories. Unknown names are
ignored, including the fallback `Words` key in older links. The study-list `category`
query still chooses Known, Rehearsing or Training.
The combined views distinguish entries by dataset plus original ID, even when
two catalogs use the same ID. Their temporary display IDs are never saved.

## Book source and coverage

The bundled GENKI I third-edition vocabulary comes from the introductory greetings
list on printed page 32 and the opening Vocabulary lists immediately after the
dialogues in lessons 1–12. Contiguous Additional
Vocabulary sections and individually listed indented examples are included.
The back dictionary and vocabulary panels elsewhere in lessons are excluded.
Existing extras remain available with `self added` provenance.

[The source manifest](../tests/fixtures/genki-vocabulary.json) identifies the PDF
by title and SHA-256 and records the physical/printed page, section, reading and
catalog target for each of 678 lesson rows and 19 introductory greetings. A row can have multiple targets when
it lists different readings. Identical words combine their English senses:
彼女 includes “she; girlfriend” under Nouns, and 聞く combines listening,
hearing and asking under the earliest entry, Verbs ID 2. Inflected examples such
as 知っています are expressions rather than dictionary-form verbs.

Categories follow the sense used in the lesson. In particular, the publisher's
dictionary identifies temporal [いつ as a pronoun](https://kotobank.jp/word/何時-433890),
price-related [いくら as a noun](https://kotobank.jp/word/幾ら-431526), and
negative-polarity [何も as an adverb](https://kotobank.jp/word/何も-668836).
These references clarify classification; they do not supply additional vocabulary.

The catalog has 698 records, including 15 retained extras. Category checks cover
all catalog IDs, conjugation types and source-manifest references. The original
413 records retain their identities; 101 move to another grammatical category.

## Origins and selection

`origin` defaults to `self added`. A book entry uses `<book name>-<lesson number>`,
for example `GENKI I (3rd edition)-1`. The final hyphen separates the numeric lesson,
so book names may contain hyphens. Repeated vocabulary keeps a single catalog ID
and lists its other lesson origins in `additionalOrigins`.
Named introductory sections use `<book name>-Introduction: <section name>`.
The page-32 entries use `GENKI I (3rd edition)-Introduction: Greetings & introductions`,
retaining their existing Expressions IDs and study assignments.

Selectors group by book, placing named introductory sections before numbered lessons,
with self-added or blank origins under General. Other origins without a numeric lesson
appear under that source's Other words.
Expansion and selection are separate controls. A book selects its eligible words
across all lessons, including collapsed lessons; lesson selection stays within
that book. Repeated words are counted and saved once. A partial group has a mixed
selection state. During search, group actions affect matching words only.

Editable group changes use a single atomic progress update per backend dataset.
Known, Rehearsing and Training
remain exclusive; each list continues to exclude words already assigned above it.
Read-only mixer groups allow expansion and search but have no assignment controls.

## Identity and compatibility

Catalog IDs connect words to study progress and extracted kanji relations. Keep
IDs stable when editing entries. Allocate additions above the dataset's previous
maximum, including retired IDs; never recycle an ID for a different word.

Verbs ID 61 is retired into ID 2 (聞く). Pronouns IDs 21 and 22 are retired into
Nouns IDs 348 (彼女) and 349 (彼), respectively. These combined entries belong to
Nouns and keep their first book appearance in Lesson 12. Their original noun IDs
are retained, including when loading progress from the earlier pronoun-only merge.
Loading local or legacy progress remaps these IDs when using the merged catalog;
the next save persists them. If both entries
were assigned, Known takes priority over Training, then Rehearsing. History follows
the winning category, preferring the retained ID's history when both entries had that category.
Recognized previous catalog recovery snapshots receive the same remapping;
unknown catalog hashes remain incompatible. The frozen earlier split manifests
are unchanged. Matching kana alone does not identify duplicates: for example,
いる meaning “to exist” and いる meaning “to need” have different conjugations.

Progress and sync snapshots use schema version 4 with fourteen datasets (twelve
vocabulary catalogs and two kanji catalogs). Version 1 local progress first uses
the frozen noun-split manifest, then version 2 uses the frozen
[`VocabularySplit.json`](../RealJapanese/Repositories/Sync/VocabularySplit.json)
mapping. Current saves never run either ID migration again. Versions 1, 2 and 3
are read as version 4 and the current version is persisted atomically on the next
change or import. Version 3 had no rehearsal history, so its history starts empty.

Bundled legacy `SavedData.json` files already contain the new IDs. Their root
`LegacyProgressVersion.json` marker identifies version 3. This legacy marker stays
at version 3 when unified progress moves to schema 4. Keep the marker with
those files when copying them. Older legacy layouts are recognized by the absence
of that marker and the presence/absence of the Nouns save. New category saves
without the marker are rejected rather than interpreted with the wrong IDs.
Runtime migration leaves legacy files untouched.

Per-word `Rehearsal` history is persisted with progress. The [architecture
contract](ai/ARCHITECTURE.md#vocabulary-selection-and-practice) owns which routes
update it and the calendar-day rules.

Recognized older recovery snapshots migrate too; unknown catalog hashes remain
unchanged and cannot be restored into an incompatible catalog. Both devices must
use the same current catalogs to sync. Android packages catalogs only and refreshes
them independently of its private progress. The source manifest and legacy saves
are not packaged. See [local sync](local-sync.md) for transfer behavior.
