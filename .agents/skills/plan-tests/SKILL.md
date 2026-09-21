---
name: plan-tests
description: Determine durable automated and manual regression coverage after meaningful observable behavior is added, removed, fixed or materially changed. Own test design and coverage decisions; hand clear cases to create-tests for implementation.
---

# Plan durable regression coverage

Use after implementation changes observable behavior, including business logic,
APIs, authentication/session handling, persistence, errors, browser interactions
and device/UI state. Comments, formatting, harmless local renames and equivalent
mechanical refactors already adequately covered do not need a dedicated pass.

This skill decides what evidence should exist. [create-tests](../create-tests/SKILL.md)
creates it; [validate-change](../validate-change/SKILL.md) selects and executes
coverage for the completed change. Keep the plan in the current conversation or
temporary agent handoff, never in a committed test-plan or task-history file.

## Assess coverage

1. Inspect the implemented change and relevant production code, existing automated
   tests and manual specifications. Start from the test locations in
   [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md); consult affected architecture
   and conventions only where needed. Separate intended behavior from assumptions.
2. Identify changed or removed behavior and important regression risks: happy and
   negative paths, edge/boundary cases, state transitions and integration boundaries.
   Map these to existing coverage before proposing additions. Update obsolete
   expectations when behavior is deliberately removed or changed.
3. Choose the lowest appropriate level that proves each behavior: unit,
   integration, runtime/application, browser, or device/UI/manual. Decide what
   should be automated and what needs repeatable manual observation, considering
   determinism, environment availability and maintenance cost. Do not replace
   browser/device evidence with source assertions when actual interaction matters.
4. Reuse sufficient coverage; add or update only cases with distinct regression
   value. Avoid implementation-mirroring tests and redundant variants. State when
   no new coverage is warranted and continue to normal validation.

Use installed, applicable technology adapters for execution constraints and
mechanics; they do not own test design. Do not introduce a framework merely to
fit a preferred test level.

## Temporary handoff

Give the creator concrete inputs/preconditions, expected observable outcomes,
target files or existing cases, and dependencies where relevant:

```text
AUTOMATED
- Test to add/update; behavior it proves; appropriate level.

MANUAL
- Case to add/update; expected observable behavior.

EXISTING COVERAGE
- Tests/case IDs that remain sufficient and why.

LIMITATIONS
- Intentionally uncovered areas and reasons; unresolved requirements.
```

Use None where appropriate. Resolve ambiguity before delegating dependent cases;
do not infer desired behavior solely from what the implementation currently does.

## Model routing

Prefer the capable primary model for coverage selection, edge cases, architectural
or cross-component reasoning, automated-versus-manual decisions and ambiguous
behavior. Do not automatically delegate meaningful test design to a cheap model.
Once expectations are clear, hand bounded creation work to `create-tests`.
