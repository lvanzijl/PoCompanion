## Feature: Enhanced Effort Distribution Analytics

### Goal

Provide advanced analytics not available in TFS WebUI for effort distribution, including imbalance detection, trend analysis, concentration risk assessment, forecasting, and actionable recommendations. Enable Product Owners and Scrum Masters to proactively identify and mitigate capacity planning risks before they impact delivery.

This feature must explicitly reference and comply with:

* `docs/UX_PRINCIPLES.md`
* `docs/rules/ui-rules.md`
* `docs/rules/architecture-rules.md`
* `docs/rules/process-rules.md`

---

## Functional Description

### Overview

The Enhanced Effort Distribution Analytics feature extends the basic effort distribution heat map with three advanced analytics capabilities:

1. **Imbalance Detection** - Identifies teams or sprints with disproportionate workload allocation
2. **Trend Analysis** - Shows how effort distribution patterns change over time with forecasting
3. **Concentration Risk** - Flags scenarios where too much effort is in a single area or sprint

Each capability provides:
- Risk level assessment (None/Low/Medium/High/Critical)
- Quantitative metrics and scoring
- Actionable recommendations for rebalancing
- Visual indicators and charts

---

## Value Proposition

### Beyond TFS WebUI

TFS WebUI provides basic capacity planning but lacks:
- **Imbalance detection** - No automated identification of workload imbalances across teams
- **Trend forecasting** - No predictive analysis of future effort distribution
- **Concentration risk analysis** - No warnings about single points of failure
- **Actionable recommendations** - No guidance on how to rebalance workloads
- **Historical trend analysis** - Limited visibility into distribution patterns over time

This feature adds:
- Automated detection of effort imbalances using statistical analysis (deviation from mean)
- Time-series trend analysis with linear regression for forecasting
- Concentration risk scoring using Herfindahl-Hirschman Index (HHI)
- Prioritized recommendations for rebalancing with specific effort amounts
- Confidence levels based on historical variance
- Interactive tabbed interface for exploring different analytics

---

## Configuration Requirements

### Prerequisites

1. **Cached Work Items** - Work items with effort hours must be cached locally
2. **Effort Estimates** - Work items must have effort field populated
3. **Area Paths** - Work items must be assigned to area paths (teams)
4. **Iteration Paths** - Work items must be assigned to iterations (sprints)
5. **Historical Data** - At least 2-3 sprints of historical data for meaningful analysis

### User Configuration

- **Area Path Filter** - Optional filter to scope analysis to specific team hierarchy
- **Max Iterations** - Number of sprints to analyze (default: 10, range: 1-20)
- **Default Capacity** - Capacity per iteration for utilization context in sprint descriptions (optional)
- **Imbalance Threshold** - Base threshold for imbalance detection (default: 0.3 = 30%; higher bands apply at 1.5x and 2.5x this threshold)
- **Concentration Threshold** - Legacy compatibility query parameter; the stable subset uses fixed 25/40/60/80 concentration bands

No global configuration required; all settings are per-analysis.

---

## Main View Structure and Components

### Access Point

- Navigation menu: **Metrics → Effort Distribution**
- Direct URL: `/effort-distribution`

### Main Page Structure

1. **Header Section**
   - Page title and description
   - Context-aware help component with enhanced best practices

2. **Filter Controls** (existing)
   - Area path filter
   - Max iterations slider
   - Default capacity input
   - Refresh button

3. **Summary Cards** (existing)
   - Total Effort
   - Area Paths count
   - Iterations count
   - Average Utilization

4. **Heat Map Section** (existing)
   - Area Path × Iteration heat map
   - Color-coded cells by capacity utilization

5. **Charts Section** (existing)
   - Effort by Iteration bar chart
   - Effort by Area Path pie chart
   - Utilization table

6. **Enhanced Analytics Section** (NEW)
   - Tabbed interface with three analytics tabs
   - On-demand loading (click button to analyze)

### Enhanced Analytics Tabs

#### Tab 1: Imbalance Detection

**Summary Cards:**
- Overall Risk Level chip (color-coded)
- Imbalance Score percentage

**Team Imbalances Table:**
| Column | Description |
|--------|-------------|
| Area Path | Team/area identifier |
| Total Effort | Effort assigned to team |
| Average | Average effort across all teams |
| Deviation | Percentage deviation from average |
| Risk Level | Low/Medium/High/Critical chip |
| Description | Human-readable explanation |

**Sprint Imbalances Table:**
| Column | Description |
|--------|-------------|
| Sprint | Sprint name |
| Total Effort | Effort in sprint |
| Average | Average effort across sprints |
| Deviation | Percentage deviation |
| Risk Level | Color-coded chip |
| Description | With capacity utilization info |

**Recommendations List:**
- Icon + color-coded by priority
- Title (e.g., "Reduce load for Team A")
- Description with specific effort amounts

#### Tab 2: Trend Analysis

**Summary Cards:**
- Overall Trend direction chip (Stable/Increasing/Decreasing/Volatile)
- Trend Slope value

**Sprint Trends Chart:**
- Line chart showing effort over time
- X-axis: Sprint names
- Y-axis: Total effort
- Clear trend line visualization

**Forecasts Table:**
| Column | Description |
|--------|-------------|
| Sprint | Future sprint name (Sprint +1, +2, +3) |
| Forecasted Effort | Predicted effort amount |
| Low Estimate | Confidence interval lower bound |
| High Estimate | Confidence interval upper bound |
| Confidence | Percentage confidence level |

**Area Path Trends:**
- Shows trend direction per team
- Identifies increasing/decreasing workload patterns

#### Tab 3: Concentration Risk

**Summary Cards:**
- Risk Level chip (None/Low/Medium/High/Critical)
- Concentration Index (HHI-based score)

**Area Path Concentration Table:**
| Column | Description |
|--------|-------------|
| Area | Short area path name |
| Effort | Total effort in area |
| % of Total | Percentage of total effort |
| Risk Level | Color-coded chip |
| Description | Risk explanation |

**Iteration Concentration Table:**
- Same structure as Area Path table
- Shows sprint-level concentration

**Mitigation Recommendations List:**
- Icon + color by priority
- Strategy type (Diversify, Spread, Break Down, etc.)
- Title and description
- Specific effort amounts to redistribute

---

## User Interactions and Behavior

### Initial Load
- Page loads with basic distribution data
- Enhanced analytics tabs show "Analyze" buttons
- User clicks button to trigger analysis on-demand

### Analyzing Imbalances
1. Click "Analyze Imbalances" button
2. Loading indicator during API call
3. Results display with risk level, tables, and recommendations
4. Color-coded chips indicate severity
5. User reviews imbalanced teams/sprints
6. User reads recommendations for rebalancing

### Analyzing Trends
1. Click "Analyze Trends" button
2. Loading indicator during calculation
3. Chart displays with trend line
4. User sees if effort is increasing, decreasing, or stable
5. Forecasts table shows next 3 sprints with confidence
6. User plans future capacity based on trends

### Analyzing Concentration Risk
1. Click "Analyze Concentration Risk" button
2. Loading during HHI calculation
3. Risk level and concentration index displayed
4. Tables show which areas/sprints have concentration
5. User identifies single points of failure
6. User reviews mitigation recommendations

### Error Handling
- API errors display with clear messages
- Empty data shows appropriate guidance
- Insufficient historical data warns user
- Invalid parameters rejected with feedback

---

## Filters and Search Capabilities

**Area Path Filter** (applies to all analytics):
- Scopes analysis to specific team hierarchy
- Inherited from main distribution controls
- Updates all analytics tabs

**Max Iterations** (applies to all analytics):
- Controls how many sprints to analyze
- Range: 1-20, default 10
- More sprints = better trend analysis but more computation

---

## Data Requirements and Caching Strategy

### Data Requirements

#### Work Item Fields
- **Area Path** - Team/component assignment (required)
- **Iteration Path** - Sprint assignment (required)
- **Effort** - Numeric effort-hour value (required)
- **State** - Optional for filtering completed vs planned work
- **Title** - For display in recommendations

#### Calculation Requirements
- **Minimum 2 sprints** for trend analysis
- **3-5 sprints recommended** for reliable forecasting
- **Multiple area paths** for meaningful imbalance detection
- **Effort estimates** on majority of work items

### Caching Strategy

Per `docs/rules/architecture-rules.md`:

1. **Local Cache** - All work items cached in SQLite
   - Retrieved via "Pull & Cache" command
   - Includes all required fields
2. **Read-Only Analysis** - Analytics computed on-demand from cache
3. **No Analytics Caching** - Results computed fresh each time
4. **Staleness Handling** - User must refresh cache before analyzing

### Data Flow

1. User opens Effort Distribution page
2. Basic distribution loaded from cache automatically
3. User clicks "Analyze" button on a tab
4. Frontend calls specific API endpoint with filters
5. Handler queries local cache for work items
6. Handler performs statistical analysis:
   - **Imbalance**: Calculate mean effort and deviation percentage
   - **Trends**: Linear regression, slope calculation, forecasting
   - **Concentration**: HHI calculation, risk thresholds
7. DTO with results, metrics, and recommendations returned
8. UI displays results in tabbed interface

### Algorithms

#### Imbalance Detection
```
1. Group work items by area path and iteration
2. Calculate total effort per group
3. Calculate average effort across groups
4. For each group:
   - Deviation = |Actual - Average| / Average
   - Risk = Low (< threshold), Medium (threshold to <1.5x threshold), High (1.5x to <2.5x threshold), Critical (≥2.5x threshold)
5. Overall score = weighted average of max and mean deviation
6. Generate recommendations:
   - Overloaded: Suggest moving effort to underloaded
   - Underloaded: Suggest adding effort from overloaded
```

#### Trend Analysis
```
1. Order sprints chronologically
2. Calculate effort per sprint
3. Linear regression:
   - Slope = trend direction and magnitude
   - R² = confidence in trend
4. Determine trend direction:
   - Stable: |slope/mean| < 10%
   - Increasing: slope/mean > 10%
   - Decreasing: slope/mean < -10%
   - Volatile: coefficient of variation > 50%
5. Forecast next 3 sprints:
   - Forecasted = Average + (Slope × Sprint Index)
   - Confidence interval = ±2 × Standard Deviation
   - Confidence level = inverse of coefficient of variation
```

#### Concentration Risk
```
1. Calculate percentage of total effort per area/sprint
2. Determine risk level:
   - None: < 25%
   - Low: 25-40%
   - Medium: 40-60%
   - High: 60-80%
   - Critical: > 80%
3. Calculate Herfindahl-Hirschman Index (HHI) from the full area and iteration distributions:
   - HHI = Σ(Percentage²)
   - Normalize to 0-100 scale
4. Overall risk = max concentration percentage
5. Generate mitigation recommendations:
   - Diversify: Move 20% from concentrated areas
   - Spread: Defer 15% to adjacent sprints
   - Break down: Split large work items
```

---

## Architecture Compliance

### Layer Separation

- **Core Layer**: Queries and DTOs
  - `GetEffortImbalanceQuery.cs`
  - `EffortImbalanceDto.cs`
  - `GetEffortDistributionTrendQuery.cs`
  - `EffortDistributionTrendDto.cs`
  - `GetEffortConcentrationRiskQuery.cs`
  - `EffortConcentrationRiskDto.cs`
  - Enums: `ImbalanceRiskLevel`, `EffortTrendDirection`, `ConcentrationRiskLevel`
  - Enums: `RecommendationType`, `MitigationStrategy`

- **API Layer**: Handlers and controller
  - `GetEffortImbalanceQueryHandler.cs`
  - `GetEffortDistributionTrendQueryHandler.cs`
  - `GetEffortConcentrationRiskQueryHandler.cs`
  - `MetricsController.cs` (new endpoints)

- **Client Layer**: Blazor UI
  - `EffortDistribution.razor` (enhanced)

### API Endpoints

**GET /api/metrics/effort-imbalance**

Query Parameters:
- `areaPathFilter` (optional) - Filter to specific area
- `maxIterations` (default: 10) - Number of sprints
- `defaultCapacity` (optional) - Capacity per iteration
- `imbalanceThreshold` (default: 0.3) - Detection threshold

Response: `EffortImbalanceDto`

Status Codes:
- 200 OK - Analysis completed
- 400 Bad Request - Invalid parameters
- 500 Internal Server Error - Calculation error

**GET /api/metrics/effort-distribution-trend**

Query Parameters:
- `areaPathFilter` (optional)
- `maxIterations` (default: 10)
- `defaultCapacity` (optional)

Response: `EffortDistributionTrendDto`

Status Codes: Same as above

**GET /api/metrics/effort-concentration-risk**

Query Parameters:
- `areaPathFilter` (optional)
- `maxIterations` (default: 10)
- `concentrationThreshold` (legacy compatibility parameter; ignored by the stable subset)

Response: `EffortConcentrationRiskDto`

Status Codes: Same as above

---

## UI Rules Compliance

Per `docs/rules/ui-rules.md`:

- **Platform**: Blazor WebAssembly ✓
- **Components**: MudBlazor only (MudTabs, MudTable, MudChart, MudCard, MudChip, MudList, MudButton) ✓
- **No JavaScript**: All logic in C# ✓
- **Dark Theme**: Follows app-wide dark theme ✓
- **CSS Isolation**: Component-specific styles in `.razor.css` ✓
- **API Client**: Uses NSwag-generated `IMetricsClient` ✓
- **Error Handling**: Centralized via `ErrorMessageService` and `ErrorDisplay` component ✓
- **Validation**: Parameter validation on API endpoints ✓

---

## UX Principles Compliance

Per `docs/UX_PRINCIPLES.md`:

- **Clarity**: Clear labels, risk indicators, and descriptions
- **Consistency**: Follows same pattern as other metric pages
- **Minimal**: On-demand loading - only analyze when needed
- **Feedback**: Loading indicators, error displays, success states
- **Context-Aware Help**: Enhanced help with new analytics best practices
- **Visual Hierarchy**: Tabs organize complex information
- **Progressive Disclosure**: Basic distribution shown first, advanced analytics on-demand
- **Color-Coding**: Risk levels use consistent color scheme (Green/Blue/Yellow/Red)

---

## Testing Requirements

### Unit Tests

Located in: `PoTool.Tests.Unit/Handlers/`

**GetEffortImbalanceQueryHandlerTests.cs** (8 tests):
- Empty data handling
- Balanced distribution (low risk)
- Imbalanced teams (high risk)
- Imbalanced sprints (high risk)
- Recommendation generation
- Area path filtering
- Capacity utilization calculation

**GetEffortDistributionTrendQueryHandlerTests.cs** (9 tests):
- Empty data handling
- Increasing trend detection
- Decreasing trend detection
- Stable trend detection
- Volatile trend detection
- Forecast generation
- Insufficient history handling
- Change from previous calculation
- Area path trend analysis

**GetEffortConcentrationRiskQueryHandlerTests.cs** (9 tests):
- Empty data handling
- Well-distributed effort (low risk)
- High area path concentration (critical risk)
- High iteration concentration (high risk)
- Medium concentration detection
- Mitigation recommendation generation
- Top work items inclusion
- Concentration index calculation
- Area path filtering
- Critical risk urgent recommendations

### Integration Tests

To be added in: `PoTool.Tests.Integration/Features/EnhancedEffortDistribution.feature`

Scenarios (recommended):
- Basic imbalance detection with various thresholds
- Trend analysis with sufficient historical data
- Trend analysis with insufficient data
- Concentration risk at various levels
- Combined filter application (area path + max iterations)
- Invalid parameter handling (negative values, out of range)
- Large dataset performance (1000+ work items)

---

## User Guidance

### Context-Aware Help

Provided via PageHelp component (enhanced):

**Data Requirements:**
- Iteration Path - Required for sprint analysis
- Area Path - Required for team analysis
- Effort (Story Points) - Required for all calculations
- Historical Data - 3-5 sprints recommended for trends

**Best Practices:**
- Plan to 80-85% of team capacity
- Use imbalance detection to identify overloaded teams early
- Monitor trend analysis to catch workload increases before problems
- Concentration risk flags single points of failure - diversify effort
- Run all three analytics for comprehensive capacity planning
- Re-analyze weekly as work evolves
- Address Critical/High risks immediately, Medium risks within sprint, Low risks opportunistically

**Common Issues:**
- **High imbalance score (>50%)**: Major workload disparities. Redistribute immediately before sprint starts.
- **Volatile trend**: Inconsistent sprint planning. Implement rolling wave planning for stability.
- **Critical concentration (>80% in one place)**: Single point of failure. Break down work items and distribute.
- **No forecasts shown**: Need at least 2 sprints of historical data for trend calculation.
- **All risks show as None/Low**: May indicate over-capacity or insufficient work planned. Review capacity settings.

---

## Future Enhancements

If approved in the future:

1. **Team Comparison View** - Side-by-side comparison of multiple teams
2. **What-If Scenarios** - Adjust capacity or effort to see impact
3. **Historical Accuracy Tracking** - Compare past forecasts to actuals
4. **Automated Alerts** - Email/notification when risks exceed thresholds
5. **Recommendation Auto-Apply** - One-click work item redistribution
6. **Monte Carlo Simulation** - Probabilistic forecasting with confidence intervals
7. **Machine Learning Trends** - More sophisticated pattern recognition

All enhancements must comply with `docs/rules/ui-rules.md` and `docs/rules/architecture-rules.md`.

---

## References

- UX Principles: `docs/UX_PRINCIPLES.md`
- UI Rules: `docs/rules/ui-rules.md`
- Architecture Rules: `docs/rules/architecture-rules.md`
- Process Rules: `docs/rules/process-rules.md`
- Implementation: `PoTool.Client/Pages/Metrics/EffortDistribution.razor`
- Queries: `PoTool.Core/Metrics/Queries/GetEffort*.cs`
- Handlers: `PoTool.Api/Handlers/Metrics/GetEffort*.cs`
- Tests: `PoTool.Tests.Unit/Handlers/GetEffort*.cs`
