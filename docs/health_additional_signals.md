# Additional Health Signals Proposal

This document proposes additional health signals to extend the Health workspace beyond the current validation and backlog health analysis. The signals are categorized by workspace and include implementation complexity estimates.

---

## Proposed Health Signals

### 1. Pipeline Failure Rate

**Workspace:** Health (Now)  
**Functional Description:**  
Tracks the percentage of CI/CD pipeline runs that fail in the last 7/14/30 days. High failure rates indicate instability in the build/deployment process and reduced team velocity.

**Data Source:**  
- **New:** Azure DevOps Pipelines REST API
- Pipeline run history with status (succeeded, failed, canceled)
- Aggregate by time window and calculate failure percentage

**Severity Mapping:**
- 0-5% failures: Success (green)
- 5-15% failures: Warning (yellow)
- 15%+ failures: Error (red)

**Complexity:** 4/5  
*Requires new Azure DevOps API integration, caching, and time-windowed aggregation.*

---

### 2. Pipeline Time-to-Green

**Workspace:** Health (Now)  
**Functional Description:**  
Average time from commit to successful pipeline completion. Slow pipelines reduce feedback loops and developer productivity. Tracks trends over time.

**Data Source:**  
- **New:** Azure DevOps Pipelines REST API
- Pipeline run duration for successful runs
- Calculate rolling average (7/14/30 days)

**Severity Mapping:**
- < 10 minutes: Success (green)
- 10-30 minutes: Info (blue)
- 30-60 minutes: Warning (yellow)
- 60+ minutes: Error (red)

**Complexity:** 4/5  
*Similar to pipeline failure rate but requires duration calculation and trend analysis.*

---

### 3. Flaky Build Detection

**Workspace:** Trends (Past)  
**Functional Description:**  
Identifies builds that intermittently fail without code changes (same commit succeeds after retry). Flaky builds erode trust in CI/CD and waste team time.

**Data Source:**  
- **New:** Azure DevOps Pipelines REST API
- Compare pipeline runs with same source commit SHA
- Flag commit as "flaky" if multiple runs have different outcomes

**Severity Mapping:**
- 0 flaky commits: Success (green)
- 1-3 flaky commits: Warning (yellow)
- 4+ flaky commits: Error (red)

**Complexity:** 5/5  
*Complex analysis requiring commit SHA tracking, retry detection, and historical comparison.*

---

### 4. Stale Pull Requests

**Workspace:** Health (Now)  
**Functional Description:**  
Count of open pull requests that haven't been updated in N days (e.g., 7, 14, 30). Stale PRs indicate blocked work, lost context, or abandoned efforts.

**Data Source:**  
- **New:** Azure DevOps Git Repos REST API
- Pull request list with status and last update timestamp
- Filter by age threshold

**Severity Mapping:**
- 0 stale PRs: Success (green)
- 1-3 stale PRs: Info (blue)
- 4-10 stale PRs: Warning (yellow)
- 10+ stale PRs: Error (red)

**Complexity:** 3/5  
*Requires new Azure DevOps Git API integration and date-based filtering.*

---

### 5. Pull Request Review Latency

**Workspace:** Health (Now)  
**Functional Description:**  
Average time from PR creation to first review comment or approval. High latency indicates bottlenecks in code review process and slows delivery.

**Data Source:**  
- **New:** Azure DevOps Git Repos REST API
- PR creation timestamp
- First review/comment timestamp
- Calculate median and P95 latency

**Severity Mapping:**
- < 4 hours: Success (green)
- 4-24 hours: Info (blue)
- 24-48 hours: Warning (yellow)
- 48+ hours: Error (red)

**Complexity:** 4/5  
*Requires PR timeline analysis, threading model understanding, and statistical calculation.*

---

### 6. Pull Request Throughput

**Workspace:** Trends (Past)  
**Functional Description:**  
Number of PRs merged per week/sprint. Tracks team velocity and identifies slowdowns in delivery pipeline. Trends over time show capacity changes.

**Data Source:**  
- **New:** Azure DevOps Git Repos REST API
- PR completion date and status (completed, abandoned)
- Group by time window and count completions

**Severity Mapping:**
- Not applicable (informational metric, show as trend line)

**Complexity:** 3/5  
*Straightforward counting and aggregation, requires time-series visualization.*

---

### 7. Bug Aging

**Workspace:** Health (Now)  
**Functional Description:**  
Distribution of open bugs by age buckets (< 7 days, 7-30 days, 30-90 days, 90+ days). Old bugs indicate neglect or insufficient capacity for bug fixing.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- Filter by Type == "Bug" and State != "Done"
- Calculate age from Created Date
- Group into age buckets

**Severity Mapping:**
- 0 bugs 90+ days: Success (green)
- 1-5 bugs 90+ days: Warning (yellow)
- 6+ bugs 90+ days: Error (red)

**Complexity:** 2/5  
*Uses existing data source, simple date calculation and bucketing.*

---

### 8. Bug Inflow vs Outflow

**Workspace:** Trends (Past)  
**Functional Description:**  
Trend comparing new bugs created vs bugs closed per week/sprint. Negative trend (inflow > outflow) indicates growing bug debt. Positive trend shows healthy bug management.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- Filter by Type == "Bug"
- Use Created Date for inflow, Closed Date for outflow
- Calculate net change per time window

**Severity Mapping:**
- Outflow > Inflow: Success (green)
- Outflow == Inflow: Info (blue)
- Inflow > Outflow: Warning (yellow)
- Inflow >> Outflow (2x): Error (red)

**Complexity:** 3/5  
*Requires time-series analysis with both creation and closure dates.*

---

### 9. Stale Work Items (Blocked State)

**Workspace:** Health (Now)  
**Functional Description:**  
Count of work items in "Blocked" or "On Hold" state for more than N days (e.g., 7, 14, 30). Long-blocked items indicate unresolved dependencies or process bottlenecks.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- Filter by State == "Blocked" or State == "On Hold"
- Calculate time in current state using State Change Date
- Filter by age threshold

**Severity Mapping:**
- 0 blocked items: Success (green)
- 1-3 blocked items: Info (blue)
- 4-10 blocked items: Warning (yellow)
- 10+ blocked items: Error (red)

**Complexity:** 2/5  
*Uses existing data, requires state history analysis (may need JsonPayload parsing).*

---

### 10. Work Items Without Activity

**Workspace:** Health (Now)  
**Functional Description:**  
Count of open work items (PBIs, Features, Epics) with no updates (comments, field changes, state transitions) in the last N days (e.g., 30, 60, 90). Indicates abandoned or forgotten work.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- Use Changed Date field
- Filter by State != "Done" and State != "Removed"
- Calculate days since last change

**Severity Mapping:**
- 0 stale items (90+ days): Success (green)
- 1-5 stale items: Info (blue)
- 6-15 stale items: Warning (yellow)
- 16+ stale items: Error (red)

**Complexity:** 2/5  
*Uses existing data, simple date-based filtering.*

---

### 11. Validation Issue Aging

**Workspace:** Trends (Past)  
**Functional Description:**  
Tracks how long validation issues remain unresolved. Shows distribution of current validation issues by age (< 7 days, 7-30 days, 30+ days). Persistent issues indicate team not prioritizing quality.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- ValidationIssues already include severity and message
- Need to track when validation issue first appeared (requires persistence layer)
- Calculate age from first detection date

**Severity Mapping:**
- 0 issues 30+ days: Success (green)
- 1-5 issues 30+ days: Warning (yellow)
- 6+ issues 30+ days: Error (red)

**Complexity:** 4/5  
*Requires new persistence layer to track validation issue history over time.*

---

### 12. Work Item Cycle Time by Type

**Workspace:** Trends (Past)  
**Functional Description:**  
Average time from "New" to "Done" for each work item type (Epic, Feature, PBI, Task, Bug). Tracks trends over sprints to identify process improvements or degradations.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- Use Created Date and Closed Date
- Calculate duration for completed items
- Group by Type and time window
- Calculate mean, median, P75, P95

**Severity Mapping:**
- Not applicable (informational metric, show as trend lines per type)

**Complexity:** 3/5  
*Uses existing data, requires time-series aggregation and statistical calculation.*

---

### 13. Sprint Completion Rate

**Workspace:** Planning (Future)  
**Functional Description:**  
Percentage of committed work (story points or item count) completed by sprint end. Tracks team capacity planning accuracy. Historical trends inform future sprint planning.

**Data Source:**  
- **Existing:** SprintRepository + WorkItemService
- Compare work items in sprint at start vs completed at end
- Calculate completion percentage by story points or count
- Aggregate by sprint

**Severity Mapping:**
- 90-100% completion: Success (green)
- 70-89% completion: Info (blue)
- 50-69% completion: Warning (yellow)
- < 50% completion: Error (red)

**Complexity:** 3/5  
*Requires sprint boundary detection and capacity tracking over time.*

---

### 14. Technical Debt Estimation

**Workspace:** Health (Now)  
**Functional Description:**  
Estimates accumulated technical debt based on validation issues, bug count, stale PRs, and code complexity metrics. Provides aggregate "debt score" and trend.

**Data Source:**  
- **Existing + New:** Combination of multiple signals
- Validation issue counts (weighted by severity)
- Bug aging (weighted by age)
- Stale PRs (weighted by age)
- Optional: Code complexity metrics from static analysis tools

**Severity Mapping:**
- Debt score 0-10: Success (green)
- Debt score 11-30: Info (blue)
- Debt score 31-60: Warning (yellow)
- Debt score 61+: Error (red)

**Complexity:** 5/5  
*Requires aggregation algorithm, weighting model, and integration of multiple data sources.*

---

### 15. Cross-Team Dependency Health

**Workspace:** Planning (Future)  
**Functional Description:**  
Tracks work items with external dependencies (other teams, vendors). Shows count of blocked items waiting on external parties and average wait time.

**Data Source:**  
- **Existing:** WorkItemService.GetAllWithValidationAsync()
- Requires tagging convention or custom field for external dependencies
- Track items in "Blocked" state with external dependency tag
- Calculate wait time

**Severity Mapping:**
- 0 externally blocked items: Success (green)
- 1-3 externally blocked items: Info (blue)
- 4-8 externally blocked items: Warning (yellow)
- 9+ externally blocked items: Error (red)

**Complexity:** 3/5  
*Uses existing data but requires consistent tagging/field usage.*

---

## Implementation Priority Recommendations

### High Priority (Implement First)
1. **Bug Aging** (Complexity: 2/5) - Immediate value, uses existing data
2. **Work Items Without Activity** (Complexity: 2/5) - Quick win, uses existing data
3. **Stale Work Items (Blocked State)** (Complexity: 2/5) - Identifies process bottlenecks
4. **Pull Request Throughput** (Complexity: 3/5) - Key velocity metric

### Medium Priority
5. **Stale Pull Requests** (Complexity: 3/5) - Identifies waste
6. **Bug Inflow vs Outflow** (Complexity: 3/5) - Quality trend indicator
7. **Work Item Cycle Time by Type** (Complexity: 3/5) - Process improvement metric
8. **Sprint Completion Rate** (Complexity: 3/5) - Planning accuracy

### Low Priority (Later)
9. **Pull Request Review Latency** (Complexity: 4/5) - Requires PR timeline analysis
10. **Pipeline Failure Rate** (Complexity: 4/5) - New integration required
11. **Pipeline Time-to-Green** (Complexity: 4/5) - New integration required
12. **Validation Issue Aging** (Complexity: 4/5) - Requires new persistence layer

### Future/Research
13. **Flaky Build Detection** (Complexity: 5/5) - Complex analysis
14. **Technical Debt Estimation** (Complexity: 5/5) - Requires aggregation model
15. **Cross-Team Dependency Health** (Complexity: 3/5) - Requires tagging conventions

---

## Implementation Considerations

### Azure DevOps API Integration
Signals requiring pipeline and PR data (1-6, 15) will need:
- New service layer for Azure DevOps REST API
- OAuth authentication flow
- Rate limiting and caching strategy
- Error handling for API outages

### Historical Data Persistence
Some signals (11, 12, 13, 14) benefit from tracking trends over time:
- Consider adding a time-series database or extending EF Core schema
- Store daily/weekly snapshots of key metrics
- Retention policy for historical data

### UI Considerations
- All signals should follow the same severity color mapping (error/warning/neutral)
- Zero counts should always be neutral, not success
- Trend signals should use line/area charts
- Distribution signals should use stacked bar charts or histograms

### Testing Strategy
- Mock Azure DevOps API responses
- Use snapshot data for historical trend testing
- Ensure all severity thresholds are configurable
- Test with edge cases (no data, stale data, API failures)

---

## Workspace Distribution Summary

- **Health (Now):** 9 signals (1, 2, 4, 5, 7, 9, 10, 14)
- **Trends (Past):** 6 signals (3, 6, 8, 11, 12)
- **Planning (Future):** 2 signals (13, 15)

This distribution maintains the Health workspace as the primary dashboard for current state, with Trends providing historical context and Planning supporting future sprint planning.
