# PO Companion User Manual

**Version 1.0**  
**Target Audience:** Product Owners managing multiple products and teams

---

## Table of Contents

1. [Introduction](#introduction)
2. [Getting Started](#getting-started)
3. [Setup & Configuration](#setup--configuration)
4. [Product & Team Management](#product--team-management)
5. [Release Planning](#release-planning)
6. [Metrics & Analytics](#metrics--analytics)
7. [Pipeline & Pull Request Insights](#pipeline--pull-request-insights)
8. [Advanced Features](#advanced-features)

---

## Introduction

### What is PO Companion?

PO Companion is a Product Owner companion tool designed to help you manage hierarchical work items from Azure DevOps/TFS. It provides powerful visualizations, metrics, and planning tools to help you lead multiple products and teams effectively.

### Work Item Hierarchy

PO Companion works with the following work item hierarchy:

```
Goal → Objective → Epic → Feature → PBI (Product Backlog Item) → Task
```

### Who Should Use This Tool?

This manual is written for **Product Owners** who:
- Manage **multiple products** (e.g., Product A and Product B)
- Lead **multiple teams** (e.g., Team Alpha and Team Beta)
- Need to plan releases across objectives
- Want data-driven insights into velocity, forecasting, and backlog health
- Coordinate dependencies between epics and features

---

## Scenario: Meet Alex, Your Guide

Throughout this manual, we'll follow **Alex**, a Product Owner at TechCorp who manages:

- **Product A**: Customer Portal (2 teams: Alpha and Beta)
- **Product B**: Admin Dashboard (2 teams: Gamma and Delta)

Alex uses PO Companion daily to:
1. Plan quarterly releases
2. Monitor team velocity
3. Forecast epic completion dates
4. Identify backlog health issues
5. Track code quality through PR and pipeline metrics

---

## Getting Started

### First Launch: Onboarding Wizard

**Page:** `/onboarding`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Onboarding.razor` (calls `Components/Onboarding/OnboardingWizard`)
- **Service:** `OnboardingService` manages onboarding state

#### What It Does

The Onboarding Wizard appears automatically on first launch and guides you through essential setup steps.

#### Use Case: Alex's First Day

Alex opens PO Companion for the first time. The onboarding wizard appears as a modal dialog and walks through:

1. **Welcome Screen** - Brief introduction to the tool
2. **TFS Connection Setup** - Configure Azure DevOps connection
3. **Profile Creation** - Create Product Owner profile
4. **Product Setup** - Add first products
5. **Team Configuration** - Link teams to products

#### How to Use

The wizard is non-blocking:
- Click **Next** to proceed through steps
- Click **Skip** to dismiss and configure later
- All settings can be changed later in the Settings pages

**Key Code Insight:**
```csharp
// Onboarding.razor shows the wizard as a dialog
var options = new DialogOptions 
{ 
    CloseButton = false,        // Cannot accidentally close
    CloseOnEscapeKey = false,   // Must complete or skip
    MaxWidth = MaxWidth.Large,
    FullWidth = true
};

var dialogReference = await DialogService.ShowAsync<OnboardingWizard>("Getting Started", options);
```

---

## Setup & Configuration

### TFS/Azure DevOps Configuration

**Page:** `/tfsconfig`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/TfsConfig.razor`
- **Service:** `TfsConfigService` manages connection settings
- **API:** `SettingsController` persists configuration

#### What It Does

Configures your connection to Azure DevOps or on-premises TFS. This is **required** for PO Companion to retrieve work item data.

#### Use Case: Connecting to TechCorp's Azure DevOps

Alex needs to connect PO Companion to TechCorp's Azure DevOps organization to pull work item data for both Product A and Product B.

**Alex's scenario:**
- Organization URL: `https://dev.azure.com/techcorp`
- Project: `TechCorpProjects`
- Authentication: Windows (NTLM) using Alex's domain credentials

#### How to Use

1. Navigate to **TFS Configuration** page (`/tfsconfig`)
2. Fill in the required fields:
   - **Organization URL**: Your Azure DevOps URL (e.g., `https://dev.azure.com/yourorg`)
   - **Project Name**: Your project name
   - **Timeout**: HTTP timeout in seconds (default: 30)
   - **API Version**: Auto-populated (read-only)
3. Click **Save Configuration**
4. Click **Test Connection** to verify the connection works
5. If successful, you'll see a green success message

**Important Notes:**
- Authentication uses **Windows credentials (NTLM)** of the current user
- No PAT (Personal Access Token) required for on-premises TFS
- For Azure DevOps cloud, ensure your Windows user has access

**Key Code Insight:**
```csharp
// TfsConfig.razor - Save configuration
private async Task SaveConfig()
{
    var config = new TfsConfigDto
    {
        Url = _url,
        Project = _project,
        TimeoutSeconds = _timeoutSeconds,
        ApiVersion = _apiVersion
    };
    
    await TfsConfigService.SaveConfigAsync(config);
    Snackbar.Add("Configuration saved", Severity.Success);
}

// Test connection validates the settings
private async Task TestConnection()
{
    var result = await TfsConfigService.TestConnectionAsync();
    if (result.Success)
    {
        Snackbar.Add("Connection successful", Severity.Success);
    }
}
```

### Work Item State Mapping

**Page:** `/settings/workitem-states`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Settings/WorkItemStates.razor`
- **API:** `SettingsController.MapWorkItemStates`

#### What It Does

Maps your TFS work item states to canonical lifecycle states. This is **critical** for accurate metrics calculation.

**Canonical States:**
- **New** - Work not yet started
- **In Progress** - Work currently being done
- **Done** - Work completed
- **Removed** - Work cancelled or removed from scope

#### Use Case: Mapping TechCorp's Custom States

TechCorp uses custom state names in Azure DevOps:
- "Backlog" → Should map to **New**
- "Active", "In Review" → Should map to **In Progress**
- "Closed" → Should map to **Done**
- "Cut" → Should map to **Removed**

Alex needs to map these states so velocity and backlog health metrics calculate correctly.

#### How to Use

1. Navigate to **Settings** → **Work Item States** (`/settings/workitem-states`)
2. For each TFS state in the list, select the corresponding canonical state
3. Click **Save Mapping**
4. The mapping applies immediately to all metrics calculations

**Why This Matters:**
- Velocity calculations count only work items in "Done" state
- Backlog health excludes "Removed" items
- State timeline analysis depends on accurate state mapping

**Key Code Insight:**
```csharp
// WorkItemStates.razor maps TFS states to canonical states
public enum CanonicalWorkItemState
{
    New,
    InProgress,
    Done,
    Removed
}

// Metrics calculations use these mappings
var completedWork = workItems.Where(wi => 
    stateMapping[wi.State] == CanonicalWorkItemState.Done);
```

---

## Product & Team Management

### Managing Products

**Page:** `/settings/products`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Settings/ManageProducts.razor`
- **Sub-component:** `Components/Settings/ProductEditor.razor`
- **Service:** `ProductService`
- **API:** `ProductsController`

#### What It Does

Manage all products in your organization. Products represent your portfolio of work and are linked to specific work items in Azure DevOps.

#### Use Case: Alex Adds Two Products

Alex manages two products at TechCorp:
1. **Product A: Customer Portal** - Backlog root work item ID: 12345
2. **Product B: Admin Dashboard** - Backlog root work item ID: 12389

Each product needs to be registered in PO Companion so work items can be scoped correctly.

#### How to Use

**Adding a Product:**

1. Navigate to **Settings** → **Manage Products** (`/settings/products`)
2. Click **Add Product** button (top right)
3. In the Product Editor:
   - **Name**: Enter product name (e.g., "Customer Portal")
   - **Backlog Root Work Item ID**: Enter the root Goal or Objective ID from Azure DevOps
   - **Assign to Product Owner**: Select your profile (or leave unassigned)
   - **Select Teams**: Choose which teams work on this product
   - **Picture**: Select an icon for the product
4. Click **Save**

**Editing a Product:**

1. In the product list, click the **Edit** icon (pencil) next to a product
2. Update any fields
3. Click **Save**

**Deleting a Product:**

1. Click the **Delete** icon (trash can) next to a product
2. Confirm deletion in the dialog
3. Note: This only removes the product from PO Companion, not from Azure DevOps

**Orphaned Products:**

If a product is not assigned to any Product Owner, it's marked as "Orphan":
- Orphans appear with a warning chip
- Toggle **Show Only Orphans** to filter the list
- Assign orphans to a Product Owner using the Edit function

**Key Code Insight:**
```csharp
// ManageProducts.razor - Product list with orphan detection
var _orphanCount = _products.Count(p => p.ProductOwnerId == null);

// Filter products
private IEnumerable<ProductDto> GetFilteredProducts()
{
    return _showOnlyOrphans 
        ? _products.Where(p => p.ProductOwnerId == null)
        : _products;
}

// ProductDto structure (from Core layer)
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int BacklogRootWorkItemId { get; set; }
    public int? ProductOwnerId { get; set; }
    public List<int> TeamIds { get; set; }
    public int DefaultPictureId { get; set; }
}
```

### Managing Teams

**Page:** `/settings/teams`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Settings/ManageTeams.razor`
- **Service:** `TeamService`
- **API:** `TeamsController`

#### What It Does

Manage teams that deliver work. Teams are linked to products and used for scoping velocity and metrics.

#### Use Case: Alex's Team Structure

Alex's team structure at TechCorp:

**Product A (Customer Portal):**
- Team Alpha - Area path: `TechCorp\CustomerPortal\Alpha`
- Team Beta - Area path: `TechCorp\CustomerPortal\Beta`

**Product B (Admin Dashboard):**
- Team Gamma - Area path: `TechCorp\AdminDashboard\Gamma`
- Team Delta - Area path: `TechCorp\AdminDashboard\Delta`

Alex needs to register all four teams in PO Companion.

#### How to Use

**Adding a Team:**

1. Navigate to **Settings** → **Manage Teams** (`/settings/teams`)
2. Click **Add Team** button
3. Fill in team details:
   - **Name**: Team name (e.g., "Alpha")
   - **Area Path**: Azure DevOps area path (e.g., `TechCorp\CustomerPortal\Alpha`)
   - **Description**: Optional team description
4. Click **Save**

**Editing a Team:**

1. Click **Edit** icon next to a team
2. Update fields
3. Click **Save**

**Archiving a Team:**

1. Click **Archive** next to a team (for teams no longer active)
2. Archived teams are hidden by default
3. Toggle **Show Archived Teams** to view archived teams

**Key Code Insight:**
```csharp
// TeamDto structure
public class TeamDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string AreaPath { get; set; }
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
}

// ManageTeams.razor filters out archived teams by default
var activeTeams = _teams.Where(t => !t.IsArchived || _showArchived);
```

### Managing Product Owner Profiles

**Page:** `/settings/productowner/{ProfileId}` or `/profiles`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Settings/ManageProductOwner.razor`
- **Component:** `PoTool.Client/Pages/ProfilesHome.razor`
- **Service:** `ProfileService`
- **API:** `ProfilesController`

#### What It Does

Create and manage Product Owner profiles. Each profile represents a Product Owner and their assigned products.

#### Use Case: Alex Creates Their Profile

Alex creates a profile in PO Companion:
- **Name**: "Alex Johnson"
- **Display Name**: "Alex J."
- **Assigned Products**: Product A (Customer Portal), Product B (Admin Dashboard)

When Alex launches PO Companion, they select their profile, and all views are automatically scoped to their two products.

#### How to Use

**Creating a Profile:**

1. Navigate to **Profiles Home** (`/profiles`)
2. Click **Create New Profile**
3. Enter profile details:
   - **Name**: Full name
   - **Display Name**: Short display name
   - **Picture**: Select an avatar
4. Click **Save**
5. After creation, assign products to the profile

**Assigning Products to a Profile:**

1. Navigate to **Settings** → **Manage Products** (`/settings/products`)
2. Edit each product you want to assign
3. In **Assign to Product Owner**, select the profile
4. Save each product

**Switching Profiles:**

1. Go to **Profiles Home** (`/profiles`)
2. Click on a different profile card
3. The entire application scope changes to that profile's products

**Key Code Insight:**
```csharp
// ProfileDto structure
public class ProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public int DefaultPictureId { get; set; }
    public List<int> ProductIds { get; set; }
}

// Profile selection changes the scope for all features
var selectedProfile = await ProfileService.GetSelectedProfileAsync();
var products = await ProductService.GetProductsByOwnerAsync(selectedProfile.Id);
```

---

## Release Planning

### Release Planning Board

**Page:** `/release-planning`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/ReleasePlanning.razor`
- **Component:** `PoTool.Client/Components/ReleasePlanning/ReleasePlanningBoard.razor`
- **Service:** `ReleasePlanningService`
- **API:** `ReleasePlanningController`

#### What It Does

The Release Planning Board is a **git-style flow diagram** that helps you visually plan and sequence Epics across Objectives. It shows:
- **Lanes** - Each lane represents an Objective
- **Rows** - Vertical ordering (lower rows = delivered first)
- **Connectors** - Lines showing ordering flow (splits, merges, parallel work)

This is one of PO Companion's most powerful features for **visual release planning**.

#### Use Case: Alex Plans Q2 Release

Alex needs to plan Q2 releases for both products. The Q2 objectives are:

**Product A (Customer Portal):**
- Objective: "Improve User Experience" (ID: 100)
  - Epic 1: Redesign Dashboard (8 weeks)
  - Epic 2: Mobile Responsive Design (6 weeks)
  - Epic 3: Dark Mode Support (4 weeks)

**Product B (Admin Dashboard):**
- Objective: "Enhanced Reporting" (ID: 200)
  - Epic 4: Custom Report Builder (10 weeks)
  - Epic 5: Export to PDF (3 weeks)

Alex wants to:
1. Visualize the delivery sequence
2. Show dependencies (Epic 2 must finish before Epic 3)
3. Mark sprint boundaries
4. Export the plan for stakeholder communication

#### How to Use

**Step 1: Open Release Planning Board**

Navigate to `/release-planning`

**Step 2: Add Lanes (Objectives)**

1. Click **Add Lane** button (toolbar)
2. In the dialog, select Objective ID (e.g., 100 for "Improve User Experience")
3. Click **Add**
4. Repeat for each Objective (e.g., add lane for Objective 200)

**Step 3: Place Epics on the Board**

The board shows two areas:
- **Left side**: Unplanned Epics List (all epics not yet placed)
- **Right side**: Planning board with lanes

To place an Epic:
1. Drag an Epic card from the Unplanned List
2. Drop it into the correct lane (must match parent Objective)
3. Drop at the desired row position (lower = earlier delivery)

**Ordering Rules:**
- Epics can only be placed in their parent Objective's lane
- Lower row numbers = delivered earlier
- Epics in the same row = parallel delivery

**Step 4: Show Dependencies**

The board automatically renders **connector lines** (git-style) between Epics based on:
- Sequential ordering in the same lane
- Dependencies defined in Azure DevOps
- Parallel work streams

**Step 5: Add Milestones and Iteration Lines**

**Add Milestone Line:**
1. Click **Add Milestone** button
2. Enter label (e.g., "Q2 Release")
3. Select vertical position (row index)
4. Select type: Major Release, Minor Release, or Custom
5. Click **Add**

**Add Iteration Line:**
1. Click **Add Iteration** button
2. Enter label (e.g., "Sprint 5")
3. Select vertical position
4. Click **Add**

These lines appear as horizontal markers on the board showing release/sprint boundaries.

**Step 6: Validate and Refresh**

Click **Refresh Validation** to check:
- Data quality issues (missing effort estimates)
- Orphaned epics
- Validation errors/warnings

The board shows indicators for any issues found.

**Step 7: Export the Plan**

1. Click **Export** button
2. Choose export format:
   - PNG image
   - PDF document
3. Save or share with stakeholders

#### Best Practices

From the code (best practices list in ReleasePlanning.razor):
- "Drag Epics from the Unplanned list to place them on the board"
- "Epics can only be moved within their parent Objective's lane"
- "Use Milestone Lines to mark release boundaries"
- "Use Iteration Lines to mark sprint boundaries"
- "Validation indicators show data quality issues that need attention"
- "Export the board as PNG or PDF for stakeholder communication"

#### Key Code Insight

```csharp
// ReleasePlanning.razor - Key concepts described in help text
private List<string> _bestPractices = new()
{
    "Drag Epics from the Unplanned list to place them on the board",
    "Epics can only be moved within their parent Objective's lane",
    "Use Milestone Lines to mark release boundaries",
    "Use Iteration Lines to mark sprint boundaries",
    "Validation indicators show data quality issues that need attention",
    "Export the board as PNG or PDF for stakeholder communication"
};

// ReleasePlanningBoardDto structure (from Shared layer)
public class ReleasePlanningBoardDto
{
    public List<LaneDto> Lanes { get; set; }
    public List<PlacedEpicDto> PlacedEpics { get; set; }
    public List<UnplannedEpicDto> UnplannedEpics { get; set; }
    public List<MilestoneLineDto> MilestoneLines { get; set; }
    public List<IterationLineDto> IterationLines { get; set; }
    public int MaxRowIndex { get; set; }
}

// Lane = Objective, Rows = vertical ordering
public class LaneDto
{
    public int Id { get; set; }
    public int ObjectiveId { get; set; }
    public string ObjectiveTitle { get; set; }
    public int DisplayOrder { get; set; }
}

// PlacedEpicDto = Epic positioned on board
public class PlacedEpicDto
{
    public int EpicId { get; set; }
    public string Title { get; set; }
    public int LaneId { get; set; }
    public int RowIndex { get; set; }
}
```

---

## Metrics & Analytics

### Backlog Health Dashboard

**Page:** `/backlog-health`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Metrics/BacklogHealth.razor`
- **Sub-components:** `BacklogHealthFilters`, `BacklogHealthTrendCard`, `IterationHealthTable`
- **Service:** `BacklogHealthCalculationService`
- **API:** `HealthCalculationController.CalculateScore`, `MetricsController`

#### What It Does

Monitors **backlog quality** across multiple iterations. Calculates a **health score** based on data completeness and quality. Identifies validation issues that need attention.

**Health Score Calculation:**
- Based on percentage of work items without validation issues
- 90-100% = Healthy (green)
- 70-89% = Moderate (yellow)
- <70% = Poor (red)

#### Use Case: Alex Monitors Data Quality

Alex notices that velocity metrics seem off. Using Backlog Health, Alex discovers:

**Product A - Sprint 10:**
- Health Score: 65% (Poor)
- Issues found:
  - 12 PBIs missing effort estimates
  - 5 Features with no child PBIs
  - 3 Epics with incomplete area paths

Alex uses this information to work with Team Alpha and Beta to fix the data quality issues.

#### How to Use

**Step 1: Select Product Scope**

1. Navigate to `/backlog-health`
2. Use the **Product Selector** dropdown:
   - Select "All products" to see aggregated health
   - Select specific product (e.g., "Customer Portal") to scope to one product

**Step 2: Filter by Iteration, Team, or Area**

Use the **Backlog Health Filters** panel:
- **Iteration**: Select specific sprints or "All iterations"
- **Team**: Filter to specific team (e.g., "Team Alpha")
- **Area Path**: Filter by area path

Click **Apply Filters**

**Step 3: Review Health Trend**

The **Backlog Health Trend Card** shows:
- Current health score
- Trend over time (improving/declining)
- Number of iterations analyzed

**Step 4: Drill into Iteration Details**

The **Iteration Health Table** shows each iteration:
- **Iteration name** (e.g., "Sprint 10")
- **Health score** (percentage)
- **Total work items** in iteration
- **Issues count** (errors and warnings)
- **Trend indicator** (↑ improving, ↓ declining, → stable)

Click on an iteration row to see detailed validation issues.

**Step 5: Address Validation Issues**

For each iteration with issues:
1. Click **View Details**
2. See list of specific issues:
   - Missing effort estimates
   - Incomplete area paths
   - Orphaned work items
   - Missing parent links
3. Navigate to Azure DevOps to fix issues
4. Return to PO Companion and sync work items
5. Refresh the health dashboard to see improvements

#### Data Requirements

From the code (data requirements list):
- Work items must have **iteration paths** set
- PBIs and Tasks must have **effort estimates** (Story Points or Hours)
- Work items must have **area paths** matching team definitions
- Parent-child relationships must be properly linked
- Work item states must be mapped correctly (see Work Item State Mapping)

#### Key Code Insight

```csharp
// BacklogHealth.razor - Health score calculation
private async Task LoadHealthDataAsync()
{
    var healthData = await MetricsClient.GetBacklogHealthAsync(
        productId: _selectedProductId,
        iterationPath: _selectedIteration,
        teamId: _selectedTeamId
    );
    
    // Health score = (total items - items with issues) / total items * 100
    foreach (var iteration in healthData.IterationHealth)
    {
        var healthPercentage = 
            (iteration.TotalWorkItems - iteration.IssueCount) * 100.0 
            / iteration.TotalWorkItems;
        iteration.HealthScore = healthPercentage;
    }
}

// HealthCalculationController calculates validation issues
public class IterationHealthDto
{
    public string IterationName { get; set; }
    public int TotalWorkItems { get; set; }
    public int IssueCount { get; set; }
    public double HealthScore { get; set; }
    public List<ValidationIssueDto> Issues { get; set; }
}
```

### Velocity Dashboard

**Page:** `/velocity`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Metrics/VelocityDashboard.razor`
- **Service:** `MetricsClient`
- **API:** `MetricsController.GetVelocityTrend`

#### What It Does

Tracks **completed story points per sprint** to help forecast future work. Shows velocity trends, averages, and sprint-by-sprint breakdown.

#### Use Case: Alex Forecasts Sprint Capacity

Alex needs to plan how much work to commit for upcoming Sprint 11:

**Historical Data:**
- Sprint 8: 32 story points
- Sprint 9: 28 story points
- Sprint 10: 35 story points

**Team Alpha velocity:**
- Average: 31.7 points/sprint
- 3-sprint average: 31.7 points/sprint
- Trend: Stable

Alex uses this data to commit ~30-32 story points for Sprint 11, accounting for some variation.

#### How to Use

**Step 1: Select Scope**

1. Navigate to `/velocity`
2. Use the scope selectors:
   - **Product Scope**: Select product or "All products"
   - **Team Scope**: Select team or "All teams"

**Step 2: Review Summary Metrics**

The dashboard shows **metric cards**:
- **Average Velocity**: Overall average across all sprints
- **Last 3 Sprints**: Recent average (more accurate for short-term forecasting)
- **Total Sprints**: Number of sprints with data
- **Total Points Delivered**: Cumulative story points completed

**Step 3: Analyze Velocity Trend Chart**

The velocity chart shows:
- **X-axis**: Sprint names
- **Y-axis**: Story points completed
- **Bars**: Velocity per sprint
- **Trend line**: Moving average showing direction

Look for:
- Consistent velocity (good for forecasting)
- Upward trend (team improving)
- Downward trend (investigate capacity issues)
- Spikes (investigate outliers)

**Step 4: Review Sprint Details Table**

The sprint details table shows:
- **Sprint name**
- **Story points completed**
- **Number of work items completed**
- **Sprint start/end dates**

Click on a sprint to drill into work item details.

#### Data Requirements

From the code:
- Work items must be in "Done" state (see Work Item State Mapping)
- Work items must have **effort estimates** (Story Points field)
- Work items must have **iteration paths**
- Sprints must have completed work items to appear in the chart

#### Key Code Insight

```csharp
// VelocityDashboard.razor - Loading velocity data
protected override async Task OnInitializedAsync()
{
    await LoadVelocityDataAsync();
}

private async Task LoadVelocityDataAsync()
{
    _velocityTrend = await MetricsClient.GetVelocityTrendAsync(
        productId: _selectedProductId,
        teamId: _selectedTeamId
    );
    
    // Calculate 3-sprint average for short-term forecasting
    var last3Sprints = _velocityTrend.SprintVelocities
        .OrderByDescending(s => s.EndDate)
        .Take(3);
    _velocityTrend.ThreeSprintAverage = last3Sprints.Average(s => s.Velocity);
}

// VelocityTrendDto structure
public class VelocityTrendDto
{
    public double AverageVelocity { get; set; }
    public double ThreeSprintAverage { get; set; }
    public int TotalSprints { get; set; }
    public int TotalPointsDelivered { get; set; }
    public List<SprintVelocityDto> SprintVelocities { get; set; }
}

public class SprintVelocityDto
{
    public string SprintName { get; set; }
    public int Velocity { get; set; }
    public int CompletedWorkItemCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
```

### Epic Completion Forecast

**Page:** `/epic-forecast` or `/epic-forecast/{epicId}`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Metrics/EpicForecast.razor`
- **API:** `MetricsController.GetEpicCompletionForecast`

#### What It Does

Forecasts when an Epic or Feature will be completed based on:
- **Historical velocity** from recent sprints
- **Remaining effort** (story points not yet completed)
- **Velocity variance** (consistency of delivery)

Provides **three scenarios**:
- **Best case**: Assuming high velocity (85th percentile)
- **Likely case**: Assuming average velocity
- **Worst case**: Assuming low velocity (15th percentile)

#### Use Case: Alex Forecasts Epic Completion

Alex needs to forecast when "Redesign Dashboard" (Epic ID: 1001) will be completed:

**Epic Details:**
- Total effort: 120 story points
- Completed: 45 story points
- Remaining: 75 story points

**Team Alpha velocity:**
- Average: 30 points/sprint
- Standard deviation: 4 points

**Forecast Results:**
- Best case: 2 sprints (June 15)
- Likely case: 3 sprints (June 29)
- Worst case: 4 sprints (July 13)

Alex communicates the **likely case** (June 29) to stakeholders with the caveat that it could be as early as June 15 or as late as July 13.

#### How to Use

**Step 1: Navigate to Epic Forecast**

1. Go to `/epic-forecast`
2. Enter **Epic/Feature ID** (e.g., 1001)
3. Set **Historical Sprints** (number of past sprints to use for velocity calculation)
   - Default: 3 sprints
   - Range: 1-20 sprints
4. Click **Calculate**

**Step 2: Review Epic Information**

The Epic Info Card shows:
- **Title**: Epic/Feature name
- **ID**: Work item ID
- **Type**: Epic or Feature
- **Area Path**: Team/area assignment
- **Confidence**: High/Medium/Low (based on velocity consistency)

**Step 3: Review Progress Summary**

Summary cards show:
- **Total Effort**: Total story points for the epic
- **Completed Effort**: Story points already done
- **Remaining Effort**: Story points left
- **Progress Percentage**: % complete

**Step 4: Analyze Forecast Scenarios**

The forecast section shows three scenarios:

**Best Case:**
- **Completion Date**: Earliest likely completion
- **Sprints Remaining**: Minimum sprints needed
- **Assumption**: Team delivers at 85th percentile velocity

**Likely Case:**
- **Completion Date**: Most probable completion
- **Sprints Remaining**: Expected sprints
- **Assumption**: Team delivers at average velocity

**Worst Case:**
- **Completion Date**: Latest likely completion
- **Sprints Remaining**: Maximum sprints
- **Assumption**: Team delivers at 15th percentile velocity

**Step 5: Review Velocity Basis**

The dashboard shows the velocity calculation basis:
- **Velocity used**: Average velocity from selected historical sprints
- **Standard deviation**: Measure of velocity consistency
- **Confidence level**: 
  - High confidence = low standard deviation (consistent velocity)
  - Low confidence = high standard deviation (variable velocity)

#### Best Practices

From the code (best practices):
- "Use 3-5 recent sprints for most accurate forecasts"
- "Re-forecast after significant scope changes"
- "Account for team capacity changes (vacations, new members)"
- "Communicate likely case, but prepare for worst case"
- "Monitor forecast vs. actual and adjust assumptions"

#### Key Code Insight

```csharp
// EpicForecast.razor - Forecast calculation
private async Task LoadForecast()
{
    _forecastData = await MetricsClient.GetEpicCompletionForecastAsync(
        epicId: _selectedEpicId.Value,
        maxSprintsForVelocity: _maxSprintsForVelocity
    );
}

// EpicCompletionForecastDto structure
public class EpicCompletionForecastDto
{
    public int EpicId { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string AreaPath { get; set; }
    
    // Progress
    public int TotalEffort { get; set; }
    public int CompletedEffort { get; set; }
    public int RemainingEffort { get; set; }
    public double ProgressPercentage { get; set; }
    
    // Velocity basis
    public double AverageVelocity { get; set; }
    public double StandardDeviation { get; set; }
    public string Confidence { get; set; } // High/Medium/Low
    
    // Forecast scenarios
    public ForecastScenarioDto BestCase { get; set; }
    public ForecastScenarioDto LikelyCase { get; set; }
    public ForecastScenarioDto WorstCase { get; set; }
}

public class ForecastScenarioDto
{
    public DateTime CompletionDate { get; set; }
    public int SprintsRemaining { get; set; }
    public double VelocityAssumption { get; set; }
}

// Confidence is based on coefficient of variation
var coefficientOfVariation = standardDeviation / averageVelocity;
var confidence = coefficientOfVariation < 0.15 ? "High" 
    : coefficientOfVariation < 0.30 ? "Medium" 
    : "Low";
```

### Dependency Graph Visualization

**Page:** `/dependency-graph`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Metrics/DependencyGraph.razor`
- **API:** `MetricsController.GetDependencyGraph`

#### What It Does

Visualizes **work item dependencies** and **hierarchical relationships**. Shows:
- Parent-child relationships (Goal → Objective → Epic → Feature → PBI → Task)
- Dependency links (predecessor/successor)
- Critical paths
- Blocking items

#### Use Case: Alex Identifies Blocking Dependencies

Alex needs to understand why Epic 1001 ("Redesign Dashboard") is blocked. Using the Dependency Graph:

**Discovered:**
- Epic 1001 depends on Epic 1050 ("API Modernization" - Product B)
- Epic 1050 is only 30% complete (Team Gamma)
- This is a cross-product dependency!

**Action:**
- Alex coordinates with Product B's team to prioritize Epic 1050
- Updates stakeholders about the dependency
- Adjusts forecast for Epic 1001

#### How to Use

**Step 1: Navigate to Dependency Graph**

1. Go to `/dependency-graph`
2. Optionally, enter a specific work item ID to center the graph on that item

**Step 2: Explore the Graph**

The graph visualization shows:
- **Nodes**: Work items (sized by effort, colored by state)
- **Edges**: Dependencies and parent-child links
- **Clusters**: Grouped by hierarchy level or area path

**Interaction:**
- **Pan**: Click and drag background
- **Zoom**: Mouse wheel or pinch
- **Click node**: Show work item details
- **Hover**: Show dependencies

**Step 3: Identify Critical Issues**

Look for:
- **Red nodes**: Blocked items or items with missing dependencies
- **Thick edges**: Strong dependencies (many dependent items)
- **Long chains**: Potential bottlenecks
- **Cross-product edges**: Dependencies between products

**Step 4: Export or Share**

- Click **Export** to save the graph as PNG or SVG
- Share with team to discuss dependencies

#### Key Code Insight

```csharp
// DependencyGraphDto structure
public class DependencyGraphDto
{
    public List<GraphNodeDto> Nodes { get; set; }
    public List<GraphEdgeDto> Edges { get; set; }
}

public class GraphNodeDto
{
    public int WorkItemId { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string State { get; set; }
    public int? Effort { get; set; }
    public int Level { get; set; } // Hierarchy level (0=Goal, 5=Task)
}

public class GraphEdgeDto
{
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string EdgeType { get; set; } // "Parent", "Dependency"
}
```

### Work Item State Timeline

**Page:** `/state-timeline` or `/state-timeline/{workItemId}`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Metrics/StateTimeline.razor`
- **API:** `MetricsController.GetWorkItemStateTimeline`

#### What It Does

Tracks **work item lifecycle** and **state transitions** over time. Useful for:
- Identifying bottlenecks (items stuck in one state)
- Calculating cycle time and lead time
- Analyzing team efficiency

#### Use Case: Alex Investigates Cycle Time

Alex notices that PBIs in Sprint 10 are taking longer than expected. Using State Timeline for PBI 5678:

**Timeline:**
- Created: Jan 5 (New state)
- Started: Jan 10 (In Progress) — **5 days in New**
- Code review: Jan 25 (In Review) — **15 days in In Progress**
- Completed: Feb 1 (Done) — **7 days in In Review**

**Analysis:**
- **Lead time**: 27 days (Jan 5 to Feb 1)
- **Cycle time**: 22 days (Jan 10 to Feb 1)
- **Bottleneck**: 15 days in "In Progress" state is excessive

**Action:**
- Alex discusses with Team Alpha to understand why development took 15 days
- Discovers the PBI had unclear acceptance criteria
- Implements "Definition of Ready" checklist to prevent similar issues

#### How to Use

**Step 1: Navigate to State Timeline**

1. Go to `/state-timeline`
2. Enter **Work Item ID** (e.g., 5678)
3. Click **Load Timeline**

**Step 2: Review Timeline Visualization**

The timeline shows:
- **Horizontal axis**: Time (dates)
- **Vertical segments**: States (colored by state type)
- **Segment length**: Time spent in each state

**Step 3: Analyze Metrics**

Summary cards show:
- **Lead Time**: Total time from creation to completion
- **Cycle Time**: Time from first "In Progress" to "Done"
- **State Count**: Number of state transitions
- **Rework**: Times moved backward to previous states

**Step 4: Identify Patterns**

Look for:
- **Long segments**: Bottlenecks (state where items get stuck)
- **Many transitions**: Possible rework or unclear process
- **Long lead time**: Items waiting too long to start
- **Long cycle time**: Development taking too long

#### Key Code Insight

```csharp
// WorkItemStateTimelineDto structure
public class WorkItemStateTimelineDto
{
    public int WorkItemId { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    
    // Lifecycle metrics
    public int LeadTimeDays { get; set; }
    public int CycleTimeDays { get; set; }
    public int StateTransitionCount { get; set; }
    public int ReworkCount { get; set; }
    
    // Timeline segments
    public List<StateSegmentDto> Segments { get; set; }
}

public class StateSegmentDto
{
    public string StateName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationDays { get; set; }
}
```

### Effort Distribution Heat Map

**Page:** `/effort-distribution`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Metrics/EffortDistribution.razor`
- **API:** `MetricsController.GetEffortDistribution`

#### What It Does

Visualizes **capacity planning** and **effort distribution** across teams and iterations. Shows:
- Heat map of effort by area path and iteration
- Over-allocated teams (red)
- Under-allocated teams (green)
- Balanced distribution (yellow)

#### Use Case: Alex Balances Team Workload

Alex is planning Q2 and wants to ensure teams are evenly loaded:

**Effort Distribution for Sprint 11-15:**

| Team | Sprint 11 | Sprint 12 | Sprint 13 | Sprint 14 | Sprint 15 |
|------|-----------|-----------|-----------|-----------|-----------|
| Alpha | 45 pts 🔴 | 32 pts | 30 pts | 28 pts | 35 pts |
| Beta | 18 pts 🟢 | 30 pts | 32 pts | 30 pts | 28 pts |
| Gamma | 35 pts | 35 pts | 40 pts 🟠 | 38 pts | 32 pts |
| Delta | 28 pts | 30 pts | 28 pts | 30 pts | 30 pts |

**Analysis:**
- Team Alpha is **over-allocated** in Sprint 11 (45 pts vs 30-35 avg capacity)
- Team Beta is **under-allocated** in Sprint 11 (18 pts)

**Action:**
- Move some work from Team Alpha to Team Beta for Sprint 11
- Result: Balanced workload, reduced risk of sprint failure

#### How to Use

**Step 1: Navigate to Effort Distribution**

1. Go to `/effort-distribution`

**Step 2: Select Scope**

- **Product**: Select product or "All products"
- **Iterations**: Select sprint range (e.g., "Sprint 11 - Sprint 15")

**Step 3: Review Heat Map**

The heat map shows:
- **Rows**: Teams or area paths
- **Columns**: Iterations
- **Cell color**: 
  - 🟢 Green: Under-allocated (<70% capacity)
  - 🟡 Yellow: Well-balanced (70-100% capacity)
  - 🟠 Orange: Near capacity (100-120% capacity)
  - 🔴 Red: Over-allocated (>120% capacity)
- **Cell value**: Total story points planned

**Step 4: Identify Imbalances**

Look for:
- Red cells (over-allocated) — Need to move work out
- Green cells (under-allocated) — Can take on more work
- Patterns (one team always over-allocated)

**Step 5: Take Action**

- Click on a cell to see work items in that team/iteration
- Use Release Planning Board to re-sequence work
- Coordinate with teams to move work items

#### Key Code Insight

```csharp
// EffortDistributionDto structure
public class EffortDistributionDto
{
    public List<EffortCellDto> Cells { get; set; }
    public List<string> Teams { get; set; }
    public List<string> Iterations { get; set; }
}

public class EffortCellDto
{
    public string TeamName { get; set; }
    public string IterationName { get; set; }
    public int TotalEffort { get; set; }
    public int TeamCapacity { get; set; }
    public double UtilizationPercentage { get; set; }
    public string AllocationLevel { get; set; } // Under/Balanced/Near/Over
}

// Heat map color logic
var color = utilizationPercentage < 70 ? "green"
    : utilizationPercentage < 100 ? "yellow"
    : utilizationPercentage < 120 ? "orange"
    : "red";
```

---

## Pipeline & Pull Request Insights

### Pipeline Insights

**Page:** `/pipeline-insights`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Pipelines/PipelineInsights.razor`
- **Sub-components:** `PipelineMetricsSummaryPanel`, `PipelineHealthTable`, `PipelineDurationChart`
- **API:** `PipelinesController`

#### What It Does

Monitors **CI/CD pipeline health** and **build performance**. Shows:
- Success/failure rates
- Build duration trends
- Failed pipeline runs
- Deployment frequency

#### Use Case: Alex Monitors Build Health

Alex notices that deployments are taking longer than usual. Using Pipeline Insights:

**Discovered:**
- Product A build duration increased from 8 minutes to 15 minutes over last 2 weeks
- Success rate dropped from 95% to 82%
- Most failures are in "Product.A.CI" pipeline

**Action:**
- Alex escalates to engineering leads
- Team investigates and finds slow database migration scripts
- Build duration returns to normal after optimization

#### How to Use

**Step 1: Navigate to Pipeline Insights**

1. Go to `/pipeline-insights`

**Step 2: Select Product**

- Use product selector to scope to specific product or "All products"

**Step 3: Review Summary Metrics**

Summary cards show:
- **Total Pipelines**: Number of pipelines tracked
- **Average Success Rate**: Overall success percentage
- **Average Duration**: Mean build time
- **Failed Runs (Last 7 Days)**: Recent failure count

**Step 4: Analyze Pipeline Health Table**

The table shows each pipeline:
- **Pipeline Name**
- **Last Run**: Date/time of most recent run
- **Status**: Success/Failed/In Progress
- **Success Rate (30 days)**
- **Average Duration**
- **Trend**: ↑/↓/→ for duration

Click on a pipeline to see detailed run history.

**Step 5: Review Duration Chart**

The duration chart shows build times over time:
- X-axis: Date
- Y-axis: Duration (minutes)
- Lines: One per pipeline

Look for:
- Upward trends (builds getting slower)
- Spikes (investigate specific runs)
- Patterns (certain pipelines always slow)

#### Key Code Insight

```csharp
// PipelineDto structure
public class PipelineDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime? LastRunDate { get; set; }
    public string LastRunStatus { get; set; }
    public double SuccessRate { get; set; }
    public int AverageDurationMinutes { get; set; }
    public string Trend { get; set; }
    public List<PipelineRunDto> RecentRuns { get; set; }
}

public class PipelineRunDto
{
    public int Id { get; set; }
    public DateTime RunDate { get; set; }
    public string Status { get; set; }
    public int DurationMinutes { get; set; }
    public string Branch { get; set; }
}
```

### Pull Request Insights

**Page:** `/pr-insights`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/PullRequests/PRInsight.razor`
- **Sub-components:** `PRMetricsSummaryPanel`, `PRStatusChart`, `PRTimeOpenChart`, `PRUserChart`
- **API:** `PullRequestsController`

#### What It Does

Tracks **pull request metrics** and **code review bottlenecks**. Shows:
- PR cycle time (time from open to merge)
- Review bottlenecks (PRs waiting too long)
- PR volume by team member
- Open vs. closed PR trends

#### Use Case: Alex Identifies Review Bottleneck

Alex notices that Feature 2001 is behind schedule despite code being written. Using PR Insights:

**Discovered:**
- Average PR time to merge: 4.5 days (target: 1 day)
- 8 PRs open for >5 days (all from Team Alpha)
- Reviewer bottleneck: One senior developer reviewing all PRs

**Action:**
- Alex works with Team Alpha lead to distribute review load
- Implements "pair review" where junior devs review with senior oversight
- PR cycle time drops to 1.5 days within 2 weeks

#### How to Use

**Step 1: Navigate to PR Insights**

1. Go to `/pr-insights`

**Step 2: Select Filters**

- **Repository**: Select specific repository or "All repositories"
- **Date Range**: Select time range (e.g., "Last 30 days")
- **Team/User**: Filter by team or user

**Step 3: Review Summary Metrics**

Summary cards show:
- **Total PRs**: Count of pull requests
- **Average Time to Merge**: Mean cycle time
- **Open PRs**: Currently open
- **Merged PRs**: Successfully merged
- **Closed PRs**: Closed without merge

**Step 4: Analyze PR Status Chart**

Pie chart shows distribution:
- Open
- Merged
- Closed

**Step 5: Review Time Open Chart**

Bar chart shows PRs by time open:
- <1 day (green)
- 1-3 days (yellow)
- 3-7 days (orange)
- >7 days (red)

Identify red bars as review bottlenecks.

**Step 6: Review PR User Chart**

Shows PR volume by user:
- Who creates most PRs
- Who reviews most PRs
- Imbalances in contribution

#### Key Code Insight

```csharp
// PullRequestDto structure
public class PullRequestDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? MergedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string Status { get; set; } // Open/Merged/Closed
    public int DaysOpen { get; set; }
    public List<string> Reviewers { get; set; }
    public int CommentCount { get; set; }
}

// Time to merge calculation
var timeToMerge = pr.MergedDate.HasValue 
    ? (pr.MergedDate.Value - pr.CreatedDate).TotalDays 
    : -1;
```

---

## Advanced Features

### Product Home Dashboard

**Page:** `/` or `/product-home`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/ProductHome.razor`

#### What It Does

The **main dashboard** and landing page after profile selection. Shows:
- Overview of all products assigned to current profile
- Key metrics for each product
- Quick access to all features
- Recent activity

This is Alex's daily starting point.

#### How to Use

**Step 1: Select Profile**

If multiple profiles exist:
1. Go to `/profiles`
2. Select your profile
3. You're redirected to Product Home

**Step 2: Review Product Cards**

Each product shows:
- Product name and icon
- Quick metrics:
  - Open work items
  - Epics in progress
  - Current sprint velocity
  - Backlog health score
- Quick action buttons

**Step 3: Navigate to Feature**

From Product Home, click:
- **Release Planning** - Plan releases
- **Velocity** - View velocity dashboard
- **Backlog Health** - Check data quality
- **Settings** - Manage products/teams

### Help & Data Requirements

**Page:** `/help`

**Implementation Reference:**
- **Component:** `PoTool.Client/Pages/Help.razor`

#### What It Does

Provides comprehensive **data requirements** and **validation rules** for all features. This is the reference guide for what data must exist in Azure DevOps for each feature to work.

#### Use Case: Alex Troubleshoots Missing Data

Alex notices velocity dashboard is empty. Checks Help page and discovers:

**Velocity Data Requirements:**
- Work items must be in "Done" state (check Work Item State Mapping)
- Work items must have effort estimates (Story Points field)
- Work items must have iteration paths

Alex realizes Team Beta's PBIs don't have Story Points field populated. After fixing, velocity appears correctly.

#### How to Use

**Step 1: Navigate to Help Page**

1. Go to `/help` or click **Help** icon (top right)

**Step 2: Select Feature**

Choose the feature you need help with:
- Backlog Health
- Velocity Dashboard
- Epic Forecast
- Release Planning
- etc.

**Step 3: Review Data Requirements**

Each feature section shows:
- **What data is required** in Azure DevOps
- **How data should be structured**
- **Common issues** and solutions
- **Best practices**

**Step 4: Validate Your Data**

Use the checklist to verify your data meets requirements:
- [ ] Work items have iteration paths
- [ ] PBIs have effort estimates
- [ ] States are mapped correctly
- [ ] Parent-child links exist
- etc.

### Keyboard Shortcuts

**Shortcut:** Press `?` (question mark) anywhere in the app

**Implementation Reference:**
- **Component:** `Components/Common/KeyboardShortcutsDialog.razor`

#### What It Does

Shows **keyboard shortcuts** for power users. Provides quick access to common actions without using the mouse.

#### Common Shortcuts

| Shortcut | Action |
|----------|--------|
| `?` | Show keyboard shortcuts dialog |
| `h` | Go to home/product dashboard |
| `r` | Go to release planning |
| `v` | Go to velocity dashboard |
| `b` | Go to backlog health |
| `p` | Go to pipeline insights |
| `/` | Focus search box |
| `Esc` | Close dialog/modal |

---

## Appendix: Page Reference

Quick reference of all pages and their routes:

| Feature | Route | Component |
|---------|-------|-----------|
| **Home & Setup** |
| Product Home | `/` or `/product-home` | `ProductHome.razor` |
| Profiles Home | `/profiles` | `ProfilesHome.razor` |
| Onboarding | `/onboarding` | `Onboarding.razor` |
| TFS Configuration | `/tfsconfig` | `TfsConfig.razor` |
| Help | `/help` | `Help.razor` |
| **Settings** |
| Manage Products | `/settings/products` | `Settings/ManageProducts.razor` |
| Manage Teams | `/settings/teams` | `Settings/ManageTeams.razor` |
| Manage Product Owner | `/settings/productowner/{id}` | `Settings/ManageProductOwner.razor` |
| Edit Product Owner | `/settings/productowner/edit/{id}` | `Settings/EditProductOwner.razor` |
| Work Item States | `/settings/workitem-states` | `Settings/WorkItemStates.razor` |
| **Planning** |
| Release Planning | `/release-planning` | `ReleasePlanning.razor` |
| **Metrics** |
| Backlog Health | `/backlog-health` | `Metrics/BacklogHealth.razor` |
| Velocity Dashboard | `/velocity` | `Metrics/VelocityDashboard.razor` |
| Epic Forecast | `/epic-forecast` or `/epic-forecast/{id}` | `Metrics/EpicForecast.razor` |
| Dependency Graph | `/dependency-graph` | `Metrics/DependencyGraph.razor` |
| State Timeline | `/state-timeline` or `/state-timeline/{id}` | `Metrics/StateTimeline.razor` |
| Effort Distribution | `/effort-distribution` | `Metrics/EffortDistribution.razor` |
| **Code Quality** |
| Pipeline Insights | `/pipeline-insights` | `Pipelines/PipelineInsights.razor` |
| PR Insights | `/pr-insights` | `PullRequests/PRInsight.razor` |

---

## Conclusion

This manual covers all major features of PO Companion based on the current codebase implementation. Each feature description is derived directly from the code, ensuring accuracy.

**For Alex and Other Product Owners:**

PO Companion is designed to help you:
1. **Plan** - Use Release Planning Board to visualize and sequence epics
2. **Monitor** - Track velocity, backlog health, and forecast completion
3. **Optimize** - Balance team workload and identify bottlenecks
4. **Communicate** - Export plans and share metrics with stakeholders

The key to success with PO Companion is **data quality**. Ensure your Azure DevOps work items have:
- Effort estimates
- Iteration paths
- Area paths
- Parent-child links
- Correctly mapped states

With quality data, PO Companion becomes an indispensable tool for managing multiple products and teams effectively.

---

**Manual Version:** 1.0  
**Last Updated:** Based on codebase as of latest commit  
**Implementation Source:** All descriptions verified against code in `PoTool.Client/Pages/`, `PoTool.Client/Components/`, and `PoTool.Api/Controllers/`
