---
name: aspnet-web
description: Launch and verify ASP.NET applications with real-browser evidence using Microsoft Playwright CLI and Chromium. Use with core workflows for pages, navigation, forms, console/network failures or end-to-end behavior; skip browser automation for backend-only changes.
---

# ASP.NET / web adapter

Use alongside a core implementation/debug/validation/review workflow and the
[.NET adapter](../dotnet/SKILL.md). Read [ASPNET facts](../../../docs/ai/modules/ASPNET.md)
for entry projects, profiles, URLs, services and important flows; read .NET facts
only for the needed build/test/run target. No ASP.NET project means this adapter
applies only to explicitly requested supporting toolchain work.

## Establish a runnable application

1. Detect .NET and the actual project; use the .NET adapter for build/test/run.
   Confirm launch profile, environment and required local services from config.
   Do not guess ports or substitute a sample page for the actual app.
2. Detect Node.js, Microsoft Playwright CLI and Chromium. This template's wrapper
   is [tooling/playwright.ps1](../../../tooling/playwright.ps1); inspect its installed
   `--help` or the available CLI's help for current commands. Exact installation
   details belong to [tooling](../../../docs/tooling.md).
3. Node.js is browser-tooling infrastructure. Do not add an application npm
   manifest, JavaScript build pipeline or another browser stack merely to use it.
   Only Chromium belongs to the default browser profile; do not assume Firefox/
   WebKit. Detect even default tools on a copied/unconfigured host.
4. Start the intended application or confirm the identity/configuration of an
   existing instance. Check the actual listening URL/readiness and required state.
   Diagnose launch/dependency failures before trying interactions against it.

## Gather real-browser evidence

1. Choose the relevant scenario from the change or original reproduction. Establish
   inputs and a concrete observable expected result; use known local test state.
2. Launch Chromium with the detected Playwright CLI and navigate to the actual
   application URL. Inspect current page/UI or accessible representation where
   supported before choosing locators. Do not invent selectors or stale element refs.
3. Exercise navigation, forms and controls with supported locate/click/type/fill
   operations. Wait for the relevant state, response or page transition rather
   than treating a click returning successfully as proof of behavior.
4. Assert observable results against actual application state, including persistence
   or server-side effects when relevant. Gather focused browser console/network
   information where supported; use screenshots for visual evidence where useful.
5. For a browser-only failure, feed observed page state, console errors and requests
   into `debug-problem`. Prefer this runtime evidence over source speculation.
   After the fix, repeat the original interaction and relevant neighboring behavior.
6. Close task-owned browser sessions and stop the app/services started solely for
   verification. Clean disposable fixtures/artifacts using existing tooling when
   appropriate; do not close user-owned sessions or reset unrelated app data.

## Validation handoff and missing tools

Real-browser checks complement unit/integration tests; they do not replace them.
Report the profile/URL, scenario, observed result, console/network evidence examined
and limitations to `validate-change`. Distinguish application behavior from the
standalone tooling fixture's acceptance result.

If the app/services or browser tooling are unavailable, execute useful remaining
tests/static/HTTP checks and report browser validation as not performed with the
specific reason. Do not claim equivalent UI evidence from an HTTP request. Do not
silently install dependencies or add another stack to conceal an unavailable check.
