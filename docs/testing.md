# Automated tests

Open `RealJapanese.Tests.slnx` in Rider/Visual Studio, or use `dotnet test` from
the repository root. Tests use xUnit v3, with bUnit for Razor components and
Microsoft Playwright for real Chromium interaction. There is no custom test runner.

## Setup and execution

Requires the .NET 10 SDK. Restore/build once, then install Playwright's matching
Chromium browser before the first browser run:

```powershell
dotnet restore RealJapanese.Tests.slnx
dotnet build RealJapanese.Tests.slnx --no-restore
pwsh -File tests/RealJapanese.WebTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test RealJapanese.Tests.slnx --no-build --settings tests/parallel.runsettings
```

The browser download is needed once per Playwright browser version. It does not
require Chrome to be installed. A missing browser is a setup failure, not a skipped
passing test. Browser tests start and stop their own web hosts; no separately
running app or Android SDK is required.

For subsequent runs, `dotnet test RealJapanese.Tests.slnx --settings tests/parallel.runsettings`
also builds changes. Run any project individually, or filter by class/test name:

```powershell
dotnet test tests/RealJapanese.Tests/RealJapanese.Tests.csproj
dotnet test tests/RealJapanese.IntegrationTests/RealJapanese.IntegrationTests.csproj
dotnet test tests/RealJapanese.ComponentTests/RealJapanese.ComponentTests.csproj
dotnet test tests/RealJapanese.WebTests/RealJapanese.WebTests.csproj --filter FullyQualifiedName~SyncPreviewBrowserTests
```

Run from a built checkout using the normal `bin/<configuration>/net10.0` layout.
The catalog and child-process fixtures resolve sibling projects there; relocated
test artifacts and custom output paths are not supported by those fixtures.

Use `--logger trx` for machine-readable results in ignored `TestResults/` folders.
Normal test discovery, individual execution, failure details and debugging are
available through the IDE's test runner. Helpers and `RealJapanese.UtilityHost`
are support projects, not additional test frameworks or manual tests.

## Responsibility and parallelism

| Project | Owns |
| --- | --- |
| `RealJapanese.Tests` | Pure transformations, categories, chunks, conjugation, Genki forms/pipeline and number generators |
| `RealJapanese.IntegrationTests` | JSON files, repositories, atomic progress, migration, sync merge/recovery, real socket protocol/discovery, duplicate-cleanup process |
| `RealJapanese.ComponentTests` | Rendered Razor selectors, routes, answers, retry/chunk state and reusable controls through bUnit |
| `RealJapanese.WebTests` | Browser JavaScript, keyboard/focus, responsive CSS, persisted selections and two-host sync through Playwright |

xUnit runs independent test classes in parallel. Each test owns its mutable data,
component/service context and sockets; TCP and HTTP listeners use OS-assigned
ports. Browser concurrency is capped at two classes to keep headless browsers and
app processes from exhausting a development machine. Other projects use up to
half the available processors. `parallel.runsettings` also permits independent
test assemblies to run concurrently. Cases within one class remain sequential.
Only shared-resource discovery checks need a nonparallel collection. Discovery
may report a skip when no usable private IPv4 interface exists; loopback transport
tests still run and do not prove real Wi-Fi or Android interoperability.

Catalogs are copied to a unique temporary directory. Tests never write canonical
catalogs or user progress. Cleanup runs only against constructed disposable data
in a separate child process; the tiny host catches utility exceptions and returns
an exit code, avoiding unhandled-exception Windows dialogs. Web build output is
isolated from the developer's normal running app. Processes are stopped and
temporary data removed when their test finishes.

## Known application defects

Intended behavior that currently fails is marked `Explicit = true` and
`Category=KnownDefect`, with a comment explaining the discrepancy. These cases
are excluded from normal run totals, not counted as passing. The VSTest adapter
may print skip notices without including them in its skipped count. Inspect them
in the source or with `--list-tests`, and run them deliberately:

```powershell
dotnet test RealJapanese.Tests.slnx --filter Category=KnownDefect -- xUnit.Explicit=only
```

Expect this diagnostic run to fail until the application defects are fixed. Remove
the explicit marker and trait when a fix makes a regression pass. Do not change
the expected result to preserve erroneous application output.

Covered defects include counting/age readings, indented JSONL round-tripping, the
invalid default random-time range, stale retry questions after switching chunks,
and interrupted legacy cleanup leaving incompatible catalog/progress IDs.
Conjugation and Genki form regressions have independent literal Japanese/kana
oracles in ordinary tests; they are no longer marked as known defects. Native
lifecycle and external-model extraction are not established by this suite.

Genki runtime practice reads the stored `RealJapanese/Data/Genki/questions.jsonl`
bank; the canonical file is intentionally empty. Component and browser practice
tests create isolated private fixture banks, so their successful practice cases do
not imply that production questions have been published. `GenkiTests`,
`GenkiPipelineTests` and `GenkiFormsTests` validate controlled schemas, pipeline
behavior and form rendering. `GenkiSourceCoverageIntegrationTests` scans fixed
references and form availability against real catalogs without enumerating sentences.
Full exhaustive generation is an explicit operator
workflow documented in [Genki offline generation and practice](genki.md), not part
of routine test execution.

## Adding coverage

Every test class has a short XML summary stating its behavioral scope. Prefer
descriptive test names; add case summaries/comments when setup, an independent
oracle, a known defect or an unusual boundary needs explanation. Use the lowest
level that establishes the regression and avoid repeating whole scenario matrices
in the browser. Shared fixtures only manage resources; assertions and scheduling
belong to the framework. Keep test plans in the current conversation.

The [manual regression catalog](../tests/manual/README.md) records which parts of
each scenario are automated and which still need device or human observation.
