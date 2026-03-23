# BuildQuality Missing Ingestion For Build 168570 — Code Analysis Report

## 1. Proven facts

The following facts are treated as proven input for this code-path analysis:

### TFS facts

- Build `168570` is on `refs/heads/main`
- Build `168570` has `reason = individualCI`
- Build `168570` has `result = partiallySucceeded`
- Direct TFS coverage query for build `168570` returns valid coverage data
- Direct TFS test-runs query for build `168570` returns test runs

### SQLite facts

- `CachedPipelineRuns` contains a build anchor row for external build id `168570`
- that build anchor has `SourceBranch = refs/heads/main`
- the corresponding pipeline default branch is also `refs/heads/main`
- `TestRuns` contains **zero** rows linked to that cached build
- `Coverages` contains **zero** rows linked to that cached build
- no stray/mislinked `TestRuns` rows were found
- no stray/mislinked `Coverages` rows were found

### Implications already proven

Therefore, for build `168570`, the failure is already proven **not** to be:

- UI display
- Health scope filtering
- provider logic
- branch mismatch
- persisted-row linkage mismatch

The failure must happen before child rows are written, or returned child rows must be dropped before insert.

## 2. Build-id batch construction analysis

The child-fetch batch is built in `PoTool.Api/Services/Sync/PipelineSyncStage.cs`.

1. `ExecuteAsync(...)` fetches pipeline runs from TFS with:
   - `context.PipelineDefinitionIds`
   - `branchName: null`
   - `minStartTime: context.PipelineWatermark`
   - `top: 100`
2. Those results become `runList`.
3. `SyncBuildQualityFactsAsync(...)` receives that `runList` and builds:
   - `requestedRunIds = runs.Select(run => run.RunId).Distinct().ToArray()`
4. It then loads cached build anchors only for those `requestedRunIds`:
   - `_context.CachedPipelineRuns.Where(run => run.ProductOwnerId == productOwnerId && requestedRunIds.Contains(run.TfsRunId))`
5. Only the resulting anchor keys are sent to:
   - `GetTestRunsByBuildIdsAsync(buildIds, ...)`
   - `GetCoverageByBuildIdsAsync(buildIds, ...)`

Important consequence: the child-fetch phase is **not** driven by "all cached builds missing children". It is driven only by the current `runList` returned by `GetPipelineRunsAsync(...)`.

That means a valid cached build anchor on `refs/heads/main` can still be skipped when:

- the build is older than the current incremental watermark window
- the build is outside the latest `top: 100` run batch
- the current sync returns zero runs
- the build already exists in `CachedPipelineRuns` but is not re-returned in the current pipeline-run fetch

So yes: the code path makes it entirely plausible that build `168570` is omitted **before** child retrieval even though its cached build anchor is valid and branch-aligned.

This is the only inspected failure point that naturally explains **both**:

- zero persisted `TestRuns`
- zero persisted `Coverages`

for the same anchored build.

## 3. Test retrieval analysis

The test-run retrieval path is in `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`.

### Request shape

The code does **not** call one test endpoint per build. Instead it:

1. normalizes the requested build ids
2. calls `GetBuildQueryWindowsAsync(...)`
3. loads build metadata for those ids through `_apis/build/builds?buildIds=...`
4. computes time windows around the returned build timestamps
5. calls the test endpoint per time window with:

```text
_apis/testresults/runs?minLastUpdatedDate=<window-start>&maxLastUpdatedDate=<window-end>&buildIds=<csv-of-valid-build-ids>
```

So the retrieval is nominally build-id-based, but it is actually a **windowed test-runs query plus a buildIds filter**.

### Post-response filtering

After the HTTP response:

- `ParseTestRunDto(...)` drops a row if:
  - `build.id` cannot be resolved
  - `totalTests` is missing
  - `passedTests` is missing
  - `notApplicableTests` is missing
- the caller then keeps only DTOs whose `BuildId` is in `buildWindows.ValidBuildIds`

### Can rows be dropped because required fields are missing?

Yes, but the exact behavior matters:

- missing `BuildId` in the payload causes the row to be dropped in `ParseTestRunDto(...)`
- missing raw counters also cause the row to be dropped in `ParseTestRunDto(...)`
- `UpsertTestRunsAsync(...)` would drop a DTO if `ExternalId` is null
- `UpsertTestRunsAsync(...)` also drops a DTO if build linkage cannot be resolved or any counters are negative

However, the current `RealTfsClient` parser does **not** reliably surface missing external ids as `null`. It executes:

```csharp
_ = TryGetIntProperty(run, "id", out var externalId);
ExternalId = externalId;
```

So an absent or unparsable `id` becomes `0`, not `null`, and therefore does **not** trigger the sync-stage `"missing stable external id"` guard for this client path.

### Most likely reason test runs still do not get persisted

Given the proven fact that direct TFS test-run queries return runs for build `168570`, the most likely code-path reason they still do not persist is **not** the test payload parser. The more likely reason is earlier: build `168570` never enters the `buildIds` batch for `GetTestRunsByBuildIdsAsync(...)` during the sync that produced the observed SQLite state.

If `168570` is absent from `requestedRunIds`, the test retrieval method is never asked for it, so zero `TestRuns` rows is the expected persisted outcome.

## 4. Coverage retrieval analysis

The coverage path is also in `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`.

### Request shape

Coverage retrieval is not one big batched HTTP call. The method:

1. normalizes the requested build ids
2. calls `GetBuildQueryWindowsAsync(...)` only to discover valid build ids
3. chunks valid ids in groups of 25
4. within each chunk, launches one HTTP request per build:

```text
_apis/testresults/codecoverage?buildId=<buildId>
```

So the public client contract is batched by build-id set, but the actual remote retrieval is effectively **per-build**, executed in parallel within 25-build chunks.

### How line counts are extracted

`ParseCoverageDtos(...)` reads:

- root `coverageData[]`
- then each entry's `coverageStats[]`
- then only the stat whose `label` is exactly `"Line"` or `"Lines"` (case-insensitive)
- then `covered` and `total`

If any of those elements are missing, that entry is silently skipped.

### Can coverage rows be dropped?

Yes:

- if the build id never enters the requested batch, coverage is never fetched for that build
- if the payload has no `coverageData` array, the parser yields no rows
- if `coverageStats` is missing, that entry is skipped
- if the label is not `"Line"` or `"Lines"`, that entry is skipped
- if `covered` or `total` is missing, that entry is skipped
- later, `ReplaceCoverageAsync(...)` drops DTOs whose `BuildId` cannot be linked or whose counters are negative

### Empty results are treated as success

Yes. This is important.

If coverage parsing yields zero rows, `GetCoverageByBuildIdsAsync(...)` still returns an empty list successfully. `ReplaceCoverageAsync(...)` then simply inserts nothing. There is no error and no warning for parser-level empty coverage.

### Most likely reason coverage still does not get persisted

There is a real secondary risk here: if the confirmed TFS coverage payload uses a label other than `"Line"` or `"Lines"`, the parser would silently drop it.

But for build `168570`, the stronger shared explanation remains earlier batch omission. That single omission explains why both test runs and coverage are missing at once. If `168570` is not present in the sync-stage `buildIds`, neither child retrieval path is asked to ingest it.

## 5. Pre-persistence filtering analysis

These are the exact guards and skip paths between remote child DTOs and persisted rows.

### Test runs

Pre-persistence and persistence-stage filters:

1. `SyncBuildQualityFactsAsync(...)`
   - entire build omitted if its `TfsRunId` is not in the current `runList`
   - entire build omitted if no cached anchor is found for the requested run id
2. `RealTfsClient.ParseTestRunDto(...)`
   - drops row if `build.id` is missing/unparseable
   - drops row if `totalTests`, `passedTests`, or `notApplicableTests` is missing
3. `RealTfsClient.GetTestRunsByBuildIdsAsync(...)`
   - drops parsed DTOs whose `BuildId` is not in `ValidBuildIds`
4. `PipelineSyncStage.UpsertTestRunsAsync(...)`
   - drops DTO if `buildAnchors.TryGetValue(dto.BuildId, ...)` fails
   - drops DTO if `dto.ExternalId is null`
   - drops DTO if any counter is negative
5. replace/upsert behavior
   - existing rows for the affected build set are loaded
   - matched `(internal BuildId, ExternalId)` rows are updated
   - unmatched incoming rows are inserted
   - existing rows not present in the new incoming key set are deleted as stale

Critical note for build `168570`: none of the replace/delete behavior can remove rows for that build unless `168570` is part of `affectedBuildIds`. If the build is omitted from the batch entirely, it is simply untouched and remains child-empty.

### Coverage

Pre-persistence and persistence-stage filters:

1. `SyncBuildQualityFactsAsync(...)`
   - entire build omitted if its `TfsRunId` is not in the current `runList`
   - entire build omitted if no cached anchor is found for the requested run id
2. `RealTfsClient.ParseCoverageDtos(...)`
   - silently yields nothing if `coverageData` is missing
   - silently skips entries without `coverageStats`
   - silently skips entries whose label is not `"Line"` or `"Lines"`
   - silently skips entries missing `covered` or `total`
3. `PipelineSyncStage.ReplaceCoverageAsync(...)`
   - removes all existing coverage rows for the affected build set first
   - drops DTO if `buildAnchors.TryGetValue(dto.BuildId, ...)` fails
   - drops DTO if any counter is negative
   - inserts all remaining DTOs

Again, if build `168570` never enters `affectedBuildIds`, none of this code runs for that build.

## 6. Logging / observability analysis

### Are dropped test runs logged?

Partially.

- `ParseTestRunDto(...)` logs warnings when build linkage is missing from the payload or required counters are missing
- `UpsertTestRunsAsync(...)` logs warnings for:
  - missing build linkage
  - missing stable external id
  - negative counters

### Are dropped coverage rows logged?

Only partially, and the most important parser-level drops are **not** logged.

- `ReplaceCoverageAsync(...)` logs warnings for:
  - missing build linkage
  - negative counters
- `ParseCoverageDtos(...)` does **not** log when:
  - `coverageData` is absent
  - `coverageStats` is absent
  - labels do not match `"Line"` / `"Lines"`
  - `covered` / `total` is missing

### Can the code silently produce zero persisted rows while sync appears successful?

Yes.

It can happen in at least two ways:

1. build `168570` is omitted from the child-fetch batch because `SyncBuildQualityFactsAsync(...)` only uses the current `runList`
2. coverage parsing yields zero rows and returns success without warnings

In the first case, there is no build-specific warning at all. The sync can still log success and return success. The only summary log is:

- `"Build quality ingestion synced {TestRunCount} test runs and {CoverageCount} coverage rows ..."`

That message is product-owner scoped, not build scoped, and does not reveal that a previously cached anchor such as `168570` was never retried.

## 7. Most likely root cause

The single most likely exact failure point is:

**`PipelineSyncStage.SyncBuildQualityFactsAsync(...)` constructs the child-ingestion build batch only from the current `GetPipelineRunsAsync(...)` result set, not from cached build anchors missing BuildQuality child rows.**

So if build `168570` already exists in `CachedPipelineRuns` but is not present in the current `runList` returned by the sync window (`PipelineWatermark` / `top: 100` / current fetch scope), then:

- `168570` is absent from `requestedRunIds`
- `168570` is absent from `buildAnchors`
- `GetTestRunsByBuildIdsAsync(...)` is never asked for `168570`
- `GetCoverageByBuildIdsAsync(...)` is never asked for `168570`
- zero `TestRuns` and zero `Coverages` remain the persisted state

This is the most likely explanation because it is the only inspected code-path failure that cleanly explains the proven facts for **both** child tables at the same time without relying on UI, provider, branch, or linkage errors.

## 8. Fix direction

Add one targeted backfill step in `PipelineSyncStage` so the child-fetch build-id batch includes cached build anchors in the current product/pipeline scope that still have no `TestRuns` and no `Coverages`, instead of relying only on the latest `runList`.
