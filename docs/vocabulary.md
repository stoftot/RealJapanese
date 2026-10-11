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
the twelve backend categories, including General words. All toggles start enabled
unless the URL specifies a selection. Each enabled category contributes only its
saved Known, Rehearsing and Training entries to the corresponding read-only list.
Toggling, searching and collapsing never assign or remove saved words. Manage
assignments in Nouns, Verbs, Adjectives or Words & expressions.

The `sources` query carries the selected dataset names through spelling,
flashcards, back links and reloads; `none` means no categories. Unknown names are
ignored. The study-list `category` query still chooses Known, Rehearsing or Training.
The combined views distinguish entries by dataset plus original ID, even when
two catalogs use the same ID. Their temporary display IDs are never saved.

## Book source and coverage

The bundled GENKI I third-edition vocabulary comes from the opening Vocabulary
lists immediately after the dialogues in lessons 1–12. Contiguous Additional
Vocabulary sections and individually listed indented examples are included.
The back dictionary and vocabulary panels elsewhere in lessons are excluded.
Existing extras remain available with `self added` provenance.

[The source manifest](../tests/fixtures/genki-vocabulary.json) identifies the PDF
by title and SHA-256 and records the physical/printed page, section, reading and
catalog target for each of 678 printed rows. A row can have multiple targets when
it lists different readings or grammatical senses. For example, 聞く has listening
and asking senses; 彼女 has pronoun and girlfriend senses. Inflected examples such
as 知っています are expressions rather than dictionary-form verbs.

Categories follow the sense used in the lesson. In particular, the publisher's
dictionary identifies temporal [いつ as a pronoun](https://kotobank.jp/word/何時-433890),
price-related [いくら as a noun](https://kotobank.jp/word/幾ら-431526), and
negative-polarity [何も as an adverb](https://kotobank.jp/word/何も-668836).
These references clarify classification; they do not supply additional vocabulary.

The catalog has 701 records, including 34 retained extras. Category checks cover
all catalog IDs, conjugation types and source-manifest references. The original
413 records retain their identities; 101 move to another grammatical category.

## Origins and selection

`origin` defaults to `self added`. A book entry uses `<book name>-<lesson number>`,
for example `GENKI I (3rd edition)-1`. The final hyphen separates the numeric lesson,
so book names may contain hyphens. Repeated vocabulary keeps a single catalog ID
and lists its other lesson origins in `additionalOrigins`.

Selectors group by book and numeric lesson, with self-added or blank origins under
General. An origin without a numeric lesson appears under that source's Other words.
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

Progress and sync snapshots use schema version 3 with fourteen datasets (twelve
vocabulary catalogs and two kanji catalogs). Version 1 local progress first uses
the frozen noun-split manifest, then version 2 uses the frozen
[`VocabularySplit.json`](../RealJapanese/Repositories/Sync/VocabularySplit.json)
mapping. Current saves never run either migration again. Loading performs these
steps in memory; the next edit/import persists the current version atomically.

Bundled legacy `SavedData.json` files already contain the new IDs. Their root
`LegacyProgressVersion.json` marker identifies version 3. Keep the marker with
those files when copying them. Older legacy layouts are recognized by the absence
of that marker and the presence/absence of the Nouns save. New category saves
without the marker are rejected rather than interpreted with the wrong IDs.
Runtime migration leaves legacy files untouched.

Recognized older recovery snapshots migrate too; unknown catalog hashes remain
unchanged and cannot be restored into an incompatible catalog. Both devices must
use the same current catalogs to sync. Android packages catalogs only and refreshes
them independently of its private progress. The source manifest and legacy saves
are not packaged. See [local sync](local-sync.md) for transfer behavior.
