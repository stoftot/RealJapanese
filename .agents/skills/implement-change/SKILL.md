---
name: implement-change
description: Implement features, refactors, configuration changes, or fixes with an understood cause, including scope selection, validation, context maintenance and release relevance. Route unexplained failures to debug-problem first.
---

# Implement a change

Use for end-to-end implementation. For initialization use `initialize-project`;
for a review-only request use `review-change`. Keep planning in the current
conversation; no persistent task plans or progress files.

## Scope and implementation

1. Establish the intended outcome and observable acceptance criteria from the
   request. Identify important compatibility constraints and separate assumptions
   from confirmed requirements.
2. For unfamiliar/substantial work, start with
   [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md). Read only relevant architecture,
   conventions, decisions and enabled-module facts, following authoritative links.
3. Inspect current code/configuration and related tests. If a failure's cause is
   unclear, use [debug-problem](../debug-problem/SKILL.md) before selecting edits.
4. Choose the smallest coherent change, affected boundaries and proportionate
   validation. Include an observable behavior check when source/build evidence
   alone cannot establish the requested outcome. Use applicable installed adapters
   for technology execution; core work does not require any adapter.
5. Implement within the authorized scope. Add/update tests when they protect a
   meaningful behavior/regression; avoid tests that merely mirror an implementation
   or low-impact prose edit. Respect generated files and unrelated work.

## Validate and finish

1. Execute [validate-change](../validate-change/SKILL.md). Fix failures caused by
   the change and rerun affected checks. For ambiguous failures, return to evidence
   gathering; do not repeatedly guess/edit/build. Identify pre-existing failures
   and unavailable dependencies explicitly instead of concealing them.
2. Check [maintain-project-context](../maintain-project-context/SKILL.md) for durable
   structural, architectural, convention or module changes. Update only affected
   canonical owners and validate any changed references.
3. **Only after the outcome is completed and sufficiently validated**, assess its
   release relevance under [RELEASES](../../../docs/ai/RELEASES.md). If worthy,
   follow its pending-record procedure: identify latest released and current pending
   records, classify the bump, create/escalate the single pending record, preserve
   every accumulated outcome, and recheck its invariants. RELEASES owns the rules;
   do not maintain another version algorithm here. Incomplete attempts do not
   become release notes. Material outstanding behavior verification may mean the
   outcome is not yet sufficiently validated.
4. Inspect the final change set (or compare intended files when Git is unavailable).
   Check for unrelated/generated edits, broken references and unfinished procedure
   scaffolding. Do not publish, tag or mark a release as released in this workflow.
5. Report the result, affected areas, actual commands/scenarios and outcomes, any
   remaining uncertainty, and whether release/context maintenance was warranted.
   Distinguish edits, compilation, passing tests, launch, reproduction and observed
   runtime/browser/device behavior; do not collapse them into a generic verified claim.
