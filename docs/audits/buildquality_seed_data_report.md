# BuildQuality Seed Data Report

Date: 2026-03-24

## 1. Dataset extended

- Extended product: `Incident Response Control`
- Added through the existing battleship mock dataset in `BattleshipMockDataFacade`
- Added records:
  - `5 builds`
  - `3 test runs`
  - `3 coverage entries`
- Existing repository/pipeline mapping remained unchanged; the added BuildQuality facts flow through the normal mock TFS → sync/cache → query handler path.

## 2. Scenario coverage

Included edge cases in the Incident Response Control seeded BuildQuality slice:

- build with tests **and** coverage
- build with tests but **no** coverage
- build with coverage but **no** tests
- build with neither tests nor coverage
- mixed build results across the same product:
  - `Succeeded`
  - `Failed`
  - `PartiallySucceeded`

Scenario build IDs added in the battleship dataset:

- `910001` → tests + coverage
- `910002` → tests + coverage
- `910003` → tests + no coverage
- `910004` → coverage + no tests
- `910005` → no tests + no coverage

## 3. Test validation

Assertions added to existing tests:

- `PoTool.Tests.Unit/Services/MockTfsClientTests.cs`
  - verifies the battleship Incident Response Control dataset exposes the 5 linked builds
  - verifies test-run linkage exists only for builds `910001`, `910002`, and `910003`
  - verifies coverage linkage exists only for builds `910001`, `910002`, and `910004`
  - verifies the seeded mix produces:
    - known SuccessRate
    - known TestPassRate when tests exist
    - known Coverage when coverage exists
    - positive Confidence
- `PoTool.Tests.Unit/Handlers/BuildQualityQueryHandlerTests.cs`
  - extends the existing product-owner scope seed builder with optional BuildQuality edge-case records
  - verifies sprint-scoped BuildQuality remains known when some builds are missing tests or coverage but the scope still has valid data
  - verifies Unknown remains limited to the dimensions that truly lack data
- `PoTool.Tests.Unit/Audits/BuildQualitySeedDataReportDocumentTests.cs`
  - verifies this report exists with the required sections and seed-data facts

Scenarios now covered:

- seeded mock retrieval through the existing battleship dataset
- default-branch BuildQuality calculation with mixed build outcomes
- missing test data on one build without making the whole product Unknown
- missing coverage on one build without making the whole product Unknown
- fully missing child data on one build while confidence still comes from the wider scope

## 4. UI validation (manual)

Validation method:

1. Ran the hosted app in mock mode with a temporary SQLite database.
2. Selected the seeded Product Owner and used the normal `/sync-gate` flow.
3. Opened `/home/health` after sync completed.

Observed Health page result after seeding:

- Overall Build Quality
  - Builds: `40%`
  - Tests: `90%`
  - Coverage: `74%`
  - Confidence: `2`
- Incident Response Control
  - Builds: `40%`
  - Tests: `90%`
  - Coverage: `74%`
  - Confidence: `2`
- Crew Safety Operations intentionally remained `Unknown` because only one existing product was extended.

Observed evidence from the seeded scope:

- Eligible builds: `5`
- Succeeded builds: `2`
- Failed builds: `2`
- Partially succeeded builds: `1`
- Test volume: `524`
- Total tests: `535`
- Passed tests: `474`
- Not applicable tests: `11`
- Covered lines: `17050`
- Total lines: `23100`

Screenshot reference suitable for PR context:

- https://github.com/user-attachments/assets/f706fff5-5e76-4e13-a59d-98878f5f5f4c

## 5. Issues found

- No additional product or query-layer issues were found while extending the existing battleship dataset.
- A direct visit to Health before sync still shows Unknown values, but this is expected because the cache has not been seeded yet; the issue requirement was validated after normal seeding/sync.

## 6. Final verdict

READY
