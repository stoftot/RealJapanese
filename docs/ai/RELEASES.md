# Release tracking

This is the canonical release policy. Actual records live in [releases/](../../releases/).
Release notes describe completed, sufficiently validated application outcomes
relevant to end users; keep them short, simple and user-facing. Technical implementation and validation
details belong in development documentation. Publishing, tagging, GitHub releases,
and artifact upload are outside V2.

## Initial version and adoption

**Assumed base release: `0.0.0`.** This is the owner-approved starting baseline
for version tracking, not a claim that `0.0.0` was published.
**Initial expected version: `0.1.0`.**
Inspect actual project history during adoption. Do not infer released versions
from a template/specification generation label or invent a historical baseline.

- A fresh copy may have no release records; `releases/.gitkeep` retains the directory.
- On the first completed, validated, version-worthy change, create the initial
  expected version with `released: false`, `release_date: null`, and
  `previous_release: "0.0.0"`, using the assumed baseline above. Do not create a
  published release record for that assumption.
- Before any release has happened, accumulate further outcomes in that initial
  pending version, rather than incrementing per task.
  The owner may change the initial target; rename the same record and preserve notes.
- When copying the template for a new product, omit template release records and
  choose that product's initial expected version here.
- For existing products, inspect version manifests, release records, tags, and
  changelogs (when available). Reference the authoritative version source. Do not
  infer that a tag or manifest was actually released without supporting evidence.
  Record a known external baseline here if not represented in local records;
  if evidence conflicts or is unknown, resolve that before assigning a next version.

Use the assumed `0.0.0` baseline until a record is marked `released: true`.
Thereafter, use the latest released version. Keep updating the same pending
record; create another only after the current one has been marked released.
Package and target-framework versions do not establish a product release baseline.

## Invariants and schema

There may be **at most one** record with `released: false`; zero is valid before
version-worthy work or just after a release. Every actual record has:

- A filename `<major>.<minor>.<patch>.md` matching `version` and its H1 heading.
- A quoted three-part version with nonnegative integers and no leading zeros.
- `released`: YAML boolean; `release_date`: `null` while pending, otherwise an
  ISO `YYYY-MM-DD` date; `previous_release`: quoted latest confirmed released
  version, or the assumed `"0.0.0"` baseline before the first release. It never
  points to an abandoned pending version.
- Human-readable outcome notes under applicable Added, Changed, Fixed, Removed,
  and Notes headings; omit empty sections.

Example only, not an assertion of release history:

```markdown
---
version: "1.2.0"
released: false
release_date: null
previous_release: "1.1.1"
---

# 1.2.0

## Added

- Added offline project synchronization.
```

Do not mark records released or set dates as part of implementation. A future
authorized release/publish workflow will perform that transition.

## Release relevance and Semantic Versioning

Use `major.minor.patch` terminology:

| Level | Meaning | Next version from released M.m.p |
| --- | --- | --- |
| Major | Incompatible/breaking behavior or API change | (M+1).0.0 |
| Minor | Backward-compatible new functionality | M.(m+1).0 |
| Patch | Backward-compatible bug fix or corrective version-worthy change | M.m.(p+1) |
| None | No version-worthy outcome | No bump/record required |

Only application changes relevant to end users are release-worthy: features, fixes,
usability, performance, reliability, security and compatibility changes. Describe
the effect on the app and its users, not the internal implementation work.

Internal tools, AI infrastructure, agent skills, development workflows, test-only
changes and developer documentation do not warrant release notes or a version bump.
When such work accompanies a qualifying app change, record only the app outcome.
Typo-only edits, routine rephrasing, transient investigations and unsuccessful
attempts normally are not release-worthy. State a compatibility rationale for
breaking changes.
For consistency this project uses these bump rules even after a `0.x` baseline
release; an alternative pre-1.0 policy must be explicitly adopted here first.

## Update the single pending record

The [implementation skill](../../.agents/skills/implement-change/SKILL.md) invokes
this procedure only after the change is complete enough and validated:

1. Read all actual release frontmatter, locate the most recent confirmed released
   version (compare version integers, not lexical filenames), or use the assumed
   `0.0.0` baseline if none exists, and find the pending record. Exclude `.gitkeep`.
   If multiple pending records, mismatched fields, or
   contradictory baselines exist, reconcile from evidence while preserving notes;
   do not add another record or guess release history.
2. Classify the new outcome using the table. A non-worthy change adds no note/bump.
3. With a released baseline, calculate the new candidate from that baseline.
   Determine the already-required bump from the pending version and its notes.
   Use the highest level required by ALL accumulated outcomes: never downgrade
   or repeatedly bump from the pending version. Before the first release, use the
   initial expected version above. If the pending version is inconsistent with policy, resolve it
   from evidence before changing it.
4. Create a pending file if absent. If escalating, rename/update that same pending
   record, preserving all accumulated notes. Check destination collisions first;
   never overwrite a released record. Leave no abandoned intermediate file.
5. Keep filename, H1, frontmatter version, and previous released baseline consistent.
   Add concise user-facing outcomes, grouped and deduplicated. Keep technical
   details and validation reports in the development documentation.
6. Re-read the directory: at most one pending record, correct version/baseline,
   `release_date: null`, and every previously accumulated outcome preserved.

Example with latest released `1.4.2`: a fix creates `1.4.3`; a later feature renames
the same pending file to `1.5.0`; a later breaking change renames it to `2.0.0`.
All three outcomes remain and `previous_release` stays `1.4.2`. Another patch after
that keeps `2.0.0`; it does not create `2.0.1`. No intermediate pending files remain.
