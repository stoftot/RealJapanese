# Genki grammar practice

Open **Genki** in the navigation, choose a lesson, and read its grammar recap.
Practise the whole lesson or an individual grammar point. Each round includes
every selected pattern, with fresh vocabulary combinations. Write a Japanese
sentence, compare it with the models, and choose to continue or practise it again.
Vocabulary help gives dictionary forms. Kana and kanji model answers are available.

Comparison recognizes supplied models after normalizing spacing and punctuation.
It does not grade arbitrary Japanese: other word orders, synonyms and appropriate
omissions may be valid. The learner reviews meaning, form and register. Retry
replays the same prompt later in the round. Practice state is temporary and is not
saved to `Progress.json`, scored as vocabulary mastery, or transferred by local sync.

## Curriculum and source

The authoritative curriculum is the supplied **GENKI: An Integrated Course in
Elementary Japanese I, third edition** (2020, ISBN 978-4-7890-1730-5).
The 12 conversation/grammar lessons run from printed pages 36–293; their numbered
grammar sections are listed on printed pages 6–10. The supplied 393-page PDF has
nine front-matter images before printed page 1: PDF page = printed page + 9.
`sourcePages` always records printed page numbers.

All numbered grammar sections have their own recap and production patterns.
Productive expression-note grammar appears in the associated point or as a named
supplementary point. The reading/writing section pairs with these same 12 lessons;
it adds script/kanji practice rather than another grammar sequence. Kana study
links to the existing module. Culture essays, dialogues, illustrations, reading
passages and the textbook's exercise sets are not reproduced. The application's
recaps, examples, prompts and lexical combinations are original companion content.
The source PDF and extracted page images are not application assets.

Core point IDs follow the textbook's section numbering (`g03-03` is lesson 3's
particles section). Supplementary points start at section 09. Lesson titles in the
application describe their practice themes. Companion vocabulary levels are
explicit authoring choices, not a transcription of the textbook's vocabulary lists.

## Ownership and delivery

- [Models](../RealJapanese/DataLoaders/Models/Genki/GenkiLesson.cs) belong to
  DataLoaders; `GenkiLexeme` extends the existing `Word` record.
- [Lesson JSON](../RealJapanese/Repositories/Genki/Content/) is embedded in
  Repositories. Both web and Android load the same offline assets; there is no new
  mobile installation path or dependency on the PDF at runtime.
- [Catalog](../RealJapanese/Repositories/Genki/GenkiCatalog.cs) validates unique
  IDs, prerequisites, templates and required content when loaded.
- [Generator](../RealJapanese/Repositories/Genki/GenkiGenerator.cs) selects compatible
  words, renders the three language fields, and returns the existing
  `QuestionAnswerDto` inside a richer exercise carrying context and model variants.
- [Shared page](../RealJapanese/RealJapanese.UI/Components/Pages/Genki/Genki.razor)
  uses `PracticeShell` and `PracticeCard`; session state belongs to the component.

## Generation contract

Each lesson contains `grammarPoints` and `lexicon`. Each grammar point has meaning,
formation, nuances, original examples, source pages, prerequisites and schemas.
Every point must have a `translation` schema. Additional `transformation` and
`response` patterns focus on conjugation and conversational use.

A schema is a reusable sentence frame with parallel `english`, `japanese` and
`kana` templates, context, instruction, register, tense, polarity, particles,
omission guidance and restrictions. Slots declare their word type, required tags
and forms. `{item}` inserts the base word; `{action:politePast}` inserts a reviewed
form. Context/instruction placeholders render in English. Literal particles and
endings belong in the template. English lexical forms include the article,
inflection or plural needed by the specific template.

`relations` are enforced while assigning slots:

| Kind | Rule |
| --- | --- |
| `distinct` | Left and right must have different Japanese base text. |
| `accepts` | At least one tag of the right entry must occur in the left entry's `accepts` list. |

Tags on a slot are an **all-of** requirement. Narrow tags restrict animate beings,
countable flat objects, compatible features/adjectives, voluntary actions, motion
goals and other roles. Transitive verb/object pairs use compatible semantic tags
and/or `accepts`. A restriction written only in prose is not a substitute for a
typed slot or relation when it affects which combinations are legal.

Each schema emits a complete model. Optional topics/arguments are documented in
`omission`; reviewed alternative templates may omit them. Substantive changes in
register, tense, polarity or construction use separate schemas, keeping each
English cue aligned with its answer. Free optional deletion is not performed.

`prerequisites` reference earlier grammar point IDs (including earlier points in
the current lesson). The target point itself is implicitly being introduced, so
it is not its own prerequisite. The default vocabulary ceiling is the target
lesson. Choosing a later vocabulary ceiling expands eligible words without
advancing the grammar ceiling. Non-base forms carry their own `introducedLesson`
and remain gated by the grammar lesson, even with expanded vocabulary.

## Adding vocabulary and patterns

1. Add a unique `lNN-...` lexical key with `japanese`, `kana`, `english`, an explicit
   `wordType`, an introduction level and reviewed semantic tags. Reuse an existing
   role tag when the new word genuinely fits every pattern using it. Do not infer
   transitivity or argument compatibility from the existing freeform `Category`.
2. Supply reviewed forms, including English realizations and introduction levels.
   Base words are automatic. Verb dictionary forms may appear as vocabulary help
   before lesson 8, but standalone casual predicates must remain lesson 8 patterns.
   Plain adjective negatives start in lesson 8; plain past predicates in lesson 9.
3. Existing `Word`, `Verb` and `Adjective` records can be wrapped using
   `GenkiLexeme.FromWord` with metadata reviewed for that word. The wrapper preserves
   the reviewed English realization, including template-required articles and
   possessives, while copying the dictionary word's identity and Japanese text.
   The generator accepts additional lexemes; new matching words participate without
   changing templates.
   Existing known/training lists are not imported blindly: they lack the semantic
   and valency metadata needed to guarantee natural sentences.
4. Review **all** combinations of each affected pool, including broader vocabulary
   ceilings. Check the Japanese, kana, English agreement, context, register and
   source progression. Supply alternatives only when their meaning really matches.
5. Run `GenkiTests` and the Genki component/browser tests; see [testing](testing.md).
   The unit suite checks every packaged pattern across deterministic seeds at both
   its own and the widest vocabulary ceiling. Human language review remains part
   of content authoring, as documented in [manual cases](../tests/manual/genki.md).

Forms are deliberately curated data. The older general conjugators have documented
language defects and are not called by this generator. This avoids introducing
incorrect answers into the new module while keeping unrelated conjugation behavior
unchanged. Fixing those APIs is separate work; use independent language oracles
before substituting them for reviewed forms.
