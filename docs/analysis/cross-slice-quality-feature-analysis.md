# Cross-Slice Quality Feature Analysis

## Summary

**Final classification: Partial**

The system does **not** currently provide a true cross-slice quality summary.

What exists today is a set of adjacent quality surfaces:

- a **Build Quality** summary in the Health workspace,
- a separate **Pipeline Insights** workspace,
- a **Build Quality overlay** inside the Pipeline Insights build drawer,
- and a **Pipeline Insights** navigation tile in Trends that is actually driven by Build Quality data.

Those pieces are useful, but they do **not** satisfy the definition of a true cross-slice summary:

- the two slices are **not combined into one shared model or query**,
- there is **no single derived conclusion** that uses both datasets,
- there is **no conflict-resolution rule** when one slice is healthy and the other is degrading,
- and the main user-facing entry points use **different time windows** (rolling window vs selected sprint vs arbitrary Trends date range).

The dominant state is therefore **Partial** rather than Complete, Absent, or Duplicated. There is also a **localized misleading risk**: the Trends workspace “Pipeline Insights” tile is sourced from Build Quality, not from the Pipeline Insights dataset.

---

## Feature Inventory

### Pages, hubs, tiles, and shared components

| Location | Route / surface | Purpose | Main data sources | References Build Quality | References Pipeline Insights | Notes |
|---|---|---|---|---|---|---|
| `PoTool.Client/Pages/Home/HealthWorkspace.razor` | `/home/health` | Health hub navigation | None; static navigation tiles | Yes, by navigation target/copy | No | Hub only; no runtime health summary is computed here. |
| `PoTool.Client/Pages/Home/HealthOverviewPage.razor` | `/home/health/overview` | Build Quality overview for active Product Owner | `IBuildQualityService.GetRollingWindowAsync()` → `GET /api/buildquality/rolling` → `GetBuildQualityRollingWindowQueryHandler` → `EfBuildQualityReadStore` + `BuildQualityProvider` | Yes | No | Canonical Build Quality page. |
| `PoTool.Client/Pages/Home/PipelineInsights.razor` | `/home/pipeline-insights` | Sprint-scoped pipeline stability and diagnostics per product | `IPipelinesClient.GetInsightsEnvelopeAsync()` → `GET /api/pipelines/insights` → `GetPipelineInsightsQueryHandler` → `EfPipelineInsightsReadStore`; plus per-pipeline `IBuildQualityService.GetPipelineAsync()` → `GET /api/buildquality/pipeline` → `GetBuildQualityPipelineDetailQueryHandler` | Yes, in drawer/overlay only | Yes | Only place that shows both slices on one screen, but not as a unified conclusion. |
| `PoTool.Client/Pages/Home/TrendsWorkspace.razor` | `/home/trends` tile: “Pipeline Insights” | Navigation signal for pipeline-related trends | `IBuildQualityService.GetRollingWindowAsync()` → `TrendsWorkspaceTileSignalService.GetPipelineSignal(...)` | Yes | No | Tile label says “Pipeline Insights”, but the signal comes from Build Quality only. |
| `PoTool.Client/Pages/Home/HomePage.razor` | `/home` workspace tiles | Home dashboard with workspace-level prompts | `WorkspaceSignalService` calls validation, delivery, trend, and planning sources | No direct Build Quality use in this page | No direct Pipeline Insights use in this page | No cross-slice delivery-quality summary is shown here. |
| `PoTool.Client/Pages/Home/ValidationTriagePage.razor` | `/home/validation-triage` | Validation issue triage | `WorkItemService.GetValidationTriageSummaryAsync()` → `GET /api/workitems/validation-triage` | No | No | Health/backlog validation only. |
| `PoTool.Client/Pages/Home/SubComponents/HealthProductSummaryCard.razor` | Embedded health card | Lightweight per-product backlog readiness summary | `WorkItemService.GetHealthWorkspaceProductSummaryAsync()` → `GET /api/workitems/health-summary/{productId}` | No | No | Health/backlog readiness only. |
| `PoTool.Client/Components/Common/BuildQualitySummaryComponent.razor` | Shared component | Render Build Quality summary panels | `BuildQualityResultDto` | Yes | No | Shared presenter; no cross-slice logic. |
| `PoTool.Client/Components/Common/BuildQualityCompactComponent.razor` | Shared component | Render compact Build Quality metrics | `BuildQualityResultDto` | Yes | No | Used in Pipeline Insights drawer and Sprint Trend page. |
| `PoTool.Client/Pages/Home/Components/PipelineBreakdownTable.razor` | Shared component | Render per-pipeline breakdown with delta/trend | `PipelineBreakdownEntryDto` | No | Yes | Pipeline-only interpretation. |
| `PoTool.Client/Pages/Home/Components/TroubleEntryCard.razor` | Shared component | Render “Top pipelines in trouble” cards | `PipelineTroubleEntryDto` | No | Yes | Pipeline-only interpretation. |
| `PoTool.Client/Pages/Home/Components/WorkspaceTileBadge.razor` | Shared component | Render tile-level state badges | `StatusTileSignal` | Indirectly, via Trends tile signal | No | Presentation only. |

### Relevant API endpoints and DTOs

| Surface | Endpoint / DTOs | Observed contract |
|---|---|---|
| Build Quality overview | `GET /api/buildquality/rolling`; `BuildQualityPageDto`, `BuildQualityProductDto`, `BuildQualityResultDto` | Build Quality-only summary across rolling window. |
| Build Quality pipeline overlay | `GET /api/buildquality/pipeline`; `PipelineBuildQualityDto` | Build Quality-only detail for one sprint + one pipeline or repository. |
| Pipeline Insights workspace | `GET /api/pipelines/insights`; `PipelineInsightsDto`, `ProductPipelineInsightsDto`, `PipelineTroubleEntryDto`, `PipelineBreakdownEntryDto`, `PipelineScatterPointDto` | Pipeline-only sprint analytics. |
| Health backlog product summary | `GET /api/workitems/health-summary/{productId}`; `HealthWorkspaceProductSummaryDto` | Backlog health only. |
| Validation Triage | `GET /api/workitems/validation-triage`; `ValidationTriageSummaryDto` | Validation health only. |

### Inventory conclusion

There are multiple health/quality surfaces, but they are **slice-specific**:

- **Build Quality** has its own page and DTO family.
- **Pipeline Insights** has its own page and DTO family.
- **Health workspace** is mostly a navigation shell plus backlog/validation summaries.
- No inspected page, endpoint, or DTO serves as a **canonical combined Build Quality + Pipeline Insights summary**.

---

## Signal Mapping

### Build Quality signals

Source chain:

- `HealthOverviewPage.razor`
- `IBuildQualityService`
- `BuildQualityController.GetRolling(...)`
- `GetBuildQualityRollingWindowQueryHandler`
- `BuildQualityProvider.Compute(...)`

Signals shown:

- **Success rate** (`BuildQualityMetricsDto.SuccessRate`)
- **Test pass rate** (`BuildQualityMetricsDto.TestPassRate`)
- **Coverage** (`BuildQualityMetricsDto.Coverage`)
- **Confidence** (`BuildQualityMetricsDto.Confidence`)
- Evidence counts:
  - eligible / succeeded / failed / partially succeeded builds
  - total / passed / not-applicable tests
  - covered / total lines
- Unknown-state reasons:
  - no eligible builds
  - no test runs
  - no coverage / zero total lines

Signal type:

- Mostly **derived metrics** from raw facts.
- Confidence is a **derived status-like score**, but only for data sufficiency.
- No improving/stable/degrading trend is produced for Build Quality.

### Pipeline Insights signals

Source chain:

- `PipelineInsights.razor`
- `PipelinesController.GetInsights(...)`
- `GetPipelineInsightsQueryHandler`
- `EfPipelineInsightsReadStore`

Signals shown globally and per product:

- **Total builds**
- **Completed builds**
- **Failed builds**
- **Failure rate**
- **Warning builds / warning rate**
- **Succeeded builds / success rate**
- **Median duration**
- **P90 duration**
- **Top pipelines in trouble**
- **Scatter points** with start time, finish time, duration, branch, run result

Signals shown per pipeline:

- **Delta failure rate vs previous sprint**
- **Half-sprint trend**:
  - Improving
  - Degrading
  - Stable
  - Insufficient
- **First-half vs second-half failure rate**

Signal type:

- Mix of **raw counts**, **aggregated metrics**, and **derived trend labels**.
- The strongest derived status in this slice is `PipelineHalfSprintTrend`, but it is **pipeline-level**, not product-level and not cross-slice.

### Health / backlog signals

Source chain:

- `ValidationTriagePage.razor` / `WorkspaceSignalService.GetHealthSignalAsync(...)`
- `GET /api/workitems/validation-triage`
- `ValidationTriageSummaryDto`

Signals shown:

- Structural integrity issue counts
- Refinement readiness issue counts
- Refinement completeness issue counts
- Missing effort counts
- Top validation rule groups

Source chain:

- `HealthProductSummaryCard.razor`
- `GET /api/workitems/health-summary/{productId}`
- `HealthWorkspaceProductSummaryDto`

Signals shown:

- Ready effort
- Features ready in pending epics
- Top epics by refinement score

Signal type:

- Mostly **derived backlog-health indicators**.
- Not execution-quality or pipeline-health signals.

### Home and Trends tile signals

#### Home workspace tiles

`HomePage.razor` loads four independent strings through `WorkspaceSignalService`:

- Health
- Delivery
- Trends
- Planning

These are **workspace prompts**, not a combined quality summary. They do not inspect Build Quality + Pipeline Insights together.

#### Trends “Pipeline Insights” tile

`TrendsWorkspace.razor` calls `BuildQualityService.GetRollingWindowAsync(...)`, then `TrendsWorkspaceTileSignalService.GetPipelineSignal(...)`.

Signals shown:

- Stable
- Unstable
- Insufficient data / No data

Calculation used:

- It reads **Build Quality success rate only**.
- It converts that to failure rate.
- It compares against a fixed threshold (`20%`) to choose Stable vs Unstable.

This is a **derived signal**, but it is **Build Quality-only**, despite the tile being labeled “Pipeline Insights”.

---

## Cross-Slice Analysis

### Are Build Quality and Pipeline Insights shown separately, visually combined, or logically combined?

#### 1. Shown separately

Yes.

The main delivery-quality surfaces are separate:

- `/home/health/overview` shows Build Quality only.
- `/home/pipeline-insights` shows Pipeline Insights only for its main summaries, rankings, scatter, and breakdowns.

Their backend contracts are also separate:

- Build Quality uses `BuildQualityPageDto` / `PipelineBuildQualityDto`.
- Pipeline Insights uses `PipelineInsightsDto` and related DTOs.

#### 2. Combined visually

**Only partially, and only in one narrow place.**

The Pipeline Insights build drawer loads Build Quality detail for the selected pipeline run and displays it under a “Build Quality” section. That is a **visual side-by-side presentation**:

- the drawer keeps the original run result, timing, and branch from Pipeline Insights,
- then renders Build Quality metrics beneath it using `BuildQualityCompactComponent`.

However, that combination is only present in the drawer and only after an explicit click on one point.

#### 3. Combined logically

**No.**

No inspected page, handler, or DTO uses both datasets to produce a single conclusion.

Examples of what is missing:

- no shared handler that loads both `IBuildQualityReadStore` and `IPipelineInsightsReadStore`,
- no shared DTO that contains both Build Quality and Pipeline Insights with a final status,
- no rule that says, for example, “quality is degrading because failure rate is rising and coverage is falling”,
- no rule that resolves disagreement between the slices.

### Is there any place where both datasets produce a single conclusion?

**No.**

The closest thing is the Pipeline Insights drawer, but it still leaves interpretation to the user:

- the Pipeline Insights result badge remains a pipeline-run status,
- the Build Quality block remains a separate metric panel,
- nothing derives a synthesized verdict from both.

So the system currently supports **manual comparison**, not **cross-slice synthesis**.

---

## Derived Status Analysis

### Does any feature provide a single status per product such as improving / stable / degrading?

**No true product-level cross-slice status exists.**

What does exist:

1. **Pipeline breakdown trend per pipeline**
   - `PipelineHalfSprintTrend` yields Improving / Degrading / Stable / Insufficient.
   - This is computed from first-half vs second-half failure-rate change.
   - It is **pipeline-level**, not product-level.

2. **Trends workspace pipeline tile**
   - Returns Stable / Unstable / No data.
   - This is based on **Build Quality failure rate only** over a rolling date range.
   - It is not a combined Build Quality + Pipeline Insights status.

3. **Build Quality confidence**
   - Confidence is 0–2 based on data sufficiency thresholds.
   - It is not a quality direction or stability status.

4. **Home workspace tile strings**
   - Health / Delivery / Trends / Planning signals are short prompts.
   - They are workspace-specific prompts, not one combined delivery-quality status.

### Confirmed absence

There is no inspected feature that provides:

- one status per product derived from **both** Build Quality and Pipeline Insights,
- one status vocabulary like improving / stable / degrading at product level,
- one explanation of which combined signals produced that status.

---

## Consistency Check

### Time window alignment

#### Backend inclusion semantics

The inspected Build Quality and Pipeline Insights read stores both filter cached pipeline runs by **`FinishedDateUtc >= start` and `< end`**.

That means the backend inclusion rule is aligned on finish-time semantics.

#### User-facing window alignment

The user-facing windows are **not** aligned across the relevant surfaces:

- Health Overview uses a **rolling 30-day** Build Quality window.
- Pipeline Insights uses a **selected sprint** window.
- Trends workspace pipeline tile uses a **date range / rolling range** Build Quality window.
- Pipeline Insights drawer Build Quality detail uses the **selected sprint** window.

So although the underlying timestamp rule is consistent, the surfaced comparisons are **not aligned to one shared window**.

### Product scope alignment

The Build Quality controller and Pipeline Insights controller both resolve Product Owner scope through canonical filter-resolution services:

- `DeliveryFilterResolutionService` for Build Quality rolling/sprint queries
- `PipelineFilterResolutionService` for Pipeline Insights

Both services replace out-of-scope requested products with the Product Owner’s owned products and record validation issues.

That means the backend product-scope discipline is broadly aligned.

### Repository and pipeline identity alignment

The inspected Pipeline Insights DTOs expose:

- repository IDs in the filter/resolution path,
- external pipeline definition IDs in public DTOs.

The Build Quality DTOs also expose:

- repository IDs,
- external pipeline definition IDs for product/pipeline detail surfaces.

The Pipeline Insights page passes `PipelineDefinitionId` from its scatter-point metadata into `BuildQualityService.GetPipelineAsync(...)`, so the narrow drawer integration appears identity-consistent.

### Consistency verdict

- **Backend identity/scope/timestamp semantics:** mostly aligned.
- **User-facing comparison surface:** **not aligned** because the pages use different windows and there is no canonical shared summary surface.

---

## Gaps

### Missing signals

A true cross-slice summary would need at least some shared signal set such as:

- Build Quality success/test/coverage confidence
- Pipeline failure/warning/duration trend
- one rule for how those interact

That shared signal set does not exist.

### Missing combinations

Missing today:

- product-level combination of Build Quality + Pipeline Insights
- sprint-level Build Quality summary on the main Health overview page for direct comparison with sprint-level Pipeline Insights
- rolling-window Pipeline Insights summary paired with rolling-window Build Quality
- any shared DTO or handler that merges both slices

### Missing conclusions

Missing today:

- one final quality status per product
- one final quality status per Product Owner scope
- one explanation of why the status is improving / stable / degrading
- one place that resolves contradictory evidence across slices

### Missing conflict resolution

No inspected rule answers cases such as:

- Build Quality looks strong but Pipeline Insights duration trend is degrading
- Pipeline failure rate is improving but test pass rate or coverage is weakening
- Pipeline Insights has little data while Build Quality has enough evidence, or vice versa

The user must interpret these conflicts manually.

### Duplicated or fragmented interpretation surfaces

The system currently spreads quality interpretation across multiple places:

- Health Overview interprets Build Quality,
- Pipeline Insights interprets sprint stability,
- Trends workspace exposes a pipeline badge derived from Build Quality,
- Home workspace exposes health/trends prompts from unrelated sources.

This is more **fragmentation** than backend duplication, but it still means users must assemble the overall picture themselves.

---

## Risks

### 1. False confidence from the Trends “Pipeline Insights” tile

This is the clearest misleading risk.

The tile is labeled **Pipeline Insights**, but its badge is calculated from **Build Quality rolling-window success rate** only. It ignores:

- warning rate,
- duration,
- per-pipeline degradation,
- top troubled pipelines,
- half-sprint trend.

So a user can see **Stable** on the tile and still open `/home/pipeline-insights` to find:

- degrading half-sprint trends,
- high warning rates,
- poor duration behavior,
- or a concentrated failure hotspot in a specific pipeline.

### 2. Rolling-window vs sprint-window mismatch

Health Overview can look good over the last 30 days while Pipeline Insights shows a bad current sprint.

That is not a backend bug; it is a surface-level comparison problem. But without an explicit reconciliation layer, the UI can still create false confidence.

### 3. Drawer integration can imply deeper synthesis than actually exists

The Pipeline Insights drawer shows Build Quality under the same build summary panel, which is helpful. But the page’s rankings and trends are still driven by Pipeline Insights logic only.

That means the UI can look more integrated than the underlying logic really is.

### 4. Health workspace naming can overstate summary coverage

The Health hub routes users to an “Overview” page whose copy explicitly says Build Quality overview. That is accurate. But it also means the Health workspace does **not** actually provide one combined operational quality summary across execution quality and pipeline behavior.

Users may assume “Overview” means overall delivery quality, when it is really Build Quality only.

---

## Conclusion

**The feature does not currently exist as a true cross-slice quality summary.**

The current state is best classified as **Partial**:

- Build Quality exists and is reasonably well-structured.
- Pipeline Insights exists and is reasonably well-structured.
- There is a small amount of visual adjacency between them in the Pipeline Insights drawer.
- Backend scope, identity, and finish-time inclusion semantics are largely aligned.

But the system still lacks the defining properties of a real cross-slice summary:

- no shared combined model,
- no shared combined query,
- no single derived conclusion,
- no cross-slice conflict resolution,
- and no consistently aligned user-facing time window.

So the Product Owner does **not** currently have a single place that answers:

> “Given Build Quality **and** Pipeline Insights together, is delivery quality improving, stable, or degrading for this product?”

Today, the Product Owner must answer that question manually by comparing multiple screens.
