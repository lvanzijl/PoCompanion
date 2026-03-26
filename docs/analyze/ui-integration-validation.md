# UI Integration Validation

## Updated pages and components

- `PoTool.Client/Pages/Home/SprintTrend.razor`
  - added a product-level canonical analytics summary in the existing sprint delivery drilldown
  - switched epic presentation to render `AggregatedProgress`, forecast values, excluded/included feature counts, and `TotalWeight` directly from `EpicProgressDto`
  - switched feature presentation to render `CalculatedProgress`, `Override`, `EffectiveProgress`, `Effort`, forecast values, `Weight`, and `IsExcluded` directly from `FeatureProgressDto`
  - added a feature detail summary block so the selected feature shows canonical values without UI recomputation
- `PoTool.Client/Pages/Home/Components/AnalyticsMetricCard.razor`
- `PoTool.Client/Pages/Home/Components/CanonicalNullablePercentage.razor`
- `PoTool.Client/Pages/Home/Components/CanonicalForecastValue.razor`
- `PoTool.Client/Pages/Home/Components/CanonicalDeltaValue.razor`
- `PoTool.Client/Pages/Home/Components/CanonicalPlanningQuality.razor`
- `PoTool.Client/Pages/Home/Components/CanonicalInsightList.razor`

## Reusable display components

- `AnalyticsMetricCard`
  - shared read-only card shell for canonical metric values
- `CanonicalNullablePercentage`
  - preserves nullable progress values as unknown instead of rendering `0%`
  - optionally renders a progress bar only when the canonical value exists
- `CanonicalForecastValue`
  - renders nullable forecast and effort values without `?? 0` fallback logic
- `CanonicalDeltaValue`
  - renders positive, negative, and null deltas exactly as delivered by the DTOs
- `CanonicalPlanningQuality`
  - renders the canonical planning quality score and `PlanningQualitySignalDto` rows
- `CanonicalInsightList`
  - renders canonical insight code, severity, message, and `InsightContextDto` values

## Canonical DTO consumption points

- `GetSprintTrendMetricsResponse.ProductAnalytics`
  - consumed in `SprintTrend.razor` via `_productAnalytics`
  - product-level summary renders `ProductDeliveryAnalyticsDto.Progress`, `SnapshotComparisonDto`, `PlanningQualityDto`, and `InsightDto`
- `EpicProgressDto`
  - epic rows render `AggregatedProgress`, `ForecastConsumedEffort`, `ForecastRemainingEffort`, `ExcludedFeaturesCount`, `IncludedFeaturesCount`, and `TotalWeight`
- `FeatureProgressDto`
  - feature rows and feature detail render `CalculatedProgress`, `Override`, `EffectiveProgress`, `Effort`, `ForecastConsumedEffort`, `ForecastRemainingEffort`, `Weight`, `IsExcluded`, and `ValidationSignals`

## Verification results

### Functional checks

1. Product view shows:
   - `ProductProgress`
   - `ProductForecastConsumed`
   - `ProductForecastRemaining`
   - `ExcludedEpicsCount`
   - `PlanningQualityScore`
   - `Insights`
2. Epic rows show:
   - nullable `AggregatedProgress` through `CanonicalNullablePercentage`
   - `ForecastConsumedEffort`
   - `ForecastRemainingEffort`
   - `ExcludedFeaturesCount`
   - `IncludedFeaturesCount`
   - `TotalWeight`
3. Feature drilldown shows:
   - `CalculatedProgress`
   - `Override`
   - `EffectiveProgress`
   - `Effort`
   - `ForecastConsumedEffort`
   - `ForecastRemainingEffort`
   - `Weight`
   - `IsExcluded`
4. Delta values show:
   - positive values with a `+` prefix
   - negative values unchanged
   - null values as `Unknown`
   - no client-side recomputation from snapshots
5. Planning Quality shows:
   - score
   - signal code
   - severity
   - message
   - entity reference (`Scope #EntityId`)
6. Insights show:
   - code
   - severity
   - message
   - context values from `InsightContextDto`

### Structural checks

- `SprintTrend.razor` now consumes canonical DTOs/read models directly for progress, forecast, delta, Planning Quality, and insights
- the new display components avoid `?? 0`, `GetValueOrDefault()`, and any null-to-zero coercion for canonical analytics fields
- the page does not recompute canonical progress, forecast, delta, Planning Quality, or insights
- rendering logic is centralized in reusable components instead of duplicating nullable/delta formatting across the page

### Validation executed

- `dotnet build PoTool.sln --configuration Release`
- targeted MSTest audits for:
  - `SprintTrendCanonicalAnalyticsUiTests`
  - `UiIntegrationValidationDocumentTests`
  - `UiSemanticLabelsTests`
  - `ReleaseNotesServiceTests`

## Remaining compatibility constraints

- The existing sprint summary block at the top of `SprintTrend.razor` still shows legacy sprint-delivery metrics that predate the canonical analytics DTOs; this change only integrates canonical product/epic/feature analytics into the existing drilldown surfaces.
- The current canonical read models do not expose an explicit mixed-mode flag or label, so the UI cannot surface “Mixed mode — different estimation models combined” without a dedicated upstream field or context value.
- Snapshot comparison is rendered from `SnapshotComparisonDto` only; the UI does not attempt to reconstruct deltas from prior snapshot data.
