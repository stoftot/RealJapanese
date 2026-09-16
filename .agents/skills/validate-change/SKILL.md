---
name: validate-change
description: Select and execute proportionate checks for a change or claimed fix, distinguishing static, test, runtime, browser and device evidence. Use for verification; investigate unclear causes with debug-problem and implement with implement-change.
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
3. Climb this conceptual ladder only as far as the behavior and risk require:

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

## Execute and interpret

1. Run the narrowest meaningful checks using canonical repository targets or the
   applicable adapter. Record command/scenario, target/configuration and outcome
   in the current conversation. Wait for completion and inspect exit status/output.
2. Treat application launch as distinct from behavior verification. For browser or
   device changes exercise the affected interaction against real application state.
   A tooling probe alone validates the tool, not the changed application.
3. On failure, identify whether the change, pre-existing code or environment is
   responsible. Return change-caused failures to implementation; use
   [debug-problem](../debug-problem/SKILL.md) when cause is unclear.
4. After corrections, rerun affected checks. Broaden only for additional affected
   boundaries, new failures or unresolved concerns; avoid full-suite repetition
   that adds no evidence.
5. Stop/clean up processes, browser/debugger sessions and disposable fixtures
   created for the check. Preserve user-owned running sessions and data.

## Report / hand off

Give an evidence summary (table or short list) with separate categories:

- **Executed:** exact commands/scenarios and their scope.
- **Passed:** observed results, including concrete behavior where checked.
- **Failed:** observed failure and whether attributed to change or baseline.
- **Not executed / blocked:** what could not run, why, and remaining confidence gap.

Say None for a category with no entries. Never claim tests, compilation, launch,
reproduction or runtime/browser/device verification that did not occur. Return
results to the requesting workflow; implementation owns release relevance and
context handoff, not this skill.
