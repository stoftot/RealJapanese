---
name: debug-problem
description: Investigate a failure with an unclear cause using reproduction, targeted evidence and falsifiable hypotheses, then verify the justified fix. Use for diagnosis, not routine implementation of an already understood change or validation alone.
---

# Debug from evidence

Read [PROJECT_MAP](../../../docs/ai/PROJECT_MAP.md) for unfamiliar work, then only
the affected subsystem's context, implementation, tests and applicable module facts.
Use the active environment's available capabilities; adapters own detailed tool
procedures. Keep investigation state in the conversation, not a debugging journal.

## Investigation loop

1. **Observe / reproduce:** State expected versus actual behavior. Capture the
   failing input, steps, environment, version/configuration and frequency. Run the
   smallest reliable reproduction; if unavailable, distinguish the user's report
   from what you directly observed and explain the missing prerequisite.
2. **Collect evidence:** Inspect the failure/test/code, bounded logs, exception
   stack, recent relevant diff and actual runtime inputs. Preserve enough exact
   detail to trace a conclusion; do not dump an entire process or repository.
3. **Localize:** Identify the first demonstrably wrong state and the responsible
   path/boundary. Separate upstream cause from downstream symptoms.
4. **Form a falsifiable hypothesis:** State a proposed cause and the specific
   observation that would confirm or refute it. Rank hypotheses by evidence and
   choose the cheapest discriminating check.
5. **Gather targeted evidence:** Run that check before changing behavior. For each
   iteration keep four distinct statements: observation, hypothesis, evidence,
   conclusion. If contradicted, revise the hypothesis instead of patching around it.
   If inconclusive, change the evidence source or narrow the reproduction.
6. **Fix:** When evidence supports the cause, apply the smallest justified fix
   through [implement-change](../implement-change/SKILL.md). Explain why it addresses
   the cause; avoid unrelated cleanup. A reversible diagnostic probe is acceptable
   when it tests a named hypothesis; remove temporary instrumentation afterward.
7. **Reproduce again:** Repeat the original failing scenario with the same relevant
   conditions. Confirm expected behavior, then run appropriate regression checks
   with [validate-change](../validate-change/SKILL.md). Its result returns to
   implementation for context/release assessment once sufficiently complete.

Do not use `guess → edit → build → still broken → guess → edit`. A successful build
is evidence of compilation, not evidence that the original failure was fixed.
When no useful discriminating check remains, report the unresolved hypothesis and
specific missing evidence/prerequisite; do not claim a fix.

## Choose evidence according to the failure

These are conditional routes, not dependencies of the core:

| Problem | First useful evidence / escalation |
| --- | --- |
| Failing test | Exact failure, test input and implementation; rerun a targeted case |
| Unclear C# relationships | Search/config first; Rider MCP semantics if ambiguity remains and the .NET adapter exists |
| Unclear .NET runtime state | Targeted logs/tests first; `netcoredbg-mcp` through the .NET adapter when runtime inspection matters |
| Browser-only failure | Actual app state, Playwright CLI reproduction, console/network evidence through an applicable web adapter |
| Android/device failure | Targeted `adb` logs, package/process/device inspection through the MAUI/Android adapter if present |
| Running MAUI UI behavior | Device evidence; optional configured DevFlow for UI/app state, never as the source debugger |
| Performance, hang, postmortem | Relevant measurements; optional installed .NET diagnostics through its adapter |
| Huge output/logs | Headroom under root AGENTS guidance; retrieve exact details needed to test the hypothesis |

If an adapter/tool is absent, use available repository-native tests, logs or bounded
inspection and state the limit. A source debugger is not mandatory when cheaper
evidence already resolves the issue. Do not guess MCP tool names or capabilities.
