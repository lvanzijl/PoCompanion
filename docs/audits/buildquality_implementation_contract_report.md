# Prompt 4 — BuildQuality Data Foundation (Ingestion & Persistence Contract Report)

## 1. Purpose

Define the missing ingestion and persistence contract that allows the locked `BuildQuality` provider to consume build, test-run, and coverage facts without changing CDC semantics, aggregation rules, formulas, Unknown handling, or existing application contracts.

## 2. In scope

- test-run ingestion
- coverage ingestion
- `ITfsClient` expansion
- sync-stage expansion
- persistence entities
- linkage between:
  - builds
  - test runs
  - coverage

## 3. Out of scope

- BuildQuality formulas
- application queries/handlers
- DTOs for UI
- UI design
- metrics redesign

## 4. Locked decisions applied

- Option A — Expand scope is mandatory.
- full BuildQuality scope required:
  - builds
  - test runs
  - coverage
- no build-only fallback
- Unknown rules remain unchanged
- aggregation remains unchanged
- formulas unchanged
- percentages MUST NOT be averaged
- no new metrics
- CDC not reinterpreted
- the existing BuildQuality provider remains untouched in semantics

## 5. TFS client contract (`ITfsClient`)

`ITfsClient` remains the only backend boundary for TFS / Azure DevOps access. Existing build retrieval through cached pipeline runs remains unchanged. The contract must be extended only with raw read methods that return build-linked test and coverage facts.

### 5.1 Test runs retrieval

Define a batched read method on `ITfsClient` that returns test runs linked to supplied builds, for example: `GetTestRunsByBuildIdsAsync(...)`.

Contract requirements:

- input is a batch of build identifiers, not a single-build-only contract
- `BuildId` refers to the TFS build/run identifier already represented by cached pipeline runs
- output returns raw linked test-run facts only
- each returned record must include:
  - `BuildId`
  - `TotalTests`
  - `PassedTests`
  - `NotApplicableTests`
- exact TFS field names for total, passed, and notApplicable remain **UNCERTAIN** until verified in the source API
- if TFS uses an equivalent field rather than a literal `notApplicable` field, that mapping must be explicit and marked **UNCERTAIN** until verified
- verification path for **UNCERTAIN** test fields: confirm the concrete API payload and field names against the TFS / Azure DevOps test-results contract before implementation; do not guess or infer names from formulas

### 5.2 Coverage retrieval

Define a batched read method on `ITfsClient` that returns coverage linked to supplied builds, for example: `GetCoverageByBuildIdsAsync(...)`.

Contract requirements:

- input is a batch of build identifiers, not a single-build-only contract
- `BuildId` refers to the TFS build/run identifier already represented by cached pipeline runs
- output returns raw linked coverage facts only
- each returned record must include:
  - `BuildId`
  - `CoveredLines`
  - `TotalLines`
- Cobertura or equivalent formats are expected
- pipeline defines filters (not ingestion)
- exact TFS field names or artifact element names for coverage totals remain **UNCERTAIN** until verified in the source API
- verification path for **UNCERTAIN** coverage fields: confirm the concrete coverage artifact contract and summary fields against the TFS / Azure DevOps source before implementation; do not guess or infer names from formulas

## 6. External data contracts

The TFS client should return canonical transport DTOs that expose only the raw facts needed by the locked BuildQuality provider.

### `TestRunDto`

Required fields:

- `BuildId`
- `TotalTests`
- `PassedTests`
- `NotApplicableTests`

Contract notes:

- `BuildId` is the build/run identifier used to link the record to the existing cached build anchor
- `NotApplicableTests` may require an explicit source-field adapter if TFS exposes an equivalent field name; that source mapping remains **UNCERTAIN** until verified

### `CoverageDto`

Required fields:

- `BuildId`
- `CoveredLines`
- `TotalLines`

Contract notes:

- `BuildId` is the build/run identifier used to link the record to the existing cached build anchor
- raw coverage source element names remain **UNCERTAIN** until the exact TFS artifact contract is verified
- the minimum BuildQuality contract is summary coverage only; ingestion does not define pipeline-specific filters or quality semantics

## 7. Sync-stage responsibilities

Sync-stage responsibilities are limited to fetching, linking, and persisting raw facts.

- fetch pipeline runs first so the build anchor exists before child facts are processed
- resolve the in-scope cached builds produced by the existing pipeline ingestion path
- fetch test runs for those builds through batched `ITfsClient` calls
- fetch coverage for those builds through batched `ITfsClient` calls
- resolve each returned `BuildId` to the existing cached build row
- persist raw linked facts only

Batching strategy:

- preferred: batch by build-id set for the current sync window
- acceptable fallback: chunk by pipeline-derived build sets when request size must be limited
- not acceptable as the default contract: one remote call per build

Hard rules:

- no formula logic
- no aggregation logic
- raw facts only
- no percentage calculation in ingestion

## 8. Persistence model

The existing build cache remains the anchor. New persistence is additive and stores raw linked facts only.

### `TestRunEntity`

Required fields:

- `BuildId` (FK to the cached build anchor)
- `TotalTests`
- `PassedTests`
- `NotApplicableTests`
- `Timestamp` (nullable; populated only when the source exposes a verified test-run timestamp; when null, time scoping continues to use the build anchor completion time)

Persistence rules:

- store raw numeric facts only
- do not persist derived pass rate or test volume
- use the build foreign key, not an inferred product or pipeline key, as the linkage anchor
- `Timestamp` remains a nullable field in the initial implementation when the source does not expose a verified test-run timestamp
- BuildQuality queries must continue to use the build anchor completion time for time scoping when child timestamps are absent

### `CoverageEntity`

Required fields:

- `BuildId` (FK to the cached build anchor)
- `CoveredLines`
- `TotalLines`
- `Timestamp` (nullable; populated only when the source exposes a verified coverage timestamp; when null, time scoping continues to use the build anchor completion time)

Persistence rules:

- store raw numeric facts only
- do not persist derived coverage percentage
- use the build foreign key, not an inferred product or pipeline key, as the linkage anchor
- `Timestamp` remains a nullable field in the initial implementation when the source does not expose a verified coverage timestamp
- BuildQuality queries must continue to use the build anchor completion time for time scoping when child timestamps are absent

## 9. Linkage model

- build is the anchor
- test runs link via `BuildId`
- coverage links via `BuildId`
- the external `BuildId` from TFS is resolved to the existing cached build row before persistence
- `CachedPipelineRunEntity` remains the persisted build anchor already associated to `PipelineDefinitionEntity`

Multiplicity rules:

- multiple test runs per build are allowed
- multiple test runs per build are persisted as separate raw records
- multiple coverage entries per build are allowed
- multiple coverage entries per build are persisted as separate raw records
- ingestion does not aggregate multiple test runs per build
- ingestion does not aggregate multiple coverage entries per build
- when the source exposes a stable external child-record identity, persistence should upsert idempotently on that identity plus `BuildId`
- when the source does not expose a stable external child-record identity, the sync stage should replace previously cached child rows for the synced build set before inserting the current raw facts so duplicates do not accumulate
- decision rule: use idempotent upsert only after the TFS / Azure DevOps response is verified to contain a stable child-record identity for that child type; otherwise use replace-linked-rows for that child type
- later BuildQuality selection/provider steps aggregate totals from the linked raw facts without changing the locked formulas

## 10. Data integrity rules

- missing test runs are allowed
- missing coverage is allowed
- no coercion to zero
- incomplete records must not break ingestion
- records without verified build linkage must not be persisted as build-scoped facts
- records missing required numerator or denominator inputs must not be silently repaired
- invalid or incomplete raw records may be skipped with diagnostics, but valid sibling records must still be ingested
- raw numeric facts are persisted as-is so later Unknown evaluation remains possible

## 11. Unknown propagation support

The ingestion contract must preserve absence and zero-denominator cases so the provider can correctly emit `Unknown`.

- absence of test runs is represented by no linked `TestRunEntity` rows, not by zero-filled totals
- absence of coverage is represented by no linked `CoverageEntity` rows, not by zero-filled totals
- `TotalLines = 0` must remain representable in persisted coverage facts
- builds may exist without linked tests
- builds may exist without linked coverage
- `NotApplicableTests` must remain separate so `TestVolume` can be derived later without changing the locked formula

## 12. Performance considerations

- avoid N+1 calls per build
- prefer batched `BuildId` retrieval in `ITfsClient`
- reuse the existing pipeline-run sync result set as the source of build ids for downstream test and coverage fetches
- align test-run and coverage ingestion to the existing pipeline-run caching window instead of rescanning unrelated history
- follow the existing two-phase pattern:
  - collect remote DTOs first
  - persist sequentially against one `DbContext`
- batching and caching optimizations must not move formula or aggregation logic into ingestion

## 13. Integration risks

- missing linkage between test runs and builds
- inconsistent coverage formats
- pipelines without coverage configured
- multiple test runs per build ambiguity

## 14. Consistency with previous reports

- no change to formulas
- no change to Unknown rules
- no new metrics introduced
- BuildQuality provider contract remains valid
- CDC semantics remain locked
- aggregation semantics remain locked

## 15. Drift check

- assumptions:
  - the existing cached build persistence (`CachedPipelineRunEntity`) remains the build anchor
- uncertainties:
  - exact TFS test-run source field names for `TotalTests`, `PassedTests`, and `NotApplicableTests` remain **UNCERTAIN**
  - exact TFS coverage source field names or artifact element names for `CoveredLines` and `TotalLines` remain **UNCERTAIN**
  - source timestamps for test runs and coverage remain **UNCERTAIN**
- deviation from locked rules: none

No drift detected.

## 16. Open questions

None.
