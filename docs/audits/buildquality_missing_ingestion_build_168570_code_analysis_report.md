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

1. `ExecuteAsync(...)` still fetches pipeline runs from TFS with:
   - `context.PipelineDefinitionIds`
   - `branchName: null`
   - `minStartTime: context.PipelineWatermark`
   - `top: 100`
2. Those results become `runList` and are upserted into `CachedPipelineRuns` first.
3. `SyncBuildQualityFactsAsync(...)` then queries cached build anchors for the current product/pipeline scope and computes:
   - `hasTestRuns = EXISTS(TestRuns where BuildId == cachedBuild.Id)`
   - `hasCoverage = EXISTS(Coverages where BuildId == cachedBuild.Id)`
4. A cached build is considered incomplete when either child set is missing.
5. The incomplete set is ordered by `FinishedDateUtc DESC` (then build id descending), capped by `MaxBuildQualityBuildBatchSize = 25`, and only that filtered set is sent to:
   - `GetTestRunsByBuildIdsAsync(buildIds, ...)`
   - `GetCoverageByBuildIdsAsync(buildIds, ...)`

Important consequence: the child-fetch phase is now driven by **cached build anchors that are incomplete**, not just by the current `runList` returned by `GetPipelineRunsAsync(...)`.

That means build `168570` should now be retried whenever all of the following are true:

- the cached build anchor is still in the current product/pipeline scope
- the build still has missing `TestRuns` or missing `Coverages`
- the build is recent enough to remain inside the temporary 25-build cap

The current remaining ways such a build can still be skipped are now narrower:

- there is no cached anchor row for that build in `CachedPipelineRuns`
- the build already has both `TestRuns` and `Coverages`
- more than 25 newer incomplete builds exist in the same scoped cache

So the original omission mechanism for build `168570` has been removed, but the temporary safety cap remains an explicit follow-up constraint.

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

Given the proven fact that direct TFS test-run queries return runs for build `168570`, the most likely code-path reason they still would not persist is **not** the test payload parser. The more likely reason would again be earlier batch selection: build `168570` must enter the filtered incomplete `buildIds` batch for `GetTestRunsByBuildIdsAsync(...)`.

If `168570` is absent from that incomplete/capped selection, the test retrieval method is never asked for it, so zero `TestRuns` rows remains the expected persisted outcome.

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
- then only the stat whose `label` is exactly `"Line" or "Lines"` (case-insensitive)
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

But for build `168570`, the stronger shared explanation still remains earlier batch omission. If `168570` is not present in the sync-stage incomplete `buildIds`, neither child retrieval path is asked to ingest it.

## 5. Pre-persistence filtering analysis

These are the exact guards and skip paths between remote child DTOs and persisted rows.

### Test runs

Pre-persistence and persistence-stage filters:

1. `SyncBuildQualityFactsAsync(...)`
   - entire build omitted if it is already complete in SQLite
   - entire build omitted if it falls outside the temporary 25-build incomplete cap
   - entire build omitted if no cached anchor is found for the scoped run id
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
   - entire build omitted if it is already complete in SQLite
   - entire build omitted if it falls outside the temporary 25-build incomplete cap
   - entire build omitted if no cached anchor is found for the scoped run id
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

1. build `168570` is omitted from the incomplete child-fetch batch because it falls outside the temporary 25-build cap
2. coverage parsing yields zero rows and returns success without warnings

The observability is now better than before because the sync logs:

- `BUILDQUALITY_CHILD_INGEST_SELECTION`
- `"Build quality ingestion synced {TestRunCount} test runs and {CoverageCount} coverage rows ..."`

The selection log explicitly records:

- `originalScopeBuildCount`
- `completeBuildCount`
- `incompleteBuildCount`
- `cappedBuildCount`
- `selectedBuildIds`

So the code now makes it visible whether a cached build such as `168570` was selected or skipped by the cap.

## 7. Most likely root cause

The single most likely exact failure point is:

In the old implementation it was:

**`PipelineSyncStage.SyncBuildQualityFactsAsync(...)` constructed the child-ingestion build batch only from the current `GetPipelineRunsAsync(...)` result set, not from cached build anchors missing BuildQuality child rows.**

That specific failure mode is now addressed by the current implementation, which selects cached builds missing either child dataset before calling the TFS child-retrieval methods.

The main remaining limitation is the temporary safety cap:

- if more than 25 newer incomplete builds exist in scope
- then an older incomplete build such as `168570` can still be deferred
- but that deferral is now explicit in the selection log

## 8. Fix direction

The implemented fix direction is:

- build the child-fetch batch from cached scoped builds that are incomplete
- treat `missing TestRuns OR missing Coverages` as incomplete
- sort by `FinishedDateUtc DESC`
- cap the retrieval set at `25`
- log the scoped totals and selected build ids via `BUILDQUALITY_CHILD_INGEST_SELECTION`

Known follow-up:

- replace the temporary 25-build cap with smarter windowing once validation is complete
