# Prompt 3 — BuildQuality Application & Page Integration Report

## 1. Purpose

Define how the locked `BuildQuality` CDC slice is exposed through the existing application architecture and consumed by the relevant PoTool pages without changing CDC semantics, aggregation rules, formulas, Unknown handling, or the locked metric set.

## 2. In scope

- application-layer exposure of `BuildQuality`
- query/service responsibilities
- page-level data contracts
- page responsibility boundaries
- health hub integration
- delivery integration
- pipeline insights integration

## 3. Out of scope

- CDC redesign
- data or aggregation redesign
- storage schema
- ingestion scheduling
- UI layout, colors, or component selection
- implementation sequencing
- new metrics

## 4. Locked decisions applied

The following constraints from `docs/audits/buildquality_data_aggregation_contract_report.md` and its upstream CDC dependency are applied unchanged:

- default branch only
- no feature branches
- no nightly builds
- no WorkItem linkage
- no test-type distinction
- no additional metrics
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
- aggregation remains count/totals first, ratios second
- percentages MUST NOT be averaged
- `BuildQuality` remains time-agnostic in CDC semantics
- Health usage remains a rolling window selected by the consumer
- Delivery usage remains a sprint window selected by the consumer
- Health remains a hub
- Backlog Health remains unchanged in this phase
- Build Quality is added as a separate consumer and must not be merged into `BacklogQuality`

## 5. Application-layer responsibilities

The repository already uses the locked pattern:

- query contracts in `PoTool.Core`
- query handlers in `PoTool.Api`
- shared DTOs in `PoTool.Shared`
- typed client services and pages in `PoTool.Client`

`BuildQuality` must be exposed through that pattern, not through a new architecture.

### `PoTool.Core`

`PoTool.Core` should contain only the application contracts needed to request BuildQuality data for the known consumer scopes:

- a rolling-window query contract for the Build Quality page / Health hub flow
- a sprint-window query contract for Delivery consumption
- a pipeline- or repository-level detail query contract for Build Quality detail and Pipeline Insights reuse

`PoTool.Core` owns:

- request parameters such as product scope, sprint scope, rolling-window bounds, and repository/pipeline identifiers
- validation of request inputs where query parameters are required
- interface contracts for any shared read-provider or application-facing BuildQuality read service needed by multiple handlers

`PoTool.Core` must not own:

- EF queries
- controller logic
- client view-model logic
- CDC reinterpretation

### `PoTool.Api`

`PoTool.Api` should contain the handler and orchestration layer that turns selected cached facts into consumer DTOs.

`PoTool.Api` owns:

- query handlers that resolve the requested scope
- selection of the correct in-scope facts for:
  - rolling window usage in Health / Build Quality
  - sprint window usage in Delivery
  - pipeline/repository detail usage in Pipeline Insights
- reuse of one shared BuildQuality application service / adapter so handlers do not duplicate formula or Unknown logic
- mapping canonical BuildQuality outputs into shared DTOs

`PoTool.Api` must not:

- create page-specific semantics inside handlers
- let each handler reimplement formulas
- average percentages
- reinterpret confidence
- let Health handlers apply sprint semantics or Delivery handlers apply rolling semantics

### `PoTool.Shared`

`PoTool.Shared` should contain only DTOs that cross the API/client boundary for BuildQuality consumers.

These DTOs should carry:

- consumer scope metadata
- locked BuildQuality metric values
- supporting evidence counts needed to explain Unknown and confidence states
- per-product and, where needed, per-pipeline or per-repository breakdowns

Those supporting counts are transport evidence, not new canonical metrics.

`PoTool.Shared` must not:

- embed UI-only presentation concerns
- add new quality metrics
- restate CDC formulas in a way that allows consumers to diverge

### `PoTool.Client`

`PoTool.Client` should consume BuildQuality only through typed frontend service abstractions backed by the generated API clients.

`PoTool.Client` owns:

- route-level orchestration
- asynchronous loading
- page-local state
- mapping DTOs into view-specific labels or grouping

`PoTool.Client` must not:

- call TFS directly
- use mediator directly
- recompute BuildQuality formulas
- decide its own Unknown rules
- merge Backlog Health and Build Quality behavior in this phase

## 6. Page integration model

### Health hub

The Health hub is responsible for navigation and context, not for owning BuildQuality semantics.

Its role in this phase is:

- remain the entry point at `/home/health`
- expose separate navigation paths for:
  - Backlog Health
  - Build Quality
- pass the current product/portfolio context into the selected destination

The hub must not:

- merge backlog and build quality data into one contract
- compute quality metrics
- redefine rolling-window rules

### Backlog Health page

Backlog Health remains unchanged in this phase.

Backlog Health is responsible only for current-state backlog validation and backlog-health concerns already owned by `BacklogQuality`.

Backlog Health must not absorb:

- build outcome metrics
- test pass metrics
- test volume metrics
- coverage metrics
- BuildQuality confidence
- pipeline-specific build/test/coverage interpretation

### Build Quality page

The Build Quality page is the Health-area consumer for rolling-window BuildQuality.

Its responsibility is to present rolling-window BuildQuality for the selected scope using the locked five metrics and explicit Unknown/confidence information.

It consumes:

- a rolling-window BuildQuality summary for the selected scope
- per-product BuildQuality breakdowns for the same rolling window
- supporting evidence counts that explain confidence and Unknown states
- optional repository/pipeline drill-down data only if already computed by application queries for the same rolling scope

It must not:

- redefine formulas
- choose a sprint window
- interpret pipeline duration or other pipeline-specific diagnostics as BuildQuality metrics
- absorb Backlog Health logic
- fetch or aggregate raw data in the browser

### Delivery

Delivery remains a hub at `/home/delivery`.

Within Delivery semantics, BuildQuality appears only as sprint-scoped quality context for delivery interpretation.

Delivery is responsible for:

- consuming sprint-window BuildQuality outputs selected by sprint scope
- aligning BuildQuality to sprint reporting without changing BuildQuality semantics
- keeping sprint scoping outside the CDC and inside the application selection layer

Delivery must consume only sprint-window BuildQuality data and must not:

- use rolling-window BuildQuality in place of sprint data
- redefine confidence for sprint reporting
- mix BuildQuality with commitment, velocity, or churn formulas

### Pipeline Insights

Pipeline Insights already owns pipeline-specific analytics based on cached pipeline run data.

BuildQuality relates to Pipeline Insights as a reusable canonical quality interpretation over a selected pipeline/repository scope.

Pipeline Insights may reuse:

- existing pipeline/product/repository scope resolution
- cached run selection patterns
- team/sprint context selection already used for sprint-scoped pipeline analysis
- BuildQuality canonical success/test/coverage/confidence outputs for the selected pipeline/repository scope

Pipeline Insights must keep page-specific ownership of:

- pipeline scatter/duration analysis
- top-troubled-pipeline ranking
- pipeline-specific troubleshooting context
- any non-BuildQuality pipeline metrics already present on the page

Pipeline Insights must not:

- redefine BuildQuality formulas to fit existing pipeline charts
- leak duration or ranking logic into BuildQuality CDC/application semantics
- substitute pipeline-health wording for canonical Unknown/confidence rules

## 7. Data contracts per page

### Build Quality page

The Build Quality page needs a page-level contract with three responsibility groups.

#### Summary data

- selected rolling-window metadata:
  - window start
  - window end
  - default branch identifier
  - applied scope identifiers
- overall metric bundle:
  - `SuccessRate`
  - `TestPassRate`
  - `TestVolume`
  - `Coverage`
  - `Confidence`
- supporting evidence needed to explain those values:
  - eligible build count
  - succeeded count
  - failed count
  - partiallySucceeded count
  - canceled excluded count
  - total tests
  - passed tests
  - notApplicable tests
  - covered lines
  - total lines

#### Per-product data

For each product in scope:

- product identity
- the same locked metric bundle
- the same supporting evidence counts
- optional repository/pipeline breakdown references when the page needs to drill down without re-querying raw facts in the UI

#### Confidence / Unknown information

The contract must carry explicit state needed by the page to explain the locked semantics without recomputation:

- whether build threshold is met
- whether test threshold is met
- whether `SuccessRate` is `Unknown`
- whether `TestPassRate` is `Unknown`
- whether `Coverage` is `Unknown`
- the canonical reason for each Unknown state:
  - no eligible builds
  - no test runs
  - no coverage
  - `total_lines == 0`

### Delivery

Delivery needs a sprint-scoped quality contract.

It should include:

- sprint identity and sprint window metadata
- selected product/team scope metadata
- the locked metric bundle for the sprint window
- supporting evidence counts required to explain confidence and Unknown states in that sprint
- per-product sprint BuildQuality breakdowns when Delivery compares products inside the same sprint

It must not include:

- rolling-window summaries
- backlog-health data
- pipeline-only troubleshooting details unless a dedicated child view explicitly consumes a BuildQuality detail contract

### Pipeline Insights

Pipeline Insights needs a build-level or pipeline-level quality contract aligned to its existing pipeline scope.

It should include:

- selected sprint/team/product scope metadata
- pipeline or repository identity
- the locked metric bundle for that selected pipeline/repository scope
- supporting evidence counts required to explain confidence and Unknown states
- references to the existing pipeline-run detail data already owned by Pipeline Insights

It may sit alongside existing pipeline DTOs, but BuildQuality fields must remain a distinct canonical subset rather than being folded into duration/ranking data.

## 8. Data flow (text diagram)

```text
TFS / Azure DevOps build facts
    + test-run facts
    + coverage facts
        ->
SyncStage / cached persistence already used by the application
        ->
selected in-scope facts (default branch only; rolling or sprint window chosen by consumer)
        ->
canonical BuildQuality computation
    - aggregate counts/totals first
    - compute locked ratios second
    - apply locked Unknown rules
    - apply locked confidence thresholds
        ->
PoTool.Api query handlers orchestrate consumer scope
        ->
PoTool.Shared BuildQuality DTOs
        ->
PoTool.Client typed services / generated API clients
        ->
page-level consumers:
    - Health hub navigation -> Build Quality
    - Backlog Health (unchanged; no BuildQuality contract)
    - Delivery sprint views
    - Pipeline Insights pipeline/repository views
```

## 9. Separation-of-concerns rules

- CDC owns BuildQuality semantics.
- The data/aggregation contract owns raw-to-canonical mapping, count-first aggregation, ratio formulas, Unknown rules, and confidence rules.
- The application layer owns scope selection, orchestration, and DTO projection.
- Shared DTOs own transport shape only.
- The UI owns presentation, loading state, and local interaction only.

Explicit prohibitions:

- handlers must not each implement their own BuildQuality math
- handlers must not average precomputed percentages
- handlers must not change default-branch-only scope
- handlers must not mix rolling and sprint windows
- pages must not derive alternate formulas from evidence counts
- pages must not convert Unknown into zero
- pages must not turn confidence into a different scoring model
- Backlog Health must not absorb BuildQuality
- Pipeline Insights must not become the owner of BuildQuality semantics

## 10. Integration risks

- duplicating BuildQuality logic across multiple handlers
- page-specific reinterpretation of confidence
- mixing rolling and sprint windows in the same contract
- leaking pipeline-specific details into Health
- merging Backlog Health and Build Quality too early
- exposing supporting evidence counts in a way that encourages UI-side recomputation
- letting Pipeline Insights duration or ranking logic reshape canonical BuildQuality meaning

## 11. Consistency with input report

Confirmed:

- formulas preserved exactly:
  - `SuccessRate = succeeded / (succeeded + failed + partiallySucceeded)`
  - `TestPassRate = passed / (total - notApplicable)`
  - `TestVolume = total - notApplicable`
  - `Coverage = covered_lines / total_lines`
  - `Confidence = BuildThresholdMet + TestThresholdMet`
- Unknown rules preserved exactly:
  - no builds -> `SuccessRate = Unknown`
  - no test runs -> `TestPassRate = Unknown`
  - no coverage OR `total_lines == 0` -> `Coverage = Unknown`
- no new metrics introduced
- no CDC reinterpretation introduced

## 12. Drift check

- assumptions made: none
- newly introduced concept: none beyond application/query/page contracts required to expose the already locked `BuildQuality` slice
- deviation from locked rules: none

No drift detected.

## 13. Open questions introduced

None.
