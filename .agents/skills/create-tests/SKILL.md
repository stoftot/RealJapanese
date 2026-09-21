---
name: create-tests
description: Create or update durable automated tests and reusable manual regression cases from a sufficiently clear test-plan handoff. Reuse established test conventions; return unclear coverage or expected behavior to plan-tests.
---

# Create durable regression coverage

Consume the temporary handoff from [plan-tests](../plan-tests/SKILL.md), or an
equally clear request specifying behavior and expected results. This skill owns
coverage implementation, not independent requirements or test-design decisions.

## Create and check coverage

1. Inspect existing test organization, neighboring cases, frameworks, fixtures,
   helpers and conventions through [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md).
   Reuse established executable checks as well as framework-based tests; do not
   add a test framework without a concrete need.
2. Implement the planned automated cases at the selected level. Update existing
   tests where appropriate instead of duplicating them. Assert observable outcomes
   with deterministic, isolated data; preserve real catalogs and user progress.
3. Run created/modified tests where practical using canonical repository commands
   and applicable adapters. Inspect results and correct test-implementation errors.
   Report unavailable execution with its reason; writing a test does not prove it
   passes. Broader completion validation belongs to
   [validate-change](../validate-change/SKILL.md).
4. Create/update the planned manual specifications in the established catalog.
   Search for equivalent cases before creating files. This project's location and
   format are owned by [tests/manual](../../../tests/manual/README.md); group by
   coherent feature/domain and preserve useful existing cases and stable IDs.
5. Return changed test paths/case IDs, actual execution results and unresolved
   limitations to the calling workflow for completed-change validation.

## Production-code boundary

Do not change production code merely to force a planned test to pass. If evidence
suggests incorrect production behavior, conflicting requirements, ambiguity in
the plan or architecture that prevents reasonable testing, return the evidence
to the capable primary model. Use [plan-tests](../plan-tests/SKILL.md) for design
ambiguity, [debug-problem](../debug-problem/SKILL.md) for unexplained failures and
[implement-change](../implement-change/SKILL.md) for an understood production fix.
Do not weaken assertions to hide a failure or invent expected behavior.

## Model routing

When delegation is available and worthwhile, prefer cheaper/faster subagents for
clearly specified cases, variants following established patterns, structured
manual descriptions and repetitive scaffolding. Give each a bounded file scope,
the plan, relevant conventions and expected execution evidence; review the result.

Escalate substantial test architecture, unclear requirements, conflicts between
expectations and implementation, or important cross-component reasoning to the
capable primary model. If delegation is unavailable or costs more than it saves,
continue locally with the same handoff and production-code boundary.
