# Agent operating contract

## Orient before changing

- Inspect existing implementation and repository context before nontrivial edits.
- For unfamiliar or substantial work, begin with [PROJECT_MAP](docs/ai/PROJECT_MAP.md).
- Read only the architecture, conventions, decisions, and module facts relevant
  to the task; follow references to existing authoritative documentation.
- Separate observed repository facts from assumptions. Mark unknowns explicitly.
- Preserve unrelated work and avoid unnecessary edits to generated/external areas.

## Choose the workflow

- Adopt a repository or define a new project with
  [initialize-project](.agents/skills/initialize-project/SKILL.md).
- Build features, refactors, configuration changes, and understood fixes with
  [implement-change](.agents/skills/implement-change/SKILL.md).
- Investigate an unclear failure with
  [debug-problem](.agents/skills/debug-problem/SKILL.md).
- Select and execute checks with
  [validate-change](.agents/skills/validate-change/SKILL.md).
- Review requested changes with [review-change](.agents/skills/review-change/SKILL.md).
- After meaningful structural or architectural changes, check
  [maintain-project-context](.agents/skills/maintain-project-context/SKILL.md).

## Work from evidence

- Editing files alone does not complete a task. Execute proportionate validation.
- After meaningful behavioral changes, deliberately assess durable regression
  coverage with [plan-tests](.agents/skills/plan-tests/SKILL.md), add/update automated
  or manual cases with [create-tests](.agents/skills/create-tests/SKILL.md) where
  warranted, and reuse that coverage during later validation.
- Reproduce failures and gather evidence before selecting a fix; avoid speculative
  edit/build/retry loops. Keep observations, hypotheses, evidence, and conclusions distinct.
- Prefer repository search, filesystem inspection, and CLI when sufficient.
- Use IDE semantics when symbol/project relationships are materially uncertain.
- Use a source debugger when runtime state matters and cheaper evidence is insufficient.
- Use browser tooling for actual browser behavior and device tooling for device behavior.
- Detect installed commands and currently exposed MCP capabilities before using them.
- Tool descriptions are capability guides, not proof of installation or compatibility.
- Optional tooling must not become a hard dependency of a core workflow.
- If tooling is absent, use available evidence and state any remaining validation gap.
- Do not install a tool merely because a skill mentions it.

## Keep context small

- Headroom is the default large-output aid when exposed. Use only
  `headroom_compress`, `headroom_retrieve`, and `headroom_stats`.
- Compress unusually large logs/output or text needed later; retrieve original
  details before drawing a conclusion that depends on them.
- Leave ordinary small files/output uncompressed. Without Headroom, use bounded
  searches/excerpts and temporary artifacts; it is not durable repository storage.

## Use delegation deliberately

- When the active environment supports model delegation, use cheaper/faster
  models for worthwhile bounded mechanical, low-risk, exploratory, or parallel work.
- Give each subagent a clear question, limited scope, and expected evidence.
- Keep architecture, ambiguous reasoning, cross-cutting debugging, integration
  decisions, and final synthesis on an appropriately capable primary model.
- Review delegated results. Do not delegate merely for the sake of delegation.
- When delegation is unavailable or coordination costs more than it saves,
  continue normally on the primary model without failing the workflow.

## Preserve durable ownership

- Follow the responsibility split in [docs/ai/README](docs/ai/README.md).
- Update context only when durable facts change; reference canonical owners.
- Use Git history for implementation history, decisions for durable rationale,
  current docs for current truth, and release records for version-worthy outcomes.
- Keep task plans in the current conversation. Do not add permanent task history,
  progress files, AI activity logs, observability, or session telemetry infrastructure.
- After completed validation, assess release relevance using [RELEASES](docs/ai/RELEASES.md).
  Preserve the single pending record and its accumulated notes. Publishing is separate.

## Create meaningful local commits

- First determine whether the project is inside a Git working tree. If Git is
  unavailable or the project is not in a repository, continue normally; Git absence
  is not an error and does not require repository initialization.
- Inside a repository, create useful local commits as coherent, sufficiently
  validated work is completed. Small tasks normally get one final commit; medium
  tasks get one or a few natural milestones; large tasks progressively commit
  independently valid portions and continue until the full task is complete.
- Group implementation with its relevant automated/manual regression coverage,
  durable context and release metadata. Do not split commits by workflow step or
  skill. Intermediate commits do not replace final validation of the whole task.
- Before staging or committing, inspect status and relevant working-tree/staged
  diffs against the task's starting state. Stage explicit task-owned files/hunks;
  never automatically include, discard, reset or overwrite unrelated work. If
  intertwined changes cannot be safely separated, leave affected work uncommitted
  and explain why. Do not create knowingly broken checkpoint commits.
- Inspect recent history and follow established message conventions. Describe
  the completed result; avoid vague messages such as `updates` or `WIP`.
- The primary agent coordinates Git and inspects status/diffs before every commit.
  Subagents normally return work/results; avoid concurrent staging/committing in
  the same working tree.
- Local commits are authorized. Push/force-push, remote branch creation or changes,
  PRs, merges, rebasing user/published history, amending existing user commits,
  tags and release publication require explicit instruction or a dedicated workflow.
  Follow the [implementation procedure](.agents/skills/implement-change/SKILL.md)
  for commit preparation and final reporting.

## Apply technology modules conditionally

- Technology adapters apply only when their files and corresponding projects exist,
  or when the requested task explicitly concerns that technology's toolchain.
- Discover enabled modules in [PROJECT_BRIEF](docs/ai/PROJECT_BRIEF.md); read their
  facts and procedures only as needed. The core remains technology-neutral.
- Removing a module must not require installing its tools to use the core.

## Report completion precisely

- Summarize what changed, why, and what was actually executed and observed.
- Distinguish edited, compiled, tests passed, launched, reproduced, and behavior
  verified in runtime/browser/device. Never imply a stronger level than observed.
- Name failures and unavailable checks with reasons and their practical impact.
