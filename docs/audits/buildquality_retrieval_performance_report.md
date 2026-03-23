# BuildQuality Retrieval Performance Report

## 1. Scope measured

- Measured run: one clean local cache sync in **mock mode** by running `PoTool.Api` in Development with a fresh SQLite database at `/tmp/buildquality-perf/potool.db`, then triggering `POST /api/CacheSync/1/sync`.
- Product owner measured: `1` (`Commander Elena Marquez` in the mock seed).
- Rough number of builds involved: `428` cached pipeline runs were ingested for the BuildQuality child-fetch batch.
- Important limitation: this sandbox does **not** have real TFS credentials or a reachable real TFS endpoint, so the measured run exercised `PipelineSyncStage` plus the mock `ITfsClient`, **not** `RealTfsClient` HTTP retrieval. The new `RealTfsClient` instrumentation is present for the next real import, but its HTTP metrics were not measurable here.

## 2. Phase timings

Measured from the structured BuildQuality logs emitted during the clean mock import:

| Phase | Elapsed |
| --- | ---: |
| build-quality child ingestion total | `158 ms` |
| test-run retrieval total | `0 ms` |
| coverage retrieval total | `0 ms` |
| test-run persistence total | `67 ms` |
| coverage persistence total | `68 ms` |

Observed log lines:

- `BUILDQUALITY_TESTRUN_RETRIEVAL_SUMMARY: productOwnerId=1, requestedBuildCount=428, returnedDtoCount=0, elapsedMs=0`
- `BUILDQUALITY_COVERAGE_RETRIEVAL_SUMMARY: productOwnerId=1, requestedBuildCount=428, returnedDtoCount=0, elapsedMs=0`
- `BUILDQUALITY_TESTRUN_PERSISTENCE_SUMMARY: productOwnerId=1, persistedRowCount=0, insertedRowCount=0, updatedRowCount=0, removedRowCount=0, warningCount=0, elapsedMs=67`
- `BUILDQUALITY_COVERAGE_PERSISTENCE_SUMMARY: productOwnerId=1, persistedRowCount=0, insertedRowCount=0, removedRowCount=0, warningCount=0, elapsedMs=68`
- `BUILDQUALITY_CHILD_INGEST_SUMMARY: productOwnerId=1, childIngestionElapsedMs=158, requestedTestRunBuildCount=428, requestedCoverageBuildCount=428, returnedTestRunDtoCount=0, returnedCoverageDtoCount=0, persistedTestRunRowCount=0, persistedCoverageRowCount=0, testRunRetrievalElapsedMs=0, coverageRetrievalElapsedMs=0, testRunPersistenceElapsedMs=67, coveragePersistenceElapsedMs=68, warningCount=0`

## 3. Request volumes

From the measured mock import:

- attempted builds for test runs: `428`
- attempted builds for coverage: `428`
- test-run HTTP requests: **not measurable in this run** because mock mode bypassed `RealTfsClient`
- coverage HTTP requests: **not measurable in this run** because mock mode bypassed `RealTfsClient`
- total test-run DTO count returned: `0`
- total coverage DTO count returned: `0`
- total persisted test-run row count: `0`
- total persisted coverage row count: `0`

Additional context from the mock client logs:

- `Mock TFS client: GetTestRunsByBuildIdsAsync called for 428 builds`
- `Mock TFS client: GetCoverageByBuildIdsAsync called for 428 builds`

## 4. Slowest test-run builds

- Not measurable in this sandbox run because `RealTfsClient.GetTestRunsByBuildIdsAsync(...)` was not exercised.
- The new instrumentation now emits per-build log lines in the real client using:
  - `BUILDQUALITY_TESTRUN_BUILD_SUMMARY`
  - `BUILDQUALITY_TESTRUN_REQUEST_SUMMARY`
- On the next real TFS import, those logs will answer which build ids are slowest, how many pages each build needed, and how many HTTP calls were actually sent.

## 5. Bottleneck assessment

- Dominant bottleneck for the measured **mock mode** run: **persistence / SQLite was visible but still small** (`67 ms` + `68 ms` out of `158 ms` total).
- Dominant bottleneck for the **actual real-TFS slowdown under investigation**: **not yet measurable from this sandbox run** because remote HTTP volume, per-build sequential retrieval, and coverage endpoint latency all require `RealTfsClient` against a real server.
- Practical conclusion: the mock run does **not** indicate a SQLite-heavy problem. The next real import should use the new `RealTfsClient` logs to determine whether the dominant issue is HTTP request volume, sequential per-build retrieval, coverage retrieval, or a mixed profile.

## 6. Recommended next optimization

1. Run one clean import in a real TFS-connected environment and collect the new `BUILDQUALITY_TESTRUN_BUILD_SUMMARY`, `BUILDQUALITY_TESTRUN_REQUEST_SUMMARY`, and `BUILDQUALITY_COVERAGE_*` logs to identify the true slowest builds and total remote request count.
2. If the real logs confirm that per-build test-run retrieval dominates runtime, make one small follow-up change only after review: either reduce avoidable request count or introduce carefully bounded concurrency for the network-only retrieval phase while leaving persistence sequential.

## Reviewer notes

### What changed

- added focused BuildQuality instrumentation in `PipelineSyncStage` for child-ingestion timing, DTO counts, and persistence row counts
- added focused BuildQuality instrumentation in `RealTfsClient` for per-build test-run timing, request counts, and coverage timing summaries
- added a report documenting the one clean mock import run and its limitations

### What was intentionally not changed

- no retrieval logic changes
- no endpoint changes
- no batching or parallelization changes
- no schema/provider/UI changes

### Known limitations / follow-up

- the measured run used mock mode, so real TFS HTTP timings and per-build remote latency remain unmeasured until the same instrumentation is exercised in a real connected environment
