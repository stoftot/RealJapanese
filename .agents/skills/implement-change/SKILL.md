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
   Detect Git availability and use `git rev-parse --is-inside-work-tree` from the
   project directory. If outside a repository or Git is unavailable, continue
   normally without commits or initializing Git. Other Git failures are not proof
   of repository absence: report unresolved access/configuration problems and
   continue work that can be done safely. Inside a repository, inspect
   `git status --short`, working-tree and staged diffs, and recent `git log` messages before
   editing to identify pre-existing work and the commit-message convention.
4. Choose the smallest coherent change, affected boundaries and proportionate
   validation. Include an observable behavior check when source/build evidence
   alone cannot establish the requested outcome. Use applicable installed adapters
   for technology execution; core work does not require any adapter.
5. Implement within the authorized scope. Respect generated files and unrelated work.
6. After implementing or materially changing observable behavior, use
   [plan-tests](../plan-tests/SKILL.md) to determine whether permanent automated or
   manual regression coverage needs to be created or updated, then use
   [create-tests](../create-tests/SKILL.md) for warranted coverage before completed-change
   validation. If existing coverage is sufficient, continue to normal validation.
   Obviously non-behavioral edits do not need a dedicated test-planning pass.

## Choose coherent commit boundaries

Follow the local-commit policy in [AGENTS.md](../../../AGENTS.md). Small tasks
normally need one commit after validation and relevant context/release updates.
Medium tasks may use a few completed milestones when useful, or one final commit.
For large tasks, progressively commit independently coherent, sufficiently
validated portions rather than holding all completed work until the end.

Useful units include a complete fix with regression coverage, an independently
valid prerequisite refactor, a completed subsystem or a self-contained infrastructure
change. Keep associated tests, manual specifications, context and applicable release
metadata with the implementation; do not split one feature into code/tests/docs
commits merely because different skills produced them. If no independently valid
boundary exists, keep implementing until one does; never commit knowingly broken
intermediate work just to make a checkpoint.

## Validate each completed portion and finish

Apply the following to each completed logical portion selected for a commit, then
continue remaining work. Before finishing the full task, perform final validation
covering the complete change, including integration across portions; intermediate
checks/commits do not replace it. For a small task, these are normally the same pass.

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
4. Inspect the change set (or compare intended files when Git is unavailable).
   Check for unrelated/generated edits, broken references and unfinished procedure
   scaffolding. Do not publish, tag or mark a release as released in this workflow.
5. Inside a Git working tree, prepare a local commit for the completed portion:

   - Recheck `git status --short`, `git diff` and `git diff --cached` against the
     starting state. Identify task-owned changes and anything pre-existing or
     introduced by the user/another process, including already staged changes.
   - Stage only explicit relevant files/hunks, not a blanket `git add -A`. Preserve
     unrelated working-tree and index changes. If safe separation is not possible,
     leave affected work uncommitted and report why; do not reset or overwrite it.
   - The primary agent reviews the full staged diff before each commit and ensures
     it contains only the intended coherent unit. Do not commit unrelated content
     already in the index. Coordinate subagents to avoid concurrent staging/commits.
   - Use a meaningful message describing the result, following recent repository
     conventions (for example, `Fix duplicate project creation`). Create the local
     commit, verify success and inspect status afterward. If Git cannot commit,
     preserve the work and report the reason without claiming it was committed.

   Continue until the requested task is complete. After final validation and any
   remaining context/release updates, commit remaining coherent task work when safe;
   do not create an empty or arbitrary final commit. Local commits do not authorize
   the remote/history/release operations restricted by AGENTS.md.
6. Report the result, affected areas, actual commands/scenarios and outcomes, any
   remaining uncertainty, and whether release/context maintenance was warranted.
   Include created commit IDs and summaries, or why work remains uncommitted.
   Distinguish edits, compilation, passing tests, launch, reproduction and observed
   runtime/browser/device behavior; do not collapse them into a generic verified claim.
