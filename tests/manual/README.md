# Manual regression tests

This is the durable catalog for repeatable application scenarios that need browser,
device or human observation. [validate-change](../../.agents/skills/validate-change/SKILL.md)
selects relevant cases and executes them with available tooling where reliable;
a manual specification does not imply that human execution is always necessary.
Keep run results and unexecuted steps in the current task, not in these files.

## Catalog and existing coverage

| Domain | Cases |
| --- | --- |
| [Local sync](local-sync.md) | Automatic transfer, Manual transfer, preview cancellation across web and Android |

The initial cases reuse behavior documented in the [sync guide](../../docs/local-sync.md).
That guide remains the owner of user instructions and network/compatibility limits.
This catalog is selective, not a claim of complete application regression coverage.

Automated storage, merge/recovery and transport checks remain in
[`StorageChecks`](../../RealJapanese/StorageChecks/Program.cs); use the canonical
commands in the [development guide](../../docs/development.md#build-shared-code-and-run-storage-checks).
Do not duplicate those assertions as manual cases unless actual UI/device behavior
adds evidence. [Tooling verification](../../docs/verification.md) continues to own
tool acceptance procedures, which do not establish application correctness.

## Case conventions

Group related cases in one feature/domain file. Use stable IDs such as `SYNC-001`
and retain IDs when updating a case; do not reuse a retired ID for different behavior.
Each case contains:

- **Purpose:** the regression it protects.
- **Preconditions:** environment, test data and initial state needed to repeat it.
- **Steps:** ordered, observable actions.
- **Expected result:** concrete pass/fail observations.

Include cleanup or restoration steps where a case changes state. Use disposable
test installations/progress and preserve user-owned catalogs and saved progress.
Reference canonical setup and behavior docs instead of copying their details.
New cases are added/updated through [create-tests](../../.agents/skills/create-tests/SKILL.md)
when warranted by a clear plan; do not record implementation history here.
