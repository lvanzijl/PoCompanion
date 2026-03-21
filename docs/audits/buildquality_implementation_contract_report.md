# Prompt 4 — BuildQuality Implementation Contract Report

## 1. Purpose

Define the exact implementation contract for exposing the locked `BuildQuality` slice through `PoTool.Core`, `PoTool.Api`, `PoTool.Shared`, and `PoTool.Client` without changing formulas, thresholds, Unknown rules, aggregation rules, or page-boundary decisions from the upstream reports.

## 2. In scope

- queries
- handlers
- DTOs
- endpoints
- client usage

## 3. Out of scope

- CDC changes
- aggregation changes
- UI design
- ingestion changes
- database schema changes

## 4. Locked decisions applied

The following decisions from `docs/audits/buildquality_application_page_integration_report.md` and `docs/audits/buildquality_data_aggregation_contract_report.md` are applied unchanged, with the application-page integration report taking precedence where wording overlaps:

- default branch only
- no nightly builds
- no feature branches
- no WorkItem linkage
- no test-type distinction
- no additional metrics
- `BuildQuality` remains time-agnostic at the CDC level
- time-window and sprint scoping are selected by the consuming query/handler path, not by the UI and not by the CDC
- Health remains a hub
- Backlog Health remains unchanged in this phase
- Build Quality is a separate consumer and must not be merged into `BacklogQuality`
- Delivery consumes sprint-scoped `BuildQuality`
- Pipeline Insights consumes pipeline- or repository-scoped `BuildQuality`
- canonical metrics remain only:
  - `SuccessRate`
  - `TestPassRate`
  - `TestVolume`
  - `Coverage`
  - `Confidence`
- build success mapping remains:
  - success = `succeeded`
  - failure = `failed + partiallySucceeded`
  - exclude = `canceled`
- formulas remain locked:
  - `SuccessRate = succeeded / (succeeded + failed + partiallySucceeded)`
  - `TestPassRate = passed / (total - notApplicable)`
  - `TestVolume = total - notApplicable`
  - `Coverage = covered_lines / total_lines`
  - `Confidence = BuildThresholdMet + TestThresholdMet`
- Unknown rules remain locked:
  - no builds -> `SuccessRate = Unknown`
  - no test runs -> `TestPassRate = Unknown`
  - no coverage OR `total_lines == 0` -> `Coverage = Unknown`
- thresholds remain locked:
  - `minimum_builds = 3`
  - `minimum_tests = 20`
- aggregation remains count-first / totals-first, ratios second
- percentages MUST NOT be averaged
- confidence remains exactly:
  - `BuildThresholdMet = 1 when EligibleBuilds >= minimum_builds, otherwise 0`
  - `TestThresholdMet = 1 when TestVolume >= minimum_tests, otherwise 0`
  - `Confidence = BuildThresholdMet + TestThresholdMet`

## 5. Query contracts (PoTool.Core)

`PoTool.Core` owns query contracts only. These contracts define request shape, required validation, and the shared DTO returned across the API/client boundary. They do not contain EF access, HTTP concerns, or formula implementation.

### 5.1 GetBuildQualityRollingWindowQuery

Purpose:

- request rolling-window `BuildQuality` for the Build Quality page / Health flow

Required parameters:

- `ProductIds`
  - required
  - one or more product identifiers
  - single-product scope is represented as one item
- `WindowStart`
  - required
  - inclusive lower time bound
- `WindowEnd`
  - required
  - inclusive upper time bound

Validation rules:

- `ProductIds` must contain at least one value
- product identifiers must be unique within the request
- `WindowStart` and `WindowEnd` must both be supplied
- `WindowEnd` must not be earlier than `WindowStart`

Expected output contract reference:

- `BuildQualityPageDto`

### 5.2 GetBuildQualitySprintQuery

Purpose:

- request sprint-scoped `BuildQuality` for Delivery consumption

Required parameters:

- `ProductIds`
  - required
  - one or more product identifiers in scope
- `TeamId`
  - optional
  - supplied only when Delivery scope is team-specific
- `SprintId`
  - required
  - canonical sprint identifier used by the application layer to resolve sprint metadata and sprint window

Validation rules:

- `ProductIds` must contain at least one value
- product identifiers must be unique within the request
- `SprintId` must be supplied
- `TeamId`, when supplied, must be non-empty

Expected output contract reference:

- `DeliveryBuildQualityDto`

### 5.3 GetBuildQualityPipelineDetailQuery

Purpose:

- request pipeline- or repository-scoped `BuildQuality` for Build Quality drill-down and Pipeline Insights reuse

Required parameters:

- one scope identifier:
  - `PipelineId`, or
  - `RepositoryId`
- one optional time context:
  - `WindowStart` and `WindowEnd`, or
  - `SprintId`

Validation rules:

- exactly one of `PipelineId` or `RepositoryId` must be supplied
- rolling-window context requires both `WindowStart` and `WindowEnd`
- `WindowEnd` must not be earlier than `WindowStart`
- `SprintId` may be supplied instead of a rolling window
- sprint context and rolling-window context must not both be supplied in the same request

Expected output contract reference:

- `PipelineBuildQualityDto`

## 6. Handler design (PoTool.Api)

`PoTool.Api` owns handlers, scope resolution, raw-data selection, and DTO mapping. It does not own formula semantics.

### 6.1 Shared BuildQuality provider

`PoTool.Api` must contain one shared BuildQuality provider/service that every BuildQuality handler calls.

Responsibilities:

- accept scoped raw data that is already filtered to the requested default-branch scope and requested time scope
- accept build facts, test-run facts, and coverage facts only after scope selection is complete
- perform build aggregation using counts first
- perform test aggregation using totals first
- perform coverage aggregation using totals first
- calculate the locked formulas exactly
- apply the locked Unknown handling exactly
- calculate `BuildThresholdMet`, `TestThresholdMet`, and `Confidence` exactly
- produce a canonical result object that handlers map into shared DTOs

Guarantees:

- single implementation of BuildQuality logic
- no duplication across handlers
- no consumer-specific reinterpretation of formulas
- no consumer-specific reinterpretation of Unknown
- no averaging of percentages

The shared provider is the only allowed place for:

- `SuccessRate` calculation
- `TestPassRate` calculation
- `TestVolume` calculation
- `Coverage` calculation
- Unknown determination
- confidence calculation

### 6.2 Query handlers

Every BuildQuality query handler must follow the same flow:

- resolve the request scope
- select already-cached raw facts for that scope
- pass only the in-scope facts into the shared BuildQuality provider
- map the provider output into the page-appropriate shared DTO

Handlers must not contain formulas.

#### GetBuildQualityRollingWindowQueryHandler

Scope resolution:

- resolve the requested `ProductIds`
- resolve the rolling window from `WindowStart` to `WindowEnd`
- enforce default-branch-only fact selection for the selected products

Data selection:

- select only in-scope default-branch build facts inside the rolling window
- select only test-run facts linked to the selected in-scope builds
- select only coverage facts linked to the selected in-scope builds

Provider call:

- call the shared BuildQuality provider with the already-filtered rolling-window facts

DTO mapping:

- map overall rolling-window output plus per-product breakdowns into `BuildQualityPageDto`

#### GetBuildQualitySprintQueryHandler

Scope resolution:

- resolve the requested `ProductIds`
- resolve optional `TeamId` context
- resolve sprint metadata and sprint window from `SprintId`
- enforce default-branch-only fact selection for the selected sprint scope

Data selection:

- select only in-scope default-branch build facts inside the resolved sprint window
- select only test-run facts linked to the selected in-scope builds
- select only coverage facts linked to the selected in-scope builds

Provider call:

- call the shared BuildQuality provider with the already-filtered sprint-window facts

DTO mapping:

- map sprint metadata, scope metadata, overall result, and any per-product breakdowns into `DeliveryBuildQualityDto`

#### GetBuildQualityPipelineDetailQueryHandler

Scope resolution:

- resolve either the requested pipeline scope or the requested repository scope
- resolve either rolling-window context or sprint context when present
- enforce default-branch-only fact selection for the selected pipeline/repository context

Data selection:

- select only in-scope default-branch build facts for the chosen pipeline/repository scope
- select only test-run facts linked to the selected in-scope builds
- select only coverage facts linked to the selected in-scope builds

Provider call:

- call the shared BuildQuality provider with the already-filtered pipeline/repository facts

DTO mapping:

- map scope metadata, canonical result, and any referenced pipeline-detail metadata into `PipelineBuildQualityDto`

The following are explicitly forbidden in all handlers:

- formula logic in handlers
- percentage averaging
- custom Unknown logic
- custom confidence logic
- page-specific reinterpretation of the five canonical metrics

## 7. DTO definitions (PoTool.Shared)

`PoTool.Shared` owns the DTOs that cross the API/client boundary. These DTOs transport canonical BuildQuality outputs plus supporting evidence. They do not create new metrics.

### 7.1 BuildQualityMetricsDto

Fields:

- `SuccessRate`
- `TestPassRate`
- `TestVolume`
- `Coverage`
- `Confidence`

Field contract:

- `SuccessRate`, `TestPassRate`, and `Coverage` must allow explicit `Unknown`
- `TestVolume` is transported as the locked `total - notApplicable` value
- `Confidence` is transported exactly as `BuildThresholdMet + TestThresholdMet`

### 7.2 BuildQualityEvidenceDto

Fields:

- `EligibleBuildCount`
- `SucceededCount`
- `FailedCount`
- `PartiallySucceededCount`
- `CanceledExcludedCount`
- `TotalTests`
- `PassedTests`
- `NotApplicableTests`
- `CoveredLines`
- `TotalLines`

Field contract:

- these fields are evidence only
- they exist to explain canonical outputs, Unknown states, and threshold sufficiency
- they must not be treated as alternative metrics

### 7.3 BuildQualityResultDto

Fields:

- scope metadata
- `Metrics` (`BuildQualityMetricsDto`)
- `Evidence` (`BuildQualityEvidenceDto`)
- explicit Unknown flags:
  - `IsSuccessRateUnknown`
  - `IsTestPassRateUnknown`
  - `IsCoverageUnknown`
- explicit Unknown reasons:
  - `NoEligibleBuilds`
  - `NoTestRuns`
  - `NoCoverage`
  - `CoverageTotalLinesZero`
- threshold flags:
  - `BuildThresholdMet`
  - `TestThresholdMet`

Field contract:

- scope metadata identifies the selected consumer scope and time context
- Unknown flags are explicit so the client never infers Unknown from missing values
- threshold flags are explicit so the client never recalculates confidence inputs

### 7.4 BuildQualityProductDto

Fields:

- product identity
- `Result` (`BuildQualityResultDto`)

Field contract:

- one instance per product in scope
- uses the same canonical result structure as the overall page-level result

### 7.5 Page-level DTOs

#### BuildQualityPageDto

Purpose:

- rolling-window response for the Build Quality page

Fields:

- rolling-window metadata
- applied scope metadata
- overall `BuildQualityResultDto`
- per-product collection of `BuildQualityProductDto`
- optional pipeline/repository breakdown references already prepared by the API

#### DeliveryBuildQualityDto

Purpose:

- sprint-window response for Delivery

Fields:

- sprint identity
- sprint window metadata
- applied product/team scope metadata
- overall `BuildQualityResultDto`
- optional per-product collection of `BuildQualityProductDto`

#### PipelineBuildQualityDto

Purpose:

- pipeline- or repository-scoped response for Pipeline Insights and Build Quality drill-down reuse

Fields:

- selected pipeline or repository identity
- optional sprint metadata or rolling-window metadata
- applied scope metadata
- overall `BuildQualityResultDto`
- references to existing pipeline-detail context already owned by Pipeline Insights

## 8. Endpoint definitions (PoTool.Api)

The API exposes thin HTTP endpoints that map request parameters into the `PoTool.Core` queries and return the shared DTOs.

### 8.1 GET /api/buildquality/rolling

Query mapping:

- maps request parameters to `GetBuildQualityRollingWindowQuery`

Parameters:

- `productIds`
- `windowStart`
- `windowEnd`

Response DTO:

- `BuildQualityPageDto`

### 8.2 GET /api/buildquality/sprint

Query mapping:

- maps request parameters to `GetBuildQualitySprintQuery`

Parameters:

- `productIds`
- `teamId`
- `sprintId`

Response DTO:

- `DeliveryBuildQualityDto`

### 8.3 GET /api/buildquality/pipeline

Query mapping:

- maps request parameters to `GetBuildQualityPipelineDetailQuery`

Parameters:

- `pipelineId` or `repositoryId`
- optional `windowStart`
- optional `windowEnd`
- optional `sprintId`

Response DTO:

- `PipelineBuildQualityDto`

## 9. Client consumption (PoTool.Client)

`PoTool.Client` consumes BuildQuality through typed API client usage wrapped by page-level frontend service abstractions.

Required client pattern:

- generated or typed API client performs the HTTP call
- page-level frontend service abstraction wraps the API client
- pages consume only the frontend service abstraction
- pages manage only asynchronous loading, page state, and presentation mapping

Explicitly required:

- no mediator usage in `PoTool.Client`
- no direct HTTP logic in pages
- no formula logic in pages
- no Unknown logic in pages

Per-page consumption:

- Build Quality page -> calls the page-level service abstraction that uses `GET /api/buildquality/rolling`
- Delivery -> calls the page-level service abstraction that uses `GET /api/buildquality/sprint`
- Pipeline Insights -> calls the page-level service abstraction that uses `GET /api/buildquality/pipeline`

Explicitly forbidden in the client:

- recomputation of metrics
- deriving formulas from evidence
- redefining Unknown
- recalculating `BuildThresholdMet`
- recalculating `TestThresholdMet`
- recalculating `Confidence`

## 10. Data flow (implementation view)

GetBuildQualityRollingWindowQuery / GetBuildQualitySprintQuery / GetBuildQualityPipelineDetailQuery
-> handler
-> scope selection
-> shared BuildQuality provider
-> DTO mapping
-> API response
-> typed client
-> page-level service abstraction
-> page

## 11. Single source of truth enforcement

The shared BuildQuality provider is the ONLY place where:

- formulas exist
- Unknown logic exists
- confidence is calculated

Enforcement rules:

- handlers MUST call the shared BuildQuality provider
- handlers MUST NOT implement formulas
- handlers MUST NOT implement Unknown handling
- handlers MUST NOT implement confidence logic
- DTOs MUST transport provider outputs only
- endpoints MUST only map requests and return DTOs
- clients MUST consume outputs without recomputation
- any duplicate logic = violation

## 12. Integration risks

- provider bypass
- handler duplication
- DTO drift
- endpoint misuse
- client-side recomputation

## 13. Consistency with previous reports

- formulas unchanged
- Unknown rules unchanged
- no new metrics
- CDC not reinterpreted

## 14. Drift check

- assumptions: none
- deviations: none

No drift detected.

## 15. Open questions

None.
