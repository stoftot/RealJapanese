---
name: initialize-project
description: Initialize durable AI context for an existing repository or new application, including checking and repairing local tooling after copying or moving the template. Use for initialization or relocation, not routine feature work or context maintenance alone.
---

# Initialize project

Use one explicit mode: **A: existing repository adoption** or **B: new project
initialization**. An existing template/toolchain without application code can use
mode B for the application while preserving observed template facts.

## Establish the starting point

1. Read root `AGENTS.md`, [the map](../../../docs/ai/PROJECT_MAP.md), and
   [the brief](../../../docs/ai/PROJECT_BRIEF.md), if present. Inspect the request,
   top-level files and manifests; distinguish an application from installed tools
   and disposable probes. Do not read every dependency/cache directory.
2. Choose the requested mode, or infer it from whether actual application code
   exists. Ask only about missing scope/constraints that materially change the
   result; keep independent inspection moving. Label tentative choices as proposed.
3. Locate authoritative existing docs before populating AI context. Keep a short
   reference/summary in the expected AI file when a maintained equivalent already
   owns the subject. Do not create a competing full copy.

## Local tooling and relocation preflight (both modes)

Before relying on installed tools, check whether the checkout was copied/moved or
is on a new machine. A relocation-only request can run this shared preflight and
handoff without redefining established project goals or architecture.

1. Identify the current checkout root, selected tool modules and available commands.
   Distinguish application module activation from the toolchain profile the owner
   wants to retain. Core project definition can proceed without installing tools.
2. If this template's tooling is present, follow the canonical
   [copy/move procedure](../../../docs/tooling.md#copying-or-moving-the-template).
   Inspect local environment configuration and actual launch behavior. An existing
   executable or a successful interpreter launch alone does not prove a copied
   environment's command launchers point into the new checkout.
3. For relocated/stale local Python environments, recreate only affected retained
   environments at their final destination, reinstalling from usable package
   receipts when available. Preserve receipts and validate exact local paths before
   moving any disposable directory. Do not repair generated environments with
   blanket path replacement or silently upgrade tools as a relocation fix.
4. Refresh process-local environment variables and relevant machine-local IDE paths.
   Regenerate/merge the retained MCP profile if needed, preserving unrelated client
   configuration. Detect the current Rider endpoint; do not assume the old port.
5. Verify repaired tools with the applicable existing acceptance checks. Separate
   environment/package checks from functional MCP/browser/device evidence. If a
   dependency, download, IDE setting or restart is unavailable, report the exact
   remaining step while continuing independent context initialization.

Keep machine paths out of durable project context. The tooling guide owns detailed
repair steps; if tooling was removed, skip its preflight rather than requiring it.
This is a setup check, not permission to reinstall shared machine SDKs or optional
modules. Do not repeat initialization merely to rephrase unchanged facts.

## Mode A: Existing repository adoption

1. Inspect projects/packages, manifests, build scripts, test configuration, entry
   points and dependency relationships. Identify technologies from source/config;
   installed tools alone do not activate an application module.
2. Follow representative paths through code/tests to establish real boundaries,
   important flows, conventions, integrations and generated areas. Use available
   semantic tools only when search/configuration does not resolve relationships.
3. Populate the brief with observed purpose/scope, the map with curated paths and
   project/test relationships, architecture with boundaries/flows, and conventions
   with repository-specific patterns not already enforced in configuration.
4. Read existing rationale before adding durable decisions. Populate DECISIONS
   only for discoverable non-obvious choices; an observed pattern alone does not
   prove its rationale. Mark inferred explanations and unresolved unknowns.
5. Mark applicable supplied modules active in the brief and fill their facts from
   actual project/build/run configuration. If an adapter is absent, use the
   project's native tooling; do not fail initialization or invent the missing file's facts.
6. Read [RELEASES](../../../docs/ai/RELEASES.md). Inspect existing release/version/tag/
   changelog evidence when available and adopt conservatively. Do not fabricate
   historical records or treat a version string as proof of a release.
7. Through [validate-change](../validate-change/SKILL.md), exercise discovered
   build/test targets where feasible. Correct the map if results contradict it;
   distinguish environment limitations and pre-existing failures from wrong facts.

Initialization primarily documents reality. Do not restructure/refactor an
existing application to fit a preferred template.

## Mode B: New project initialization

1. From the idea, establish purpose, users/use cases, goals, non-goals, major
   capabilities and meaningful constraints. Put them in the brief. Record material
   assumptions explicitly; avoid designing hypothetical future requirements.
2. Select technology modules based on actual intended platforms and requirements.
   Document the choice in the brief; fill only known module facts. Distinguish
   installed infrastructure from the chosen application target.
3. Define enough initial component boundaries, dependency direction, important
   flows and integration assumptions in ARCHITECTURE to support a first coherent
   implementation. Record accepted non-obvious choices and tradeoffs in DECISIONS;
   mark unsettled choices Proposed rather than inventing owner agreement.
4. Add likely application/test layout and entry points to PROJECT_MAP, explicitly
   labeled **proposed, not yet created**. Describe only meaningful project-specific
   conventions. Keep unknown IDs, URLs and build targets as clear placeholders.
5. Choose/document the initial expected release version using RELEASES. Do not
   invent historical releases, create empty outcome notes, or interpret a template
   generation number as an application's released version.
6. Review consistency: can the first use case be implemented and checked with the
   scope, boundaries and proposed layout? Validate context links/metadata and any
   real scaffold that was authorized and created. No source means no application
   build/test claim. Do not scaffold a fake app merely to exercise a tool.

## Handoff

Use [maintain-project-context](../maintain-project-context/SKILL.md) to reconcile
cross-file facts, without copying procedures into documents. Report observed facts,
proposals/unknowns, actual checks and remaining owner inputs. Assess any completed
version-worthy infrastructure changes under RELEASES after validation. The result
should enable ordinary work through [implement-change](../implement-change/SKILL.md).
