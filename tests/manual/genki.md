# Genki companion regression cases

## GENKI-001 — Lesson and sentence practice

- **Purpose:** protect complete lesson access and the production/review loop.
- **Preconditions:** application running with its packaged curriculum; no saved
  vocabulary is required. Use a disposable Android installation for device checks.
- **Steps:** open Genki; choose lessons 1 and 12; read a recap and its kana example;
  start one point; enter a Japanese sentence with the native IME; compare with the
  model; choose retry; finish the remaining patterns and the repeated prompt;
  navigate to another lesson; repeat with the soft keyboard visible.
- **Expected:** all 12 lessons are reachable, source references and examples are
  readable, model comparison does not reject all alternative wording as incorrect,
  retry returns the same prompt, input clears between prompts, and navigation
  resets the round. No horizontal clipping or keyboard-obscured essential controls.
- **Automation:** `GenkiComponentTests` and `GenkiBrowserTests` own desktop round,
  routing, comparison and responsive behavior. Native Android IME, Back, offline
  cold start and human readability still require device observation.

## GENKI-002 — Curriculum and generated language

- **Purpose:** catch natural-language errors that structural validation cannot prove.
- **Preconditions:** reviewed lesson JSON and the third-edition source textbook.
- **Steps:** for each changed point, compare its scope with the cited grammar and
  expression notes; inspect every eligible slot combination at its own and expanded
  vocabulary ceilings; check English/Japanese/kana agreement, verb arguments,
  counting, tense, register and optionality; add a new compatible tagged word and
  verify that existing patterns accept it without exposing later grammar.
- **Expected:** original explanations cover the intended source point; every prompt
  naturally elicits that grammar; no incompatible semantic pairing or future form
  is generated. Vocabulary help supplies required lexical knowledge without giving
  the conjugated answer. Correct alternatives remain eligible for learner review.
- **Automation:** `GenkiTests` owns section coverage, ID/dependency/template
  validation, typed substitution, vocabulary growth and grammar-level gating.
  Human language judgment remains necessary for newly authored lexical meanings,
  tags, forms, recaps and pragmatics.
