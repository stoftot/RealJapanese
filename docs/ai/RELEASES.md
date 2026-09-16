# Release tracking

This is the canonical release policy. Actual records live in [releases/](../../releases/).
Release notes describe completed, sufficiently validated, version-worthy outcomes;
they are not task logs. Publishing, tagging, GitHub releases, and artifact upload
are outside V2.

## Initial version and adoption

**Initial expected version: `0.1.0`** (retained template default, not a confirmed
RealJapanese release target; resolve the existing product baseline before using it).
Inspect actual project history during adoption. Do not infer released versions
from a template/specification generation label or invent a historical baseline.

- A fresh copy may have no release records; `releases/.gitkeep` retains the directory.
- On the first completed, validated, version-worthy change, create the initial
  expected version with `released: false`, `release_date: null`, and
  `previous_release: null`. Do not fabricate a released baseline.
- Before any release has happened, accumulate further outcomes in that initial
  pending version, rather than incrementing per task against an invented baseline.
  The owner may change the initial target; rename the same record and preserve notes.
- When copying the template for a new product, omit template release records and
  choose that product's initial expected version here.
- For existing products, inspect version manifests, release records, tags, and
  changelogs (when available). Reference the authoritative version source. Do not
  infer that a tag or manifest was actually released without supporting evidence.
  Record a known external baseline here if not represented in local records;
  if evidence conflicts or is unknown, resolve that before assigning a next version.

**External released baseline:** Unknown. The existing repository contains
application history, but no local tags, changelog, product version properties or
actual release records were found during adoption. Package and target-framework
versions are not product release evidence. Do not infer a released baseline or
assign a next version until the owner supplies confirmation.

Context initialization and local environment repair document/restore the existing
setup; they do not establish a product release. No release record is created for
this adoption, and the retained initial default remains provisional.

## Invariants and schema

There may be **at most one** record with `released: false`; zero is valid before
version-worthy work or just after a release. Every actual record has:

- A filename `<major>.<minor>.<patch>.md` matching `version` and its H1 heading.
- A quoted three-part version with nonnegative integers and no leading zeros.
- `released`: YAML boolean; `release_date`: `null` while pending, otherwise an
  ISO `YYYY-MM-DD` date; `previous_release`: quoted latest confirmed released
  version, or `null` when none exists. It never points to an abandoned pending version.
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

Meaningful developer-facing template/workflow functionality can be release-worthy.
Typo-only edits, routine rephrasing, transient investigations, and unsuccessful
attempts normally are not. State a compatibility rationale for breaking changes.
For consistency this template uses these bump rules even after a `0.x` baseline
release; an alternative pre-1.0 policy must be explicitly adopted here first.

## Update the single pending record

The [implementation skill](../../.agents/skills/implement-change/SKILL.md) invokes
this procedure only after the change is complete enough and validated:

1. Read all actual release frontmatter, locate the most recent confirmed released
   version (compare version integers, not lexical filenames), and find the pending
   record. Exclude `.gitkeep`. If multiple pending records, mismatched fields, or
   contradictory baselines exist, reconcile from evidence while preserving notes;
   do not add another record or guess release history.
2. Classify the new outcome using the table. A non-worthy change adds no note/bump.
3. With a released baseline, calculate the new candidate from that baseline.
   Determine the already-required bump from the pending version and its notes.
   Use the highest level required by ALL accumulated outcomes: never downgrade
   or repeatedly bump from the pending version. Without a baseline use initial
   behavior above. If the pending version is inconsistent with policy, resolve it
   from evidence before changing it.
4. Create a pending file if absent. If escalating, rename/update that same pending
   record, preserving all accumulated notes. Check destination collisions first;
   never overwrite a released record. Leave no abandoned intermediate file.
5. Keep filename, H1, frontmatter version, and previous released baseline consistent.
   Add concise user/developer-facing outcomes, grouped and deduplicated. Mention
   material validation limitations without turning notes into execution transcripts.
6. Re-read the directory: at most one pending record, correct version/baseline,
   `release_date: null`, and every previously accumulated outcome preserved.

Example with latest released `1.4.2`: a fix creates `1.4.3`; a later feature renames
the same pending file to `1.5.0`; a later breaking change renames it to `2.0.0`.
All three outcomes remain and `previous_release` stays `1.4.2`. Another patch after
that keeps `2.0.0`; it does not create `2.0.1`. No intermediate pending files remain.
