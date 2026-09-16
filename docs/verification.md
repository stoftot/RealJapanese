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

For MAUI, create a scratch project with `dotnet new maui`, restrict its scratch
`TargetFrameworks` to `net10.0-android`, dot-source `enter-env.ps1 -Modules maui`,
then run `dotnet build -f net10.0-android`, `sdkmanager --list_installed` and
`adb devices -l`. `tooling/modules/maui.ps1` also creates, builds and cleans a
temporary MAUI project while installing/updating its dependencies.

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

