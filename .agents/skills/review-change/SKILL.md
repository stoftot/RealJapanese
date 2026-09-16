---
name: review-change
description: Review a change for evidence-backed correctness and regression findings, then architecture, validation, maintainability and style. Report findings by default; do not silently implement fixes unless requested.
---

# Review a change

Use for a requested review of a diff or defined set of changes. If no baseline is
available, establish the review scope from supplied files and state that limit.
Use `implement-change` when the user requests implementation instead.

## Procedure

1. Establish intended behavior, diff/base and changed files; account for existing
   uncommitted work. Begin unfamiliar/substantial work at
   [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md), then load only relevant
   architecture, conventions, decisions and applicable module facts.
2. Inspect the diff and surrounding implementation/callers/tests. Trace affected
   paths and boundaries, including error/edge conditions relevant to the change.
   Separate documented guarantees, observed behavior and untested assumptions.
3. Prioritize correctness, regressions, architectural violations, insufficient
   tests/validation, maintainability, then style. Do not elevate mechanically
   enforced style preferences over actionable behavior issues.
4. For a suspected defect, construct a concrete trigger and trace why the changed
   code produces the wrong outcome. Use tests/build/static checks through
   [validate-change](../validate-change/SKILL.md) when they help substantiate it.
   Optional semantic/runtime tools are useful only when available and needed;
   report evidence limits if they cannot run.
5. Check durable context/release consistency when the diff changes the relevant
   facts, using their canonical rules. Do not demand unrelated document rewrites.
6. Report actionable findings with severity, file/line, triggering conditions,
   impact and supporting evidence. Distinguish confirmed defects from open
   questions; avoid speculative findings without a plausible affected path.
7. Summarize validation executed and review limitations. If no actionable findings
   remain, say so without claiming exhaustive correctness or unperformed checks.

By default, report findings rather than rewriting code. If fixes are explicitly
requested, hand them to [implement-change](../implement-change/SKILL.md) and validate
the result. Review itself does not create a release outcome.
