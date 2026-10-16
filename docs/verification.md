# Tooling verification

Run applicable checks on the target machine. Installation, configuration validity,
compilation and actual runtime behavior are separate evidence. Report results in
the current conversation; do not populate this template with machine-specific
results, session history or acceptance dates.

## Repeat the tests

Run from the repository root. Keep configuration trusted in Codex and Rider open
for IDE checks. Do not start another verification over existing scratch fixtures.

```powershell
./tooling/prepare-verification.ps1
Get-Content tooling/verify-codex.md -Raw |
  codex exec --ephemeral --skip-git-repo-check --strict-config --approve-for-me --json `
    -o .tooling/codex-verification.md - > .tooling/codex-verification.jsonl
python tooling/verify-codex-evidence.py
python tooling/verify-config.py
python tooling/verify-browser.py
```

For this project's Android integration, follow the bounded checks below instead
of provisioning a scratch MAUI app or invoking the legacy MAUI installer/updater.

For Rider, open the scratch **solution**, confirm its identity with
`get_solution_projects(rootFolder=...)`, then call `get_file_problems`,
`search_symbol`, `build_solution_start` and `build_solution_state`. A successful
HTTP connection is not proof of the correct solution being loaded. If Rider is
closed, the integration must fail visibly while core/.NET CLI work remains usable.
Create the solution before opening Rider with `dotnet new sln -n ToolingProbe -o
.tooling/scratch`, then use `dotnet sln .tooling/scratch/ToolingProbe.slnx add` with
the scratch project paths. Include the MAUI project only when verifying that module.

After stopping test sessions and closing the scratch solution:

```powershell
./tooling/clean-verification.ps1
```

## Shared Android guidance and configuration

For documentation maintenance, use static checks and the read-only revision
commands in [Android testing](android-testing.md#discover-and-review-without-starting-services).
Do not run the broad acceptance commands above, start ADB/emulators/Appium, invoke
`Test-AndroidEnvironment.ps1`, or change shared state just to validate documentation.

1. Resolve the tool root and read README, version and relevant changelog entries.
   Inspect the actual project/manifest, build helper, output layout, tests and saved
   device configuration. Existing APKs or old reports do not establish a fresh build.
2. Check changed Markdown links, command parameters against the installed scripts,
   PowerShell example syntax, and any JSON configuration. Validate suite/project
   paths relative to the configuration, actual package ID, AVD ownership and APK
   ABI/output selection without invoking a run. Do not invent IDs or suites.
3. Check that root AGENTS has exactly one review marker and no conflicting copy.
   Use `Get-AndroidTestingChanges.ps1 -SinceRevision` with a valid recorded integer;
   UNREVIEWED uses the baseline query. Investigate invalid/future values.
4. Confirm that guidance preserves reservations, shared services/data, allocated
   ports and Managed/External ownership, and distinguishes physical/OEM coverage.
   Recheck the current revision immediately before completion. Keep the old marker
   when relevant integration validation remains outstanding.

For a separately authorized application/device test, build once with the existing
helper and run an implemented RealJapanese suite using the shared reserved runner.
Check concrete app assertions, diagnostics and teardown in its results; a Settings
or tool-counter smoke pass is environment evidence only. The current project still
needs its Android Hybrid UI harness. Select device scenarios from
[mobile lifecycle](../tests/manual/mobile-lifecycle.md) and the
[manual catalog](../tests/manual/README.md); report unperformed steps. Do not treat
desktop tests, static checks or the shared tool's published results as Android
runtime validation. App release versions do not change for guidance maintenance.

