# PO Companion - User Manual

**Version:** 2.0  
**Date:** January 2026  
**For:** Product Owners managing multiple products and teams

---

## Table of Contents

1. [About This Manual](#about-this-manual)
2. [Quick Start Guide](#quick-start-guide)
3. [Feature Overview](#feature-overview)
4. [Detailed Feature Guide](#detailed-feature-guide)
   - [Dashboard](#dashboard)
   - [Profiles Management](#profiles-management)
   - [TFS Configuration](#tfs-configuration)
   - [Work Items Explorer](#work-items-explorer)
   - [Backlog Health](#backlog-health)
   - [Effort Distribution](#effort-distribution)
   - [Velocity Dashboard](#velocity-dashboard)
   - [Epic Forecast](#epic-forecast)
   - [State Timeline Analysis](#state-timeline-analysis)
   - [Dependency Graph](#dependency-graph)
   - [PR Insights](#pr-insights)
   - [Pipeline Insights](#pipeline-insights)
   - [Settings Pages](#settings-pages)
5. [Day-to-Day Scenarios](#day-to-day-scenarios)
6. [Keyboard Shortcuts](#keyboard-shortcuts)
7. [Troubleshooting](#troubleshooting)

---

## About This Manual

This manual is designed for Product Owners who manage multiple products and teams using Azure DevOps/TFS. Throughout this manual, we'll use scenarios featuring **Sarah**, a Product Owner managing two products:

- **Product A (Mobile App)** - Team Alpha (5 developers)
- **Product B (Web Portal)** - Team Beta (7 developers)

Each feature section includes:
1. **Use Case** - What the feature is and why it matters
2. **Sub-Features** - Key capabilities within the feature
3. **Day-to-Day Usage** - Real scenarios showing how Sarah uses it
4. **How-To Guide** - Step-by-step instructions

---

## Quick Start Guide

### Prerequisites
- Azure DevOps or TFS access
- Personal Access Token (PAT) with "Work Items (Read)" scope
- Your organization URL and project name

### First-Time Setup (5 minutes)

1. **Launch PO Companion**
   - The application starts with an integrated API server
   - You'll see the Onboarding Wizard on first launch

2. **Connect to Azure DevOps**
   - Navigate to TFS Config page
   - Enter your organization URL (e.g., `https://dev.azure.com/yourorg`)
   - Enter your project name
   - Paste your Personal Access Token
   - Click "Test Connection" to verify

3. **Create Your Profile**
   - Go to Profiles page
   - Click "Add Profile"
   - Enter your name
   - Select your area paths (products/teams you manage)
   - Save the profile

4. **Sync Your Data**
   - Go to Work Items page
   - Click the "Sync" button
   - Wait for initial sync to complete (this may take a few minutes)

5. **Explore the Dashboard**
   - Return to Dashboard to see your overview
   - Select your profile from the dropdown
   - View key metrics for your products

You're ready to go! 🎉

---

## Feature Overview

PO Companion provides these key functional areas:

### Core Management
- **Dashboard** - Overview hub with key metrics
- **Profiles** - Manage user profiles and configurations
- **TFS Config** - Azure DevOps connection setup
- **Work Items** - Hierarchical work item explorer with validation

### Metrics & Analytics
- **Backlog Health** - Monitor data quality and validation issues
- **Effort Distribution** - Heat map for capacity planning
- **Velocity Dashboard** - Track team performance and forecast
- **Epic Forecast** - Predict completion dates
- **State Timeline** - Analyze work item lifecycle
- **Dependency Graph** - Visualize dependencies and bottlenecks

### DevOps Insights
- **PR Insights** - Pull request metrics and patterns
- **Pipeline Insights** - CI/CD health monitoring

### Configuration
- **Manage Products** - Product configuration
- **Manage Teams** - Team setup and management
- **Work Item States** - State mapping configuration

---

## Detailed Feature Guide

---

## Dashboard

### Use Case: Product Overview Hub

**What It Is:**  
The Dashboard is your central command center. It provides an at-a-glance view of the health and status of your selected product, showing key performance indicators that matter most to Product Owners.

**Why It Matters:**  
As a Product Owner managing multiple products, you need quick visibility into each product's health without diving into detailed reports. The Dashboard aggregates critical metrics so you can identify issues early and make informed decisions quickly.

**Sub-Features:**
- Profile selection dropdown for switching between products
- Key metrics cards (velocity, backlog health, capacity)
- Quick links to detailed analytics
- Real-time sync status
- Recent activity feed
- Team performance summary

### Day-to-Day Usage Scenario

**Scenario: Monday Morning Check-In**

Sarah starts her Monday by checking both products:

1. **Morning Review for Product A (Mobile App)**
   - Opens PO Companion
   - Sees Dashboard showing Team Alpha's metrics
   - Notices velocity dropped 15% last sprint - needs investigation
   - Backlog health score is 87% - acceptable but trending down
   - Sees 3 epics are at risk based on forecast

2. **Switch to Product B (Web Portal)**
   - Switches profile to Product B in dropdown
   - Dashboard refreshes with Team Beta's data
   - Velocity is stable at 42 points/sprint
   - Backlog health is excellent at 94%
   - One critical dependency blocking 2 features

3. **Prioritize Her Day**
   - Team Alpha needs attention due to velocity drop
   - Schedule conversation with Team Beta about blocked dependency
   - Check detailed reports after standup

### How To Use the Dashboard

**Navigation:**
- **Page Location:** `/` (home page)
- **Menu:** Click "Dashboard" in the main navigation
- **Keyboard Shortcut:** `Ctrl+H` (Home)

**Step-by-Step:**

1. **Select Your Profile**
   ```
   Top of page → Profile dropdown → Select "Sarah - Product A"
   ```
   - Click the profile dropdown in the header
   - Choose which product/team combination to view
   - Dashboard updates automatically

2. **Read Key Metrics**
   ```
   Dashboard displays cards:
   ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
   │  Velocity       │  │ Backlog Health  │  │ Team Capacity   │
   │  38 pts/sprint  │  │    87%  ↓       │  │    78%  ✓       │
   └─────────────────┘  └─────────────────┘  └─────────────────┘
   ```
   - Each card shows current value
   - Arrows indicate trends (↑ up, ↓ down, → stable)
   - Color coding: green (good), yellow (warning), red (critical)

3. **Click Through to Details**
   - Click any metric card to navigate to detailed page
   - Example: Click "Velocity 38 pts/sprint" → opens Velocity Dashboard
   - Use browser back button to return to Dashboard

4. **Sync Data**
   - Top right: "Last synced: 2 hours ago"
   - Click "Sync Now" button to refresh from Azure DevOps
   - Sync indicator shows progress
   - Dashboard updates automatically when sync completes

5. **View Recent Activity**
   - Scroll down to see recent work item changes
   - Filter by: "My Changes", "Team Changes", "All Changes"
   - Click work item to open details panel

**Tips:**
- Set up multiple browser tabs for each product if you switch frequently
- Dashboard auto-refreshes every 5 minutes when tab is active
- Bookmark Dashboard as your startup page
- Use the Dashboard before and after sprint ceremonies

---

## Profiles Management

### Use Case: Multi-Product Configuration

**What It Is:**  
Profiles allow you to define different views of your Azure DevOps data. Each profile represents a specific combination of products, teams, and area paths that you want to track separately.

**Why It Matters:**  
Product Owners often manage multiple products with different teams, backlogs, and metrics. Profiles let you segment your data so metrics are accurate and relevant to each context. Without profiles, velocity and health metrics would be mixed across all your work, making them meaningless.

**Sub-Features:**
- Create multiple profiles
- Configure area paths per profile
- Set default profile
- Profile-specific filters
- Quick profile switching
- Profile import/export
- Archive unused profiles

### Day-to-Day Usage Scenario

**Scenario: Quarterly Planning Preparation**

Sarah needs to prepare quarterly plans for both products:

1. **Review Current Profiles**
   - Has "Product A - Mobile App" profile tracking:
     - Area Path: `Project\Mobile\Team Alpha`
     - Focus: User authentication and payment features
   - Has "Product B - Web Portal" profile tracking:
     - Area Path: `Project\Web\Team Beta`
     - Focus: Admin dashboard and reporting

2. **Create New Profile for Combined View**
   - Creates "Q1 All Products" profile
   - Selects both area paths
   - Uses this for executive reporting
   - Keeps original profiles for day-to-day work

3. **Configure Profile Settings**
   - Sets "Product A" as default (opens on startup)
   - Archives old profile from previous year
   - Exports profile configuration as backup

### How To Use Profiles

**Navigation:**
- **Page Location:** `/profiles`
- **Menu:** Click "Profiles" in main navigation
- **Keyboard Shortcut:** `Ctrl+P`

**Step-by-Step:**

1. **View Existing Profiles**
   ```
   Profiles page shows:
   ┌─────────────────────────────────────────────────────────┐
   │ Profile Name         │ Area Paths      │ Actions       │
   │─────────────────────────────────────────────────────────│
   │ Product A - Mobile   │ Project\Mobile  │ [Edit] [Del]  │
   │ Product B - Web      │ Project\Web     │ [Edit] [Del]  │
   └─────────────────────────────────────────────────────────┘
   ```

2. **Create a New Profile**
   - Click "Add Profile" button (top right)
   - Dialog opens with form:
     ```
     Profile Name: [Product A - Mobile App]
     Description:  [Team Alpha mobile application features]
     Area Paths:   [Select from dropdown] ▼
                   ☑ Project\Mobile\Team Alpha
                   ☐ Project\Mobile\Team Gamma
                   ☐ Project\Web\Team Beta
     Default:      ☐ Set as default profile
     ```
   - Enter profile name (required)
   - Add description (optional but recommended)
   - Select one or more area paths
   - Check "Set as default" if this should be your startup profile
   - Click "Save"

3. **Edit an Existing Profile**
   - Click "Edit" button next to profile
   - Modify name, description, or area paths
   - You cannot change area paths if historical metrics depend on them (warning shown)
   - Click "Save Changes"

4. **Switch Active Profile**
   - Method 1: Use dropdown in header (any page)
     ```
     Header → Profile: [Product A - Mobile ▼] → Select profile
     ```
   - Method 2: Click profile name on Profiles page
   - All pages update automatically to show selected profile's data

5. **Delete a Profile**
   - Click "Delete" button next to profile
   - Confirmation dialog: "Are you sure? Historical data will be preserved but metrics will no longer calculate for this profile."
   - Click "Confirm Delete"
   - Profile removed from list

6. **Export/Import Profiles** (Advanced)
   - Click "Export All" button
   - Downloads JSON file with profile configurations
   - Use "Import" button to restore profiles
   - Useful for backup or moving to another instance

**Tips:**
- Keep profile names short but descriptive (shown in dropdown)
- Use descriptions to note team members or focus areas
- Create profiles by product/team, not by individual
- Don't delete profiles mid-sprint (wait until sprint ends)
- Review and archive old profiles quarterly

---

## TFS Configuration

### Use Case: Azure DevOps Connection Setup

**What It Is:**  
TFS Configuration is where you establish and manage the connection between PO Companion and your Azure DevOps or Team Foundation Server instance. This is the critical first step that enables all other features.

**Why It Matters:**  
Without a proper connection, PO Companion cannot sync work items, pull requests, or pipeline data. This page also handles authentication securely using Personal Access Tokens, ensuring your credentials are protected while giving the application necessary read access.

**Sub-Features:**
- Organization URL configuration
- Project selection
- Personal Access Token (PAT) management
- Connection testing
- Team picker
- Authentication method selection (PAT, OAuth)
- Proxy configuration
- Connection status monitoring

### Day-to-Day Usage Scenario

**Scenario: Onboarding New Team Member**

Sarah's team gets a new developer who needs to use PO Companion:

1. **Guide New User Through Setup**
   - Helps them navigate to TFS Config page
   - Provides organization URL: `https://dev.azure.com/companyname`
   - Tells them project name: `MainProject`

2. **Create PAT in Azure DevOps**
   - Opens Azure DevOps → User Settings → Personal Access Tokens
   - Clicks "New Token"
   - Configures:
     - Name: "PO Companion - Read Access"
     - Expiration: 90 days (company policy)
     - Scopes: "Work Items (Read)", "Code (Read)", "Build (Read)"
   - Copies token immediately (won't be shown again)

3. **Configure in PO Companion**
   - Pastes token into PAT field
   - Clicks "Test Connection"
   - Sees success message
   - Selects team from dropdown
   - Saves configuration

4. **Quarterly PAT Renewal**
   - Every 90 days, updates PAT before expiration
   - Same process but uses existing configuration
   - Tests connection to verify

### How To Use TFS Configuration

**Navigation:**
- **Page Location:** `/tfsconfig`
- **Menu:** Click "TFS Config" in main navigation
- **Keyboard Shortcut:** None
- **Auto-opens:** On first launch (if not configured)

**Step-by-Step:**

1. **Access Configuration Page**
   ```
   Main Navigation → TFS Config
   ```
   - Page shows connection form
   - Current status: "Not Connected" (red) or "Connected" (green)

2. **Enter Organization Details**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Azure DevOps Connection                                 │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Organization URL:                                       │
   │ [https://dev.azure.com/yourorgname                   ] │
   │                                                         │
   │ Project Name:                                           │
   │ [YourProjectName                                      ] │
   │                                                         │
   │ Personal Access Token:                                  │
   │ [********************************                      ] │
   │ [Show] [Generate New]                                   │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Field Details:**
   - **Organization URL:** 
     - Format: `https://dev.azure.com/yourorg` (Azure DevOps)
     - Or: `https://tfs.company.com/tfs/collection` (TFS on-premise)
     - No trailing slash
   - **Project Name:**
     - Exact name as shown in Azure DevOps
     - Case-sensitive
     - Example: "MainProject" not "mainproject"
   - **Personal Access Token:**
     - Copy/paste from Azure DevOps
     - Hidden by default (shows as asterisks)
     - Click "Show" to reveal temporarily

3. **Create Personal Access Token** (If needed)
   - Click "Generate New" link → opens Azure DevOps
   - Alternative: Manual steps:
     1. Azure DevOps → Click your profile (top right) → Security
     2. Personal Access Tokens → New Token
     3. Configure token:
        ```
        Name:              PO Companion
        Organization:      Your organization
        Expiration:        Custom defined (max 1 year)
        Scopes:            Custom defined
        ├─ Work Items:     Read
        ├─ Code:           Read (for PR insights)
        └─ Build:          Read (for pipeline insights)
        ```
     4. Click "Create"
     5. **IMPORTANT:** Copy token immediately - you won't see it again!
     6. Paste into PO Companion

4. **Test Connection**
   ```
   [Test Connection] button
   ```
   - Click "Test Connection" button
   - Application attempts to connect to Azure DevOps
   - Validates:
     - URL is reachable
     - Project exists
     - PAT has correct permissions
   - Results shown:
     ```
     ✓ Connection successful
     ✓ Project found: MainProject
     ✓ Access verified: Work Items (Read)
     ⚠ Optional: Code (Read) - PR Insights disabled
     ⚠ Optional: Build (Read) - Pipeline Insights disabled
     ```
   - If errors: detailed message shows what's wrong

5. **Select Team** (After successful connection)
   ```
   Team:  [Select team ▼]
          - Team Alpha
          - Team Beta
          - Team Gamma
   ```
   - Dropdown populates with available teams
   - Select primary team for this configuration
   - Can change later if needed

6. **Save Configuration**
   ```
   [💾 Save Configuration]  [🔄 Reset]
   ```
   - Click "Save Configuration"
   - Settings stored securely (PAT encrypted)
   - Success message: "Configuration saved successfully"
   - Page shows connection status: "Connected ✓"

7. **Advanced Settings** (Optional - Click "Advanced ▼")
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Advanced Settings                                       │
   ├─────────────────────────────────────────────────────────┤
   │ Proxy Server:  [                                      ] │
   │ Timeout (sec): [30                                    ] │
   │ Retry Attempts: [3                                    ] │
   │ API Version:   [7.1-preview ▼]                         │
   └─────────────────────────────────────────────────────────┘
   ```
   - Usually not needed
   - Proxy: If your network requires proxy (format: `http://proxy:port`)
   - Timeout: Increase if connection is slow
   - Retry: How many times to retry failed requests

**Tips:**
- Save your PAT securely (password manager) for reference
- Set PAT expiration reminder in calendar
- Test connection after changing any settings
- If connection fails: check URL spelling and project name case
- PAT stored encrypted locally - safe to save
- Use minimal required scopes for security (follow principle of least privilege)

**Troubleshooting:**
- "Invalid URL": Check format, no trailing slash
- "Project not found": Verify exact project name, check case
- "Unauthorized": PAT expired or wrong scopes
- "Network error": Check firewall, proxy settings

---

## Work Items Explorer

### Use Case: Hierarchical Work Item Management

**What It Is:**  
The Work Items Explorer is your main workspace for viewing, managing, and validating work items. It displays your backlog in a hierarchical tree structure (Goal → Objective → Epic → Feature → PBI → Task) with powerful filtering, validation, and bulk operation capabilities.

**Why It Matters:**  
As a Product Owner, you need to see the big picture (strategic goals) and the details (individual tasks) simultaneously. The tree view lets you understand dependencies, spot validation issues, track progress at every level, and maintain backlog quality. It's your single source of truth for all work items.

**Sub-Features:**
- Hierarchical tree grid view
- Multi-level filtering (product, team, iteration, state)
- Real-time validation with visual indicators
- Column picker for customization
- Work item detail panel
- Bulk operations (multi-select)
- Search and text highlighting
- Validation history panel
- Work item history timeline
- Quick actions (edit, add child, delete)
- Export to Excel
- Sync from Azure DevOps

### Day-to-Day Usage Scenario

**Scenario: Sprint Planning Preparation**

Sarah prepares for Product A's sprint planning tomorrow:

1. **Filter to Current Sprint Context**
   - Opens Work Items page
   - Selects Profile: "Product A - Mobile App"
   - Filters:
     - Iteration: "Sprint 15"
     - State: "New, Approved, Committed"
     - Area: "Team Alpha"

2. **Review Epic Progress**
   - Expands "Epic: User Authentication"
   - Sees 3 features:
     - "Social Login" - 5/8 PBIs complete
     - "Two-Factor Auth" - 0/6 PBIs (not started)
     - "Password Reset" - 6/6 PBIs complete ✓
   - Notices validation warning on "Two-Factor Auth"

3. **Investigate Validation Issue**
   - Clicks validation icon next to "Two-Factor Auth" feature
   - Validation panel shows:
     - ⚠ Missing effort estimates on 3 PBIs
     - ⚠ PBI "SMS Code Delivery" missing acceptance criteria
     - ✓ All work items properly linked
   - Makes note to discuss with team

4. **Prepare for Planning Meeting**
   - Uses column picker to add "Effort", "Priority", "Tags" columns
   - Sorts by priority
   - Exports to Excel for presentation
   - Takes screenshot of epic tree for facilitation

5. **Mid-Sprint: Track Progress**
   - Changes filter to "In Progress" state
   - Sees 8 PBIs currently being worked
   - Notices 2 PBIs stuck "In Progress" for 5 days
   - Opens State Timeline for those PBIs to analyze bottleneck

### How To Use Work Items Explorer

**Navigation:**
- **Page Location:** `/workitems`
- **Menu:** Click "Work Items" in main navigation
- **Keyboard Shortcut:** `Ctrl+W`

**Step-by-Step:**

1. **Open Work Items Page**
   ```
   Main Navigation → Work Items
   ```
   - Tree grid view loads with all work items
   - Default view: All active work items for selected profile

2. **Understanding the Tree View**
   ```
   ├─ [Goal] Strategic Goals 2024 (3 Objectives)
   │  ├─ [Objective] Improve User Experience (2 Epics)
   │  │  ├─ [Epic] User Authentication (12 Features) ⚠
   │  │  │  ├─ [Feature] Social Login (8 PBIs)
   │  │  │  │  ├─ [PBI] Google OAuth Integration (Effort: 5) ✓
   │  │  │  │  │  ├─ [Task] Setup OAuth credentials (2h) ✓
   │  │  │  │  │  └─ [Task] Implement redirect flow (3h) ✓
   │  │  │  │  └─ [PBI] Facebook Login (Effort: 3) →
   ```
   
   **Visual Indicators:**
   - `├─` / `└─` : Tree structure lines
   - `[Type]` : Work item type badge (color-coded)
   - `⚠` : Validation warning (yellow)
   - `❌` : Validation error (red)
   - `✓` : Complete/Done (green)
   - `→` : In Progress (blue)
   - `(Number)` : Count of children
   - `Effort: N` : Effort estimate

3. **Expand/Collapse Tree Nodes**
   - Click `▶` arrow to expand node (shows children)
   - Click `▼` arrow to collapse node (hides children)
   - Double-click work item name to expand/collapse
   - Keyboard:
     - `→` (Right arrow) : Expand selected node
     - `←` (Left arrow) : Collapse selected node
     - `*` : Expand all children recursively
   - Buttons:
     - "Expand All" (top toolbar) : Expands entire tree
     - "Collapse All" (top toolbar) : Collapses to top level

4. **Filter Work Items**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Filters:                                                │
   │ Product: [Product A ▼] Team: [Team Alpha ▼]           │
   │ Iteration: [Sprint 15 ▼] State: [All ▼]               │
   │ Search: [🔍 Search work items...                     ] │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Filter Options:**
   - **Product:** Select profile (uses area paths)
   - **Team:** Filter by team assignment
   - **Iteration:** Sprint/iteration path
     - "Current" - current sprint
     - "Next" - upcoming sprint
     - Specific sprint from dropdown
   - **State:** Work item state
     - "All" - show all states
     - "Active" - New, Approved, Committed, In Progress
     - "Completed" - Done, Closed
     - Specific state
   - **Search:** Free text search
     - Searches: Title, Description, Tags
     - Updates as you type
     - Matched text highlighted in tree

   **Applying Filters:**
   - Select from dropdowns
   - Changes apply immediately
   - Tree refreshes automatically
   - Filters combine (AND logic)
   - Clear all: Click "Reset Filters" button

5. **Customize Columns (Column Picker)**
   ```
   Click "Columns" button (top right toolbar)
   
   ┌─────────────────────────────────────────────────────────┐
   │ Select Columns                              [×]         │
   ├─────────────────────────────────────────────────────────┤
   │ ☑ Title                    ☑ Effort                    │
   │ ☑ State                    ☐ Priority                  │
   │ ☑ Assigned To              ☐ Tags                      │
   │ ☐ Iteration Path           ☐ Created Date              │
   │ ☑ Validation Status        ☐ Modified Date             │
   │ ☐ Parent                   ☐ Area Path                 │
   │                                                         │
   │ [Select All] [Deselect All] [Apply] [Cancel]          │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Steps:**
   - Click "Columns" button
   - Check/uncheck columns you want visible
   - Click "Apply"
   - Columns update immediately
   - Drag column headers to reorder
   - Click column header to sort

6. **View Work Item Details**
   ```
   Click any work item → Detail panel opens (right side)
   
   ┌─────────────────────────────────────────────────────────┐
   │ [PBI] Google OAuth Integration              [×]         │
   ├─────────────────────────────────────────────────────────┤
   │ ID: 12345                                               │
   │ State: Done                                             │
   │ Assigned To: John Smith                                 │
   │ Effort: 5 points                                        │
   │ Iteration: Sprint 15                                    │
   │                                                         │
   │ Description:                                            │
   │ Implement Google OAuth 2.0 integration for user        │
   │ authentication. Users should be able to sign in        │
   │ with their Google account.                             │
   │                                                         │
   │ Acceptance Criteria:                                    │
   │ - User can click "Sign in with Google"                 │
   │ - OAuth flow redirects to Google                        │
   │ - Successful auth creates user session                  │
   │                                                         │
   │ [Edit] [Add Child] [View in Azure DevOps]             │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Tabs in Detail Panel:**
   - **Details** : Core fields (shown above)
   - **History** : State changes, edits (timeline view)
   - **Validation** : Validation issues and fixes
   - **Links** : Parent, children, related work items
   - **Attachments** : Files attached to work item

7. **Check Validation Status**
   - Look for validation icons next to work items:
     - ✓ (green check) : No issues
     - ⚠ (yellow warning) : Minor issues
     - ❌ (red X) : Critical issues
   
   - Click validation icon → Validation panel expands
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Validation Issues                                       │
   ├─────────────────────────────────────────────────────────┤
   │ ⚠ Missing Effort Estimate                              │
   │   This PBI does not have an effort estimate.           │
   │   Add effort value for accurate velocity tracking.     │
   │   [Fix: Add Effort]                                    │
   │                                                         │
   │ ⚠ Missing Acceptance Criteria                          │
   │   No acceptance criteria defined.                      │
   │   Add criteria to clarify definition of done.          │
   │   [Edit in Azure DevOps]                               │
   │                                                         │
   │ ❌ Orphaned Work Item                                   │
   │   This PBI has no parent Feature.                      │
   │   [Link to Parent]                                     │
   └─────────────────────────────────────────────────────────┘
   ```
   
   - Click "Fix" buttons for guided resolution
   - Fixes sync back to Azure DevOps

8. **View Validation History**
   - Click work item → Detail panel → "Validation" tab
   ```
   Timeline shows:
   Jan 15, 2026 10:30 AM - Issue Detected
   ⚠ Missing effort estimate
   
   Jan 15, 2026 11:00 AM - Fixed
   ✓ Effort added: 5 points (by John Smith)
   
   Jan 16, 2026 09:15 AM - Issue Detected
   ⚠ Missing acceptance criteria
   
   Jan 16, 2026 02:30 PM - Fixed
   ✓ Acceptance criteria added (by Sarah Lee)
   ```

9. **Bulk Operations (Multi-Select)**
   - Hold `Ctrl` and click multiple work items
   - Or: Hold `Shift` and click to select range
   - Selected items highlight in blue
   - Bulk action toolbar appears:
   ```
   [3 items selected]  [Assign To ▼] [Change State ▼] [Add Tag ▼] [Export] [Clear]
   ```
   
   **Available Bulk Actions:**
   - Assign To : Assign selected items to team member
   - Change State : Move to different state
   - Add Tag : Add tag to all selected
   - Export : Export selection to Excel
   - Clear : Deselect all

10. **Sync from Azure DevOps**
    ```
    [🔄 Sync] button (top right)
    ```
    - Click "Sync" button
    - Modal shows sync progress:
      ```
      Syncing from Azure DevOps...
      ├─ Fetching work items... (50/250) 20%
      ├─ Processing hierarchies... ⏳
      └─ Updating validations... ⏳
      
      [Cancel]
      ```
    - Sync runs in background
    - Tree updates automatically when complete
    - Notification: "Sync complete: 45 items updated"

11. **Export to Excel**
    ```
    [📊 Export] button (top right)
    ```
    - Click "Export" button
    - Options dialog:
      ```
      Export:  ○ Current View
               ● Filtered Items
               ○ Selected Items Only
               ○ Entire Tree
      
      Include: ☑ Hierarchy (indentation)
               ☑ Validation Status
               ☑ All Visible Columns
      
      [Export to Excel] [Cancel]
      ```
    - Excel file downloads
    - Opens in default spreadsheet application

12. **Keyboard Shortcuts**
    - `↑` / `↓` : Navigate up/down tree
    - `→` : Expand node
    - `←` : Collapse node
    - `Enter` : Open detail panel
    - `Esc` : Close detail panel
    - `Ctrl+F` : Focus search box
    - `Ctrl+A` : Select all visible items
    - `Delete` : Delete selected (confirmation required)
    - `Ctrl+R` : Refresh/sync

**Tips:**
- Expand epics before sprint planning to see all features/PBIs
- Use search to quickly find specific work items
- Fix validation issues as you find them (don't let them accumulate)
- Export before major planning sessions for offline reference
- Sync at start of day to ensure latest data
- Use bulk operations to update multiple items efficiently
- Pin frequently used column configurations
- Learn keyboard shortcuts to navigate faster

**Common Workflows:**

**Workflow 1: Sprint Health Check**
1. Filter to current sprint
2. Expand all epics
3. Look for validation warnings
4. Check "In Progress" items for staleness
5. Export summary for standup

**Workflow 2: Backlog Grooming**
1. Filter to "New" and "Approved" states
2. Review epics missing effort estimates
3. Use validation panel to find incomplete items
4. Bulk assign items to team for estimation
5. Mark items as "Approved" after review

**Workflow 3: Release Planning**
1. Clear all filters (show all)
2. Expand goals and objectives
3. Export entire tree to Excel
4. Use Excel for capacity modeling
5. Update priorities in PO Companion
6. Sync changes to Azure DevOps

---

## Backlog Health

### Use Case: Data Quality Monitoring

**What It Is:**  
Backlog Health is your data quality dashboard. It monitors your backlog across iterations, identifying validation issues that could impact planning accuracy. It tracks trends over time, helping you maintain a healthy, well-defined backlog that supports reliable forecasting and sprint planning.

**Why It Matters:**  
Garbage in, garbage out. If your work items lack effort estimates, have broken parent-child links, or miss acceptance criteria, your velocity metrics become meaningless and planning becomes guesswork. Backlog Health helps you proactively find and fix these issues before they cause problems in sprint planning.

**Sub-Features:**
- Health score calculation (% of items without validation issues)
- Trend visualization (health over time)
- Issue breakdown by type
- Iteration-by-iteration health table
- Drill-down to specific problematic work items
- Validation rule configuration
- Exportable health reports
- Team comparison views

### Day-to-Day Usage Scenario

**Scenario: Weekly Backlog Maintenance**

Every Wednesday, Sarah reviews backlog health for both products:

1. **Product A Health Check**
   - Opens Backlog Health page
   - Selects "Product A - Mobile App" profile
   - Current health: 87% (down from 92% last week)
   - Trend chart shows decline over past 3 sprints

2. **Identify Problem Areas**
   - Issue breakdown shows:
     - 15 PBIs missing effort estimates (most common issue)
     - 8 PBIs with no acceptance criteria
     - 3 orphaned PBIs (no parent feature)
     - 2 tasks with no assigned team member
   - Total: 28 issues affecting 23 work items

3. **Drill into Specific Issues**
   - Clicks "15 PBIs missing effort" 
   - List shows which PBIs need estimates
   - Notices they're all in "Two-Factor Auth" feature (new work)
   - Makes note: Schedule estimation session with team

4. **Compare Iterations**
   - Iteration health table shows:
     - Sprint 13: 94% (excellent)
     - Sprint 14: 89% (good)
     - Sprint 15: 87% (declining - needs attention)
   - Pattern: Health drops when new work added, improves after grooming

5. **Set Weekly Goal**
   - Target: Get back to 95% health
   - Plan: 
     - Monday: Team estimation for Two-Factor Auth PBIs
     - Tuesday: Add acceptance criteria to 8 PBIs
     - Wednesday: Link orphaned PBIs to correct features

6. **Product B Quick Check**
   - Switches to Product B profile
   - Health: 94% (stable, excellent)
   - Only 6 minor issues
   - No action needed this week

### How To Use Backlog Health

**Navigation:**
- **Page Location:** `/backlog-health`
- **Menu:** Click "Backlog Health" under "Metrics" section
- **Keyboard Shortcut:** `Ctrl+B`

**Step-by-Step:**

1. **Open Backlog Health Page**
   ```
   Navigation → Metrics → Backlog Health
   ```
   - Dashboard loads with health overview

2. **Understand Health Score**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │              BACKLOG HEALTH SCORE                       │
   │                                                         │
   │                    87%                                  │
   │                  🟡 GOOD                                │
   │                                                         │
   │  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
   │  0%                                              100%   │
   │                                                         │
   │  Trend: ↓ Down 5% from last week                       │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Score Interpretation:**
   - **95-100%** : 🟢 Excellent - Backlog is very healthy
   - **85-94%** : 🟡 Good - Minor issues, manageable
   - **70-84%** : 🟠 Fair - Needs attention soon
   - **Below 70%** : 🔴 Poor - Immediate action required
   
   **Calculation:**
   - Health % = (Work items without issues / Total work items) × 100
   - Only counts active work items (excludes Done/Closed)
   - Weighted by work item type (PBIs count more than tasks)

3. **View Trend Chart**
   ```
   Health Trend - Last 12 Weeks
   
   100%├─────────────────────────────────────────────
       │     ●────●
    95%│   ●         ●
       │  ●            ●───●
    90%│                    ●
       │                      ●
    85%│                        ●───●
       │                              ●────●
    80%├─────────────────────────────────────────────
       Jan  Feb  Mar  Apr  May  Jun  Jul  Aug  Sep
   ```
   
   - Hover over points to see exact scores
   - Click point to see issues at that time
   - Identify patterns (e.g., drops after new work added)

4. **Review Issue Breakdown**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Issues by Type                                          │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ ⚠ Missing Effort Estimate           15 items  ████████ │
   │ ⚠ No Acceptance Criteria             8 items  ████     │
   │ ❌ Orphaned Work Item                 3 items  ██       │
   │ ⚠ No Assigned Team                   2 items  █        │
   │                                                         │
   │ Total Issues: 28 affecting 23 unique work items        │
   └─────────────────────────────────────────────────────────┘
   ```
   
   - Bar chart shows relative frequency
   - Icons indicate severity (⚠ warning, ❌ error)
   - Click bar to drill into specific issue type

5. **Drill Down to Problematic Items**
   ```
   Click "15 items Missing Effort" →
   
   ┌─────────────────────────────────────────────────────────┐
   │ Work Items Missing Effort Estimate                     │
   ├─────────────────────────────────────────────────────────┤
   │ ID     Title                           Type    Sprint   │
   │─────────────────────────────────────────────────────────│
   │ 12567  Implement SMS code delivery     PBI     Sprint15│
   │ 12568  Add phone number validation     PBI     Sprint15│
   │ 12569  Create verification UI          PBI     Sprint15│
   │ ...                                                     │
   │                                                         │
   │ [Export List] [Fix All] [Close]                        │
   └─────────────────────────────────────────────────────────┘
   ```
   
   - Shows all work items with that issue
   - Click work item ID to open detail panel
   - "Export List" sends to Excel for offline work
   - "Fix All" opens bulk fix wizard (if available)

6. **View Iteration Health Table**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Health by Iteration                                     │
   ├─────────────────────────────────────────────────────────┤
   │ Iteration  │ Health │ Total Items │ Issues │ Trend     │
   │────────────┼────────┼─────────────┼────────┼───────────│
   │ Sprint 15  │  87%🟡 │     156     │   28   │    ↓      │
   │ Sprint 14  │  89%🟡 │     148     │   19   │    ↑      │
   │ Sprint 13  │  94%🟢 │     142     │   12   │    →      │
   │ Sprint 12  │  91%🟡 │     139     │   15   │    ↑      │
   └─────────────────────────────────────────────────────────┘
   ```
   
   - Click iteration row to filter issues to that iteration
   - Sort by column (click header)
   - Export table for reporting

7. **Filter Health Data**
   ```
   Filters:  Team: [All ▼] Type: [All ▼] Severity: [All ▼]
   ```
   
   - **Team:** Focus on specific team's backlog
   - **Type:** Filter by work item type (Epic, Feature, PBI, Task)
   - **Severity:** Show only errors (❌) or warnings (⚠)
   - Filters apply to all views on page

8. **Export Health Report**
   ```
   [📊 Export Report] button (top right)
   
   Options:
   - Summary (one page overview)
   - Detailed (includes all issues)
   - Executive (high-level metrics)
   - Team Report (by team breakdown)
   
   Format: PDF or Excel
   ```

9. **Configure Validation Rules** (Admin)
   ```
   [⚙ Configure Rules] button
   
   Opens dialog showing validation rules:
   ☑ PBIs must have effort estimate
   ☑ PBIs must have acceptance criteria
   ☑ All items must have parent (except Goals)
   ☑ Tasks must be assigned
   ☐ Features must have tags (optional rule)
   
   [Save] [Reset to Defaults]
   ```
   
   - Check/uncheck rules as needed
   - Changes affect health score calculation
   - Some rules are required (can't uncheck)

**Tips:**
- Check Backlog Health every Monday before sprint planning
- Set team goal to maintain >90% health
- Address validation issues during backlog refinement
- Use health trends to identify when new work needs grooming
- Export reports for sprint retrospectives
- Don't obsess over 100% - 95% is excellent
- Focus on errors (❌) before warnings (⚠)

**Health Maintenance Workflow:**

**Weekly Routine:**
1. Monday: Check health score
2. Identify top 3 issue types
3. Assign issues to team members to fix
4. Wednesday: Recheck progress
5. Friday: Verify health improved

**Sprint Boundary:**
- Day before sprint planning: Health should be >90%
- If below 90%: Schedule grooming session
- Don't start sprint planning with unhealthy backlog

---

## Effort Distribution

### Use Case: Capacity Planning Heat Map

**What It Is:**  
Effort Distribution is a visual heat map showing how work effort is distributed across your teams (area paths) and time (iterations). It helps you identify over-allocated or under-utilized teams, spot capacity issues before they become problems, and ensure sustainable work distribution.

**Why It Matters:**  
Product Owners need to balance work across teams to prevent burnout and ensure consistent delivery. This heat map makes capacity problems immediately visible. Teams at 100%+ capacity will likely miss commitments; teams at 50% may not be challenged enough. The sweet spot is 75-85% capacity utilization.

**Sub-Features:**
- Heat map visualization (area paths × iterations)
- Capacity vs. effort comparison
- Over/under-allocation highlighting
- Drill-down to specific cells
- Team capacity configuration
- Iteration planning view
- Load balancing recommendations
- Historical utilization tracking

### Day-to-Day Usage Scenario

**Scenario: Quarterly Capacity Planning**

Sarah plans Q2 work distribution for both products:

1. **View Overall Distribution**
   - Opens Effort Distribution page
   - Heat map shows:
     ```
     Team/Sprint  │ S14  │ S15  │ S16  │ S17  │ S18  │
     ─────────────┼──────┼──────┼──────┼──────┼──────┤
     Team Alpha   │ 🟢42 │ 🔴56 │ 🟡48 │ 🟢40 │ 🟢38 │
     Team Beta    │ 🟢35 │ 🟢38 │ 🟢42 │ 🟡52 │ 🟢45 │
     ```
     Key: 🟢 Good (75-85%), 🟡 High (86-95%), 🔴 Over (>95%)

2. **Identify Problem: Team Alpha Sprint 15**
   - Team Alpha Sprint 15 shows red (56 points planned)
   - Team capacity: 45 points
   - 124% utilization - over-allocated by 11 points
   - Risk: Team will likely miss commitment

3. **Drill Down to Details**
   - Clicks red cell (Team Alpha, S15)
   - Shows work items:
     - Epic: Two-Factor Auth (18 pts)
     - Epic: Payment Integration (22 pts)
     - Epic: Profile Management (16 pts)
     - Total: 56 pts planned, 45 pts capacity

4. **Rebalance Load**
   - Options:
     1. Move "Profile Management" (16 pts) to Sprint 16
     2. Split "Payment Integration" across S15 and S16
     3. Request Team Beta help with 11 points of work
   - Decision: Move Profile Management to S16
   - Updated map now shows:
     - S15: 40 pts (89% - acceptable)
     - S16: 64 pts (142% - now over!)

5. **Iterate to Balance**
   - Further adjusts S16 workload
   - Final distribution:
     - S15: 40 pts (89%) ✓
     - S16: 48 pts (107%) - slightly over but manageable
     - S17: 40 pts (89%) ✓

6. **Compare Product B**
   - Team Beta shows balanced load
   - All sprints 80-90% capacity
   - No action needed

### How To Use Effort Distribution

**Navigation:**
- **Page Location:** `/effort-distribution`
- **Menu:** Click "Effort Distribution" under "Metrics"
- **Keyboard Shortcut:** `Ctrl+E`

**Step-by-Step:**

1. **Open Effort Distribution Page**
   ```
   Navigation → Metrics → Effort Distribution
   ```
   - Heat map loads showing effort distribution

2. **Understand the Heat Map**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │         Effort Distribution Heat Map                    │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Area Path    │ Sprint 14│ Sprint 15│ Sprint 16│Sprint17│
   │──────────────┼──────────┼──────────┼──────────┼────────│
   │ Mobile\Alpha │  42/45   │  56/45   │  48/45   │ 40/45 │
   │              │  93% 🟢  │ 124% 🔴  │ 107% 🟡  │  89%🟢│
   │──────────────┼──────────┼──────────┼──────────┼────────│
   │ Web\Beta     │  35/42   │  38/42   │  42/42   │ 52/42 │
   │              │  83% 🟢  │  90% 🟢  │ 100% 🟡  │ 124%🔴│
   │──────────────┼──────────┼──────────┼──────────┼────────│
   │ Shared\Ops   │  12/15   │  14/15   │  10/15   │  8/15 │
   │              │  80% 🟢  │  93% 🟢  │  67% 🟢  │  53%🟢│
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Reading the Heat Map:**
   - Each cell shows: `Effort / Capacity`
   - Percentage: Utilization rate
   - Color coding:
     - 🟢 Green (75-85%): Optimal utilization
     - �� Yellow (86-100%): High but manageable
     - 🔴 Red (>100%): Over-allocated (risk)
     - ⚪ Gray (<75%): Under-utilized
   
3. **Navigate Time Periods**
   ```
   View:  ○ This Quarter   ● Next Quarter   ○ Custom Range
   
   [◀ Previous] [Today] [Next ▶]
   ```
   - Select quarter or custom date range
   - Use arrows to move forward/backward
   - "Today" centers on current sprint

4. **Configure Team Capacity**
   ```
   Click team name → Capacity dialog opens
   
   ┌─────────────────────────────────────────────────────────┐
   │ Team Alpha Capacity                         [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Default Capacity per Sprint: [45] points               │
   │                                                         │
   │ Sprint-specific overrides:                              │
   │                                                         │
   │ Sprint 15:  [40] (Holiday week)                        │
   │ Sprint 16:  [50] (Extra capacity)                      │
   │                                                         │
   │ Team Size: [5] members                                 │
   │ Points per Person: [9] per sprint                      │
   │                                                         │
   │ [Save] [Cancel]                                        │
   └─────────────────────────────────────────────────────────┘
   ```
   - Set default capacity for team
   - Add sprint-specific adjustments (holidays, etc.)
   - Capacity automatically calculates utilization %

5. **Drill Down to Work Items**
   ```
   Click any cell → Detail panel shows work items
   
   ┌─────────────────────────────────────────────────────────┐
   │ Team Alpha - Sprint 15                      [×]         │
   ├─────────────────────────────────────────────────────────┤
   │ Planned: 56 points                                      │
   │ Capacity: 45 points                                     │
   │ Utilization: 124% 🔴                                    │
   │                                                         │
   │ Work Items:                                             │
   │ ┌─────────────────────────────────────────────────┐   │
   │ │ [Epic] Two-Factor Auth              18 pts      │   │
   │ │  ├─ [PBI] SMS Integration           5 pts       │   │
   │ │  ├─ [PBI] Token Generation          8 pts       │   │
   │ │  └─ [PBI] UI Components             5 pts       │   │
   │ │                                                  │   │
   │ │ [Epic] Payment Integration          22 pts      │   │
   │ │  ├─ [PBI] Stripe API                13 pts      │   │
   │ │  └─ [PBI] Payment UI                9 pts       │   │
   │ │                                                  │   │
   │ │ [Epic] Profile Management           16 pts      │   │
   │ │  ├─ [PBI] Edit Profile              8 pts       │   │
   │ │  └─ [PBI] Avatar Upload             8 pts       │   │
   │ └─────────────────────────────────────────────────┘   │
   │                                                         │
   │ ⚠ Team is over-allocated by 11 points                  │
   │                                                         │
   │ Suggestions:                                            │
   │ • Move lowest priority work to next sprint             │
   │ • Request help from Team Beta                           │
   │ • Re-estimate work items if possible                    │
   │                                                         │
   │ [Move Work Items] [Export] [Close]                     │
   └─────────────────────────────────────────────────────────┘
   ```

6. **Move Work Items Between Sprints**
   ```
   From detail panel → Click "Move Work Items"
   
   ┌─────────────────────────────────────────────────────────┐
   │ Move Work Items                             [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ From: Team Alpha, Sprint 15                            │
   │ To:   [Sprint 16 ▼]                                    │
   │                                                         │
   │ Select items to move:                                   │
   │ ☐ [Epic] Two-Factor Auth (18 pts)                     │
   │ ☐ [Epic] Payment Integration (22 pts)                 │
   │ ☑ [Epic] Profile Management (16 pts)                  │
   │                                                         │
   │ Impact:                                                 │
   │ Sprint 15: 56 → 40 pts (124% → 89%) 🟢                │
   │ Sprint 16: 48 → 64 pts (107% → 142%) 🔴               │
   │                                                         │
   │ ⚠ Warning: Sprint 16 will be over-allocated            │
   │                                                         │
   │ [Move Items] [Cancel]                                  │
   └─────────────────────────────────────────────────────────┘
   ```
   - Select work items to move
   - Choose target sprint
   - See impact before confirming
   - System warns if target becomes over-allocated

7. **View Utilization Trends**
   ```
   Click [📈 Trends] button
   
   Shows line chart:
   
   Team Utilization Over Time
   
   120%├─────────────────────────────────────────────
       │              Team Alpha
   100%│   ─────●────●─────●────────  Capacity Line
       │       ╱      ╲     ╲
    80%│    ●           ●    ●────●
       │   ╱                      
    60%│  ●           Team Beta
       │  ●────●─────●────●─────●────●
    40%├─────────────────────────────────────────────
       S12  S13  S14  S15  S16  S17  S18
   ```
   - Visualize capacity over time
   - Compare multiple teams
   - Identify utilization patterns

8. **Export for Planning**
   ```
   [📊 Export] button → Choose format
   
   - Excel: Full data with formulas
   - PDF: Heat map report
   - CSV: Raw data for analysis
   ```

9. **Balance Multiple Products**
   ```
   Product Filter: [All Products ▼]
   
   - Select "All Products" to see complete picture
   - Or filter to single product for focused view
   - Cross-product dependencies visible
   ```

**Tips:**
- Target 75-85% capacity utilization (sustainable pace)
- Update capacity for holidays, vacations, training
- Review heat map during quarterly planning
- Over 100%? Move work, don't add capacity
- Under 75%? Pull work forward or add scope
- Consider team dependencies when balancing
- Buffer 10-15% for unplanned work, bugs
- Use historical data to calibrate estimates

**Planning Workflow:**

**Quarterly Planning:**
1. Set team capacities for next 6 sprints
2. Rough-assign epics to sprints
3. Review heat map for red/yellow cells
4. Rebalance to target 75-85% utilization
5. Validate dependencies don't block
6. Export plan for stakeholder review
7. Refine sprint-by-sprint during planning meetings

**Sprint Planning:**
1. Check current sprint capacity
2. Confirm utilization is 75-85%
3. If over: move lowest priority items out
4. If under: pull additional ready items in
5. Final check before sprint starts

---

