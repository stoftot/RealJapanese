---
name: maintain-project-context
description: Synchronize durable project context after meaningful structural, architectural, convention or technology configuration changes. Use for changed facts or stale context, not routine rephrasing, task history or first-time initialization.
---

# Maintain project context

Use after meaningful changes or when stale context is discovered. For initial
adoption/project definition use `initialize-project`. This skill owns deciding
which context needs updating; each document remains the canonical owner of facts.

## Find the affected owners

Start with the changed paths/behavior and [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md),
then read only documents implicated by this table:

| Trigger | Canonical document to check/update |
| --- | --- |
| Projects/major directories created, removed or moved; entry points or production/test relationships changed; major subsystems or important dependencies changed | PROJECT_MAP.md |
| Component boundaries, important flows/integrations, dependency direction or invariants changed | ARCHITECTURE.md |
| Durable non-obvious architectural choice made or reversed | DECISIONS.md |
| Durable repository-specific development convention changed | CONVENTIONS.md |
| Project-specific build/test/run/target/device/optional-tool configuration changed | Applicable existing module fact files |
| Actual goals, non-goals, scope, users, major constraints or enabled technology scope changed | PROJECT_BRIEF.md |

## Reconcile facts

1. Verify the new fact against code/configuration and observed validation. Distinguish
   current truth from a proposal, temporary workaround, or unsupported inference.
2. Find authoritative documentation already owning that subject. Update that owner
   and any navigation reference instead of duplicating it in another file.
3. Edit only affected facts. Remove obsolete paths/instructions from current context;
   keep durable decision rationale with status/replacement links when superseded.
   Do not update the brief for internal implementation detail alone.
4. Keep the map curated: enough anchors and relationships to investigate, not a
   generated inventory. Check incoming references when moving/removing a module;
   absent optional modules must not become mandatory links/workflow dependencies.
5. Leave unresolved values explicitly Unknown or marked for owner input. Do not
   turn historic tool verification into a claim about current runtime availability.
6. Verify referenced paths and consistency across changed owners. For module
   removal, check that the remaining core workflows can still be followed without
   loading the removed module. Use relevant lightweight checks from
   [validate-change](../validate-change/SKILL.md).

## Finish

Summarize which durable facts changed and their evidence; if none changed, say no
context update was needed. Return to the calling workflow for release relevance
when appropriate. Do not create activity through rephrasing, preserve obsolete
intermediate reasoning, or add permanent task/progress/AI session records.
