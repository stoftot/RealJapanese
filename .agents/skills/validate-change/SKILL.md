---
name: validate-change
description: Select and execute automated checks and relevant existing manual regression cases for a change or claimed fix. Report actual static, test, runtime, browser, device and human evidence; use debug-problem for unclear failures.
---

# Validate a change

This skill owns validation selection and evidence reporting. Technology adapters
own build/launch/browser/device mechanics and read their project-specific facts.
Do not require tools or adapters that the project does not use.

## Select checks

1. Inspect the request, changed areas and expected observable outcome. For
   unfamiliar work start at [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md); read
   the relevant test layout, conventions and applicable module targets only.
2. Identify what could regress, available checks, required environments and the
   evidence needed to support completion. Separate observed setup from assumptions.
   Detect commands/MCP capabilities before selecting checks that depend on them.
3. Inspect existing manual specifications through the project map, including
   [tests/manual](../../../tests/manual/README.md). Select cases by affected behavior,
   state transitions and integration boundaries; record relevant case IDs and
   required environments in the current conversation. Reuse existing coverage
   before devising new scenarios. Coverage gaps return to
   [plan-tests](../plan-tests/SKILL.md) and [create-tests](../create-tests/SKILL.md).
4. Climb this conceptual ladder only as far as the behavior and risk require:

   Targeted checks → relevant automated tests → build/static validation →
   runtime verification → browser/device/UI verification.

   This is an evidence ladder, not a rigid execution order; build first if a test
   or launch needs artifacts. Not every change needs every level.

| Change | Typical useful evidence |
| --- | --- |
| Documentation / workflow | Correct links/metadata, coherent ownership and scenarios, no invented facts |
| Formatting / style | Applicable formatter/static checks; do not add meaningless behavior tests |
| Internal C# logic (when applicable) | Targeted tests and relevant build |
| ASP.NET UI interaction (when applicable) | Relevant automated tests plus real app/browser interaction |
| MAUI device behavior (when applicable) | Build, relevant tests and actual target/runtime evidence |
| Runtime bug | Repeat original failing scenario and appropriate regression checks |

## Automated suite policy

Run the full practical automated test suite by default when execution cost and
environment requirements are reasonable. For expensive suites, use targeted tests
during iteration and broader regression validation before completion where practical.

Consider runtime, external dependencies, browser/device requirements and flaky or
expensive integration environments, not token cost alone. Use the repository's
actual test entry points, including self-checking executables. For documentation
or workflow-only edits, lightweight metadata/link/coherence checks can be sufficient;
explain why application execution would add no relevant evidence. Report any
omitted broader checks and the practical confidence gap.

## Execute and interpret

1. Run the narrowest meaningful checks using canonical repository targets or the
   applicable adapter. Record command/scenario, target/configuration and outcome
   in the current conversation. Wait for completion and inspect exit status/output.
   Include the broader practical suite according to the policy above.
2. Treat application launch as distinct from behavior verification. For browser or
   device changes exercise the affected interaction against real application state.
   A tooling probe alone validates the tool, not the changed application.
   Automatically perform selected manual cases when available tooling can reliably
   establish their preconditions, execute steps and observe expected results.
   Record case ID, environment, actual observations and any unexecuted steps;
   partial execution is not a case pass. Identify cases still requiring human
   execution when device/UI access, judgment or environment is unavailable.
3. On failure, identify whether the change, pre-existing code or environment is
   responsible. Return change-caused failures to implementation; use
   [debug-problem](../debug-problem/SKILL.md) when cause is unclear.
4. After corrections, rerun affected checks and complete any outstanding broader
   regression validation. Once required checks pass, broaden or repeat only for
   new changes, failures or unresolved concerns; avoid repetition that adds no evidence.
5. Stop/clean up processes, browser/debugger sessions and disposable fixtures
   created for the check. Preserve user-owned running sessions and data.

## Report / hand off

Give an evidence summary (table or short list) with separate categories:

- **Executed:** exact commands/scenarios and their scope.
- **Passed:** observed results, including concrete behavior where checked.
- **Failed:** observed failure and whether attributed to change or baseline.
- **Not executed / blocked:** what could not run, why, and remaining confidence gap.

Within that summary distinguish automated tests executed/passed/failed, runtime
verification, browser verification, device verification, automatically executed
manual scenarios (case IDs and results), and human manual cases still required
(case IDs, remaining steps and reason). Keep execution results in the current task;
manual specifications remain repeatable cases, not a run-history log.

Say None for a category with no entries. Never claim tests, compilation, launch,
reproduction or runtime/browser/device verification that did not occur. Return
results to the requesting workflow; implementation owns release relevance and
context handoff, not this skill.
