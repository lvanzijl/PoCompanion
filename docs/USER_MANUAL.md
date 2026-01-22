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

## Velocity Dashboard

### Use Case: Team Performance and Sprint Planning

**What It Is:**  
The Velocity Dashboard tracks your team's completed story points per sprint. It shows historical velocity trends, calculates averages, and provides forecasting data to help you plan future sprints realistically based on actual team performance.

**Why It Matters:**  
Velocity is the foundation of data-driven sprint planning. By tracking how much work your team actually completes sprint-over-sprint, you can make reliable commitments instead of guesses. It helps you predict when features will complete, identify capacity issues, and set realistic expectations with stakeholders.

**Sub-Features:**
- Sprint-by-sprint velocity chart
- Rolling averages (3-sprint, 6-sprint)
- Velocity trends (increasing, stable, decreasing)
- Capacity vs. actual comparison
- Team velocity comparison
- Forecasting based on velocity
- Anomaly detection (outlier sprints)
- Export for reporting

### Day-to-Day Usage Scenario

**Scenario: Sprint Planning for Both Teams**

Sarah prepares for sprint planning sessions:

1. **Review Team Alpha Velocity (Product A)**
   - Opens Velocity Dashboard
   - Selects "Team Alpha" profile
   - Last 6 sprints velocity:
     - S10: 38 pts, S11: 42 pts, S12: 40 pts
     - S13: 45 pts, S14: 38 pts, S15: 41 pts
   - 3-sprint average: 41 points
   - Trend: Stable (good predictability)

2. **Plan Sprint 16 for Team Alpha**
   - 3-sprint avg: 41 pts (most reliable)
   - 6-sprint avg: 41 pts (confirms consistency)
   - Planning decision: Commit to 40 pts (10% buffer)
   - Rationale: One team member on vacation next sprint

3. **Review Team Beta Velocity (Product B)**
   - Switches to "Team Beta" profile
   - Last 6 sprints:
     - S10: 50 pts, S11: 52 pts, S12: 28 pts (outlier!)
     - S13: 51 pts, S14: 49 pts, S15: 50 pts
   - 3-sprint average: 50 points
   - Anomaly detected: Sprint 12 (28 pts)

4. **Investigate Anomaly**
   - Clicks Sprint 12 bar
   - Detail shows: Major production issue consumed 3 days
   - Note: Team handled unplanned work, not a velocity problem
   - Excludes S12 from planning calculations

5. **Plan Sprint 16 for Team Beta**
   - Adjusted 3-sprint avg (excluding S12): 50 pts
   - No vacation or holidays
   - Planning decision: Commit to 48 pts
   - Pull 2 pts from backlog if team finishes early

6. **Forecast Epic Completion**
   - "Two-Factor Auth Epic" = 48 pts remaining
   - Team Alpha velocity: 41 pts/sprint
   - Forecast: 2 sprints to complete (S16-S17)
   - Communicates to stakeholders: End of S17

### How To Use Velocity Dashboard

**Navigation:**
- **Page Location:** `/velocity`
- **Menu:** Click "Velocity Dashboard" under "Metrics"
- **Keyboard Shortcut:** `Ctrl+V`

**Step-by-Step:**

1. **Open Velocity Dashboard**
   ```
   Navigation → Metrics → Velocity Dashboard
   ```
   - Chart loads showing sprint velocity history

2. **Understand the Velocity Chart**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │         Team Velocity - Last 10 Sprints                 │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ 60├──────────────────────────────────────────────────   │
   │   │                                                     │
   │ 50│     ●═══●═══●       ●═══●═══●                      │
   │   │                                                     │
   │ 40│                 ●═══●                               │
   │   │                                                     │
   │ 30│                     ●                               │
   │   │                                                     │
   │ 20├──────────────────────────────────────────────────   │
   │   S10  S11  S12  S13  S14  S15  S16  S17  S18  S19    │
   │                                                         │
   │   ● Actual Velocity    ─── 3-Sprint Avg (41 pts)      │
   │   ═══ Target Capacity  ··· 6-Sprint Avg (43 pts)      │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Chart Elements:**
   - **Blue bars:** Actual completed points per sprint
   - **Solid line:** 3-sprint rolling average (most useful for planning)
   - **Dotted line:** 6-sprint rolling average (trend indicator)
   - **Orange line:** Team capacity (planned/target)
   - **Red marker:** Anomaly/outlier sprint

3. **Read Key Metrics**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Key Metrics                                             │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │  Last Sprint:      41 points                           │
   │  3-Sprint Avg:     41 points  →  (Stable)             │
   │  6-Sprint Avg:     43 points  ↓  (-5% trend)          │
   │  Std Deviation:    5.2 points                          │
   │  Predictability:   🟢 High (±12%)                      │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Metric Meanings:**
   - **Last Sprint:** Most recent completed sprint
   - **3-Sprint Avg:** Rolling average of last 3 sprints (use for planning)
   - **6-Sprint Avg:** Longer-term trend indicator
   - **Std Deviation:** Consistency (lower = more predictable)
   - **Predictability:** 
     - 🟢 High (<15% deviation): Very reliable
     - 🟡 Medium (15-25% deviation): Somewhat reliable
     - 🔴 Low (>25% deviation): Unreliable, needs investigation

4. **View Detailed Sprint Data**
   ```
   Click any sprint bar → Detail panel opens
   
   ┌─────────────────────────────────────────────────────────┐
   │ Sprint 15 Details                           [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Completed:    41 points                                │
   │ Committed:    45 points                                │
   │ Achievement:  91%                                       │
   │                                                         │
   │ Work Items Completed: 12                               │
   │  ├─ Epics:    0                                        │
   │  ├─ Features: 2                                        │
   │  ├─ PBIs:     8                                        │
   │  └─ Tasks:    N/A (not counted)                        │
   │                                                         │
   │ Completed Items:                                        │
   │  [PBI] Google OAuth (5 pts) ✓                         │
   │  [PBI] Facebook Login (3 pts) ✓                       │
   │  [PBI] Profile Edit (8 pts) ✓                         │
   │  ... (9 more)                                          │
   │                                                         │
   │ [View All Items] [Export Sprint Report]                │
   └─────────────────────────────────────────────────────────┘
   ```

5. **Filter by Team/Product**
   ```
   Filters:  Profile: [Team Alpha ▼]  Date Range: [Last 10 Sprints ▼]
   ```
   - **Profile:** Select team to view velocity
   - **Date Range:** 
     - Last 6 sprints (most useful)
     - Last 10 sprints (trend analysis)
     - Last quarter
     - Custom range

6. **Compare Multiple Teams**
   ```
   Click [📊 Compare Teams] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ Team Comparison                                         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ 60├──────────────────────────────────────────────────   │
   │   │          Team Beta                                  │
   │ 50│     ●═══●═══●═══●═══●                              │
   │   │                                                     │
   │ 40│ ●═══●═══●═══●═══●        Team Alpha                │
   │   │                                                     │
   │ 30├──────────────────────────────────────────────────   │
   │   S10  S11  S12  S13  S14  S15                         │
   │                                                         │
   │   ● Team Alpha (Avg: 41)   ● Team Beta (Avg: 50)      │
   └─────────────────────────────────────────────────────────┘
   
   Comparison Table:
   │ Team       │ Avg Velocity │ Trend │ Predictability │
   ├────────────┼──────────────┼───────┼────────────────┤
   │ Team Alpha │   41 pts     │   →   │   High 🟢      │
   │ Team Beta  │   50 pts     │   ↑   │   High 🟢      │
   ```
   
   **Important:** Don't compare teams to judge performance!
   - Different team sizes
   - Different story point scales
   - Use for capacity planning only

7. **Use Forecast Calculator**
   ```
   Click [🔮 Forecast] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ Velocity-Based Forecast                     [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Team: Team Alpha                                        │
   │ Current Velocity (3-sprint avg): 41 points             │
   │                                                         │
   │ How much work do you want to forecast?                 │
   │ Remaining Effort: [120] points                         │
   │                                                         │
   │ Forecast Results:                                       │
   │                                                         │
   │ ┌─────────────────────────────────────────────────┐   │
   │ │                                                  │   │
   │ │  Best Case:     2 sprints  (Sep 15, 2026)      │   │
   │ │  Likely:        3 sprints  (Sep 29, 2026)      │   │
   │ │  Worst Case:    4 sprints  (Oct 13, 2026)      │   │
   │ │                                                  │   │
   │ │  Based on:                                       │   │
   │ │  ├─ High velocity: 45 pts/sprint                │   │
   │ │  ├─ Average: 41 pts/sprint                      │   │
   │ │  └─ Low velocity: 35 pts/sprint                 │   │
   │ │                                                  │   │
   │ └─────────────────────────────────────────────────┘   │
   │                                                         │
   │ [Export Forecast] [Close]                              │
   └─────────────────────────────────────────────────────────┘
   ```

8. **Identify Anomalies**
   ```
   Sprints marked with 🔴 are anomalies
   ```
   - System detects outliers (>2 standard deviations from average)
   - Hover over red marker to see why flagged
   - Click to add notes explaining anomaly
   - Option to exclude from calculations

9. **Export Velocity Data**
   ```
   [📊 Export] button → Options:
   
   - Velocity Chart (image)
   - Sprint Data (Excel)
   - Planning Report (PDF)
   - Forecast Report
   ```

**Tips:**
- Use 3-sprint average for near-term planning
- Use 6-sprint average for long-term trends
- Don't plan at 100% of velocity - leave 10-15% buffer
- Investigate anomalies - don't ignore them
- Velocity should trend toward stable, not always increasing
- If velocity drops consistently, investigate (not necessarily bad)
- New team? Need 3+ sprints for reliable velocity
- Team changes? Recalibrate velocity expectations
- Story point scale changes? Reset velocity baseline

**Velocity Anti-Patterns to Avoid:**
- ❌ Comparing team velocities (different scales)
- ❌ Using velocity as performance metric (encourages gaming)
- ❌ Pushing team to increase velocity every sprint
- ❌ Planning at exactly 100% of velocity (no buffer)
- ❌ Ignoring anomalies instead of investigating
- ❌ Using velocity from first 1-2 sprints (not stable yet)

**Good Velocity Patterns:**
- ✓ Stable velocity over time (predictability)
- ✓ Low standard deviation (consistency)
- ✓ Planning at 80-90% of velocity (sustainable)
- ✓ Understanding and documenting anomalies
- ✓ Using velocity for team's own planning only

---

## Epic Forecast

### Use Case: Feature Completion Prediction

**What It Is:**  
Epic Forecast predicts when an Epic or Feature will complete based on team velocity and remaining work. It provides best-case, likely, and worst-case completion dates, helping you set realistic expectations with stakeholders and make informed trade-off decisions.

**Why It Matters:**  
Stakeholders constantly ask "When will feature X be done?" Epic Forecast gives you data-driven answers instead of guesses. It factors in team velocity variability, scope changes, and capacity to provide ranges that account for uncertainty. This helps you manage expectations and make go/no-go decisions on features.

**Sub-Features:**
- Epic/Feature selection
- Remaining work calculation
- Multi-scenario forecasting (best/likely/worst)
- Confidence intervals
- Velocity-based timeline
- Scope change impact analysis
- Dependency consideration
- Forecast accuracy tracking

### Day-to-Day Usage Scenario

**Scenario: Stakeholder Meeting - Feature Commitment**

Sarah needs to commit to feature delivery dates:

1. **Executive Asks: "When will Two-Factor Auth be ready?"**
   - Opens Epic Forecast page
   - Selects Epic: "Two-Factor Authentication"
   - System shows:
     - Total effort: 48 points
     - Completed: 12 points
     - Remaining: 36 points

2. **Review Team Velocity**
   - Team Alpha velocity: 41 pts/sprint (3-sprint avg)
   - Variability: ±5 points (low, good predictability)
   - Current sprint: 15 (Feature starts Sprint 16)

3. **View Forecast Results**
   - Best case: End of Sprint 16 (2 weeks)
     - If velocity = 46 pts (high)
     - Assumes no scope changes
   - Likely: End of Sprint 17 (4 weeks)
     - If velocity = 41 pts (average)
     - Most probable outcome: 75% confidence
   - Worst case: End of Sprint 18 (6 weeks)
     - If velocity = 36 pts (low)
     - If minor scope additions
     - Conservative estimate: 95% confidence

4. **Consider Dependencies**
   - Checks dependency graph
   - 2 PBIs depend on "User Profile API" (different epic)
   - User Profile API completes Sprint 16
   - No blocking dependencies after S16

5. **Make Commitment**
   - Tells executive: "Most likely end of Sprint 17 (4 weeks)"
   - Explains: "75% confident, assumes stable velocity"
   - Conservative: "Could slip to Sprint 18 if scope grows"
   - Documents commitment in forecast tool

6. **Track Forecast Accuracy**
   - After Sprint 16: 19 points completed (below avg)
   - Remaining: 17 points (was 36, now 17)
   - Forecast updates:
     - Likely: Still Sprint 17 (17 pts < 41 pt capacity)
     - High confidence: 90%
   - On track for commitment!

### How To Use Epic Forecast

**Navigation:**
- **Page Location:** `/epic-forecast` or `/epic-forecast/{epicId}`
- **Menu:** Click "Epic Forecast" under "Metrics"
- **Alternate:** From Work Items page → Right-click Epic → "Forecast Completion"

**Step-by-Step:**

1. **Open Epic Forecast Page**
   ```
   Navigation → Metrics → Epic Forecast
   ```
   - Landing page shows epic selector

2. **Select Epic or Feature**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Select Epic or Feature to Forecast                     │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Search: [🔍 Search epics...                          ] │
   │                                                         │
   │ Recent Forecasts:                                       │
   │  ● Two-Factor Authentication (36 pts remaining)        │
   │  ● Payment Integration (28 pts remaining)              │
   │  ● Admin Dashboard (52 pts remaining)                  │
   │                                                         │
   │ All Epics:                                              │
   │  [Epic] User Authentication (48 pts total)             │
   │  [Epic] Payment System (68 pts total)                  │
   │  [Epic] Reporting Suite (92 pts total)                 │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   - Click epic from list OR search by name
   - Can forecast any Epic or Feature level work item

3. **View Epic Overview**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Epic: Two-Factor Authentication                         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Assigned Team:  Team Alpha                             │
   │ State:          In Progress                            │
   │ Priority:       High                                   │
   │                                                         │
   │ Effort Summary:                                         │
   │  Total Effort:      48 points                          │
   │  Completed:         12 points  (25%) ▓▓▓░░░░░░░░      │
   │  In Progress:        8 points  (17%) ▓▓░░░░░░░░░░      │
   │  Remaining:         28 points  (58%) ▓▓▓▓▓▓░░░░░░      │
   │                                                         │
   │ Work Items:                                             │
   │  ├─ 3 Features                                         │
   │  ├─ 12 PBIs (3 done, 2 in progress, 7 not started)   │
   │  └─ 28 Tasks                                           │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

4. **Review Forecast Scenarios**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Forecast Results                                        │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │  📅 LIKELY COMPLETION                                  │
   │     September 29, 2026 (End of Sprint 17)              │
   │     Confidence: 75%                                    │
   │                                                         │
   │  Scenario Breakdown:                                    │
   │                                                         │
   │  🟢 Best Case                                          │
   │     September 15, 2026 (Sprint 16)                     │
   │     • High team velocity (46 pts/sprint)               │
   │     • No scope changes                                  │
   │     • No blocking issues                                │
   │     Confidence: 10%                                    │
   │                                                         │
   │  🟡 Likely Case                                        │
   │     September 29, 2026 (Sprint 17)                     │
   │     • Average velocity (41 pts/sprint)                 │
   │     • Minor scope adjustments expected                  │
   │     • Standard risk profile                             │
   │     Confidence: 75%                                    │
   │                                                         │
   │  🔴 Worst Case                                         │
   │     October 13, 2026 (Sprint 18)                       │
   │     • Low velocity (36 pts/sprint)                     │
   │     • Scope growth or technical issues                  │
   │     • Capacity constraints                              │
   │     Confidence: 95%                                    │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Understanding Confidence:**
   - **Best Case (10%):** Only if everything goes perfectly
   - **Likely (75%):** 3 out of 4 times, will finish by this date
   - **Worst Case (95%):** Almost certainly done by this date

5. **View Timeline Visualization**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Completion Timeline                                     │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Today        Sprint 16      Sprint 17      Sprint 18   │
   │   │              │              │              │        │
   │   ●──────────────●──────────────●──────────────●       │
   │   │              ▲              ▲              ▲        │
   │   │           Best Case      Likely        Worst Case  │
   │   │                                                     │
   │   └─ 12 pts done                                        │
   │        28 pts remaining                                 │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

6. **Analyze Velocity Assumptions**
   ```
   Click [📊 View Assumptions] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ Forecast Assumptions                        [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Team Velocity (Team Alpha):                            │
   │  3-Sprint Average:    41 points/sprint                 │
   │  Standard Deviation:  5 points                         │
   │  Range:               36-46 points                     │
   │                                                         │
   │ Velocity History:                                       │
   │  Sprint 13:  45 pts                                    │
   │  Sprint 14:  38 pts                                    │
   │  Sprint 15:  41 pts                                    │
   │                                                         │
   │ Assumptions:                                            │
   │  ✓ Team composition remains stable                     │
   │  ✓ No major holidays/vacations                         │
   │  ✓ Current sprint load sustainable                     │
   │  ⚠ Assumes scope freeze (no additions)                 │
   │                                                         │
   │ Accuracy Notes:                                         │
   │  Most accurate when:                                    │
   │  • Velocity is stable (✓ - 5pt std dev is good)       │
   │  • Scope is frozen (⚠ - watch for changes)            │
   │  • No major dependencies (check separately)             │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

7. **Test "What-If" Scenarios**
   ```
   Click [🧪 What-If Analysis] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ What-If Analysis                            [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Current Forecast: Sprint 17 (likely)                   │
   │                                                         │
   │ What if...                                              │
   │                                                         │
   │ ○ Scope increases by [10] points?                     │
   │   Impact: Pushes to Sprint 17 (still, +0 sprints)     │
   │                                                         │
   │ ○ Team velocity drops to [35] pts/sprint?             │
   │   Impact: Pushes to Sprint 18 (+1 sprint)             │
   │                                                         │
   │ ○ 2 team members unavailable next sprint?              │
   │   Impact: Sprint velocity → 28 pts                     │
   │           Pushes to Sprint 19 (+2 sprints)             │
   │                                                         │
   │ ○ We add 1 more developer to team?                     │
   │   Impact: Minimal (velocity won't increase            │
   │           immediately due to onboarding)                │
   │                                                         │
   │ [Calculate Impact]                                     │
   └─────────────────────────────────────────────────────────┘
   ```

8. **Check Dependencies Impact**
   ```
   Dependencies Tab:
   
   ┌─────────────────────────────────────────────────────────┐
   │ Dependencies Affecting Forecast                         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Blocking Dependencies: 2                               │
   │                                                         │
   │ ❌ [PBI] User Profile API must complete first          │
   │    Status: In Progress (Sprint 16)                     │
   │    Impact: Blocks 2 PBIs (12 pts)                      │
   │    Resolution: On track for Sprint 16                   │
   │                                                         │
   │ ⚠ [PBI] SMS Provider Integration                       │
   │    Status: Vendor approval pending                      │
   │    Impact: Blocks 1 PBI (5 pts)                        │
   │    Risk: Could delay Sprint 17 → 18                    │
   │                                                         │
   │ ⚠ Forecast assumes dependencies resolve on time        │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

9. **Save and Track Forecast**
   ```
   [💾 Save Forecast] button
   
   - Saves current forecast
   - Creates baseline for accuracy tracking
   - Can compare actual vs. forecast later
   - Useful for retrospectives and calibration
   ```

10. **Update Forecast**
    ```
    Click [🔄 Refresh Forecast] button
    
    - Recalculates based on latest data
    - Updates velocity if new sprints completed
    - Adjusts remaining work if items completed
    - Shows what changed since last forecast
    ```

11. **Export Forecast**
    ```
    [📊 Export] → Options:
    
    - Executive Summary (PDF) - One page overview
    - Detailed Report (PDF) - Includes assumptions, risks
    - Timeline (Image) - Visual for presentations
    - Data (Excel) - Raw numbers for analysis
    ```

**Tips:**
- Update forecast weekly (after standups or sprint planning)
- Always communicate likely case, mention worst case
- Document assumptions when saving forecast
- Don't hide worst-case scenarios from stakeholders
- Track forecast accuracy to improve future predictions
- If forecast slips repeatedly, investigate root causes
- Frozen scope = higher accuracy
- New team? Forecasts less reliable until velocity stabilizes

**Using Forecasts in Conversations:**

**Good Example:**
> "Based on Team Alpha's velocity of 41 points per sprint, we'll most likely complete Two-Factor Auth by end of Sprint 17 (Sep 29). There's a 75% chance we hit this date. Worst case, if velocity drops or scope grows, we could slip to Sprint 18 (Oct 13), but that's only 5% likely."

**Bad Example:**
> "We'll be done in 2 weeks." (No confidence interval, unrealistic best-case)

**When Forecast Changes:**
> "Our forecast has shifted. Originally Sprint 17, now looking like Sprint 18. Reason: Scope increased by 12 points when we discovered additional security requirements. Updated likely date: Oct 13."

---

## State Timeline Analysis

### Use Case: Work Item Lifecycle Optimization

**What It Is:**  
State Timeline Analysis visualizes how work items move through different states (New → Approved → Committed → In Progress → Done). It tracks time spent in each state, identifies bottlenecks, and compares individual work items against team averages to optimize your workflow.

**Why It Matters:**  
Understanding where work items get stuck helps you improve process efficiency. If PBIs sit "In Progress" for weeks, you might have too much WIP or hidden blockers. If items spend days in "Approved" before starting, your grooming-to-sprint handoff needs work. This analysis helps you spot and fix these issues.

**Sub-Features:**
- Visual state transition timeline
- Time-in-state calculations
- Cycle time vs. lead time metrics
- Bottleneck identification
- Team average comparisons
- State transition analysis (which paths items take)
- Aging work item alerts
- Process efficiency metrics

### Day-to-Day Usage Scenario

**Scenario: Daily Standup - Stuck Work Item Investigation**

During standup, team mentions PBI is "stuck":

1. **Team Reports Issue**
   - "PBI 12345: SMS Integration has been In Progress for 8 days"
   - Sarah opens State Timeline page
   - Enters work item ID: 12345

2. **View Timeline**
   - PBI lifecycle visualization shows:
     - New: 2 days (Jan 5-7)
     - Approved: 5 days (Jan 7-12) ⚠ Longer than average
     - Committed: 1 day (Jan 12-13)
     - In Progress: 8 days (Jan 13-21) ⚠ Much longer than average
     - (Not yet Done)
   - Total cycle time so far: 16 days

3. **Compare to Team Average**
   - Team average for PBIs:
     - In Progress: 3 days typically
     - Total cycle time: 7 days average
   - This PBI: 2x longer than normal
   - Red flag: Needs attention

4. **Analyze State Transitions**
   - Check transition history:
     - Jan 13: New → In Progress (skipped Approved/Committed!)
     - Jan 15: In Progress → Blocked (2 days)
     - Jan 16: Blocked → In Progress (1 day)
     - Jan 18: In Progress → Blocked (2 days)
     - Jan 19: Blocked → In Progress (ongoing)
   - Pattern: Multiple block/unblock cycles

5. **Root Cause Discussion**
   - Team discusses: External API vendor issues
   - Blocking issues not visible in standard view
   - Decision: Move to "Waiting" state for external dependencies
   - Process improvement: Add "Waiting on External" state

6. **Take Action**
   - Update process to use "Waiting" state
   - Document in State Timeline for future reference
   - Create alert: PBIs in "In Progress" > 5 days

### How To Use State Timeline

**Navigation:**
- **Page Location:** `/state-timeline` or `/state-timeline/{workItemId}`
- **Menu:** Click "State Timeline" under "Metrics"
- **Alternate:** From Work Items page → Right-click item → "View State Timeline"

**Step-by-Step:**

1. **Open State Timeline Page**
   ```
   Navigation → Metrics → State Timeline
   ```
   - Landing page shows work item selector

2. **Select Work Item**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Work Item Selection                                     │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Work Item ID: [12345          ] [Analyze]              │
   │                                                         │
   │ Or select from recent:                                  │
   │  • #12345 - SMS Integration (In Progress, 8 days)      │
   │  • #12340 - Payment UI (Done, 5 days)                  │
   │  • #12338 - Profile Edit (Done, 12 days) ⚠            │
   │                                                         │
   │ Or browse aging items:                                  │
   │  [Show items in "In Progress" > 5 days]                │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   - Enter work item ID OR select from list
   - Click "Analyze" to load timeline

3. **View State Timeline Visualization**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ [PBI] SMS Integration (#12345)                          │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ State Timeline (16 days total):                         │
   │                                                         │
   │ Jan 5   Jan 7    Jan 12  J13  Jan 15 J16  Jan 18 J19  │
   │  │        │        │      │      │    │      │    │    │
   │  ●────────●────────●──────●──────●────●──────●────●─→  │
   │  │   2d   │   5d   │  1d  │  2d  │ 1d │  2d  │  2d │  │
   │ New   Approved Committed   IP  Blocked IP Blocked IP   │
   │                                                         │
   │ Legend:                                                 │
   │  ● State Change   ─── Time in State   ─→ Ongoing      │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Timeline Elements:**
   - **Dots (●):** State changes
   - **Lines (───):** Time spent in state
   - **Arrow (─→):** Currently in this state
   - **Numbers:** Days in each state

4. **View Detailed State Table**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ State History                                           │
   ├─────────────────────────────────────────────────────────┤
   │ State       │ Entered  │ Exited   │ Duration │ vs Avg  │
   ├─────────────┼──────────┼──────────┼──────────┼─────────┤
   │ New         │ Jan 5    │ Jan 7    │  2 days  │ ✓ (2d)  │
   │ Approved    │ Jan 7    │ Jan 12   │  5 days  │ ⚠ (3d)  │
   │ Committed   │ Jan 12   │ Jan 13   │  1 day   │ ✓ (1d)  │
   │ In Progress │ Jan 13   │ Jan 15   │  2 days  │ ✓ (3d)  │
   │ Blocked     │ Jan 15   │ Jan 16   │  1 day   │ - (n/a) │
   │ In Progress │ Jan 16   │ Jan 18   │  2 days  │ ✓ (3d)  │
   │ Blocked     │ Jan 18   │ Jan 19   │  1 day   │ - (n/a) │
   │ In Progress │ Jan 19   │ Current  │  2 days  │ ✓ (3d)  │
   └─────────────────────────────────────────────────────────┘
   
   Total time: 16 days
   Team average for PBIs: 7 days
   Status: ⚠ 2.3x longer than average
   ```

5. **View Cycle Time Metrics**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Cycle Time Metrics                                      │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Lead Time:       16 days  (New → Done)                 │
   │   This Item:     16 days (ongoing)                     │
   │   Team Average:  10 days                               │
   │                                                         │
   │ Cycle Time:      12 days  (Committed → Done)           │
   │   This Item:     12 days (ongoing)                     │
   │   Team Average:  5 days                                │
   │                                                         │
   │ Active Time:     6 days   (In Progress states only)    │
   │ Wait Time:       6 days   (Blocked, Approved, etc.)    │
   │ Efficiency:      50%      (Active / Total)             │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Metric Definitions:**
   - **Lead Time:** New → Done (total time in system)
   - **Cycle Time:** Committed → Done (active development time)
   - **Active Time:** Sum of "In Progress" states
   - **Wait Time:** Sum of waiting/blocked states
   - **Efficiency:** % of time actively worked vs. waiting

6. **Compare to Team Averages**
   ```
   Click [📊 Team Comparison] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ Team Average Comparison                                 │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Time in "In Progress" State:                           │
   │                                                         │
   │ This Item:  ████████████████████ 8 days ⚠             │
   │ Team Avg:   ████████ 3 days                            │
   │ Team P90:   ████████████ 5 days                        │
   │                                                         │
   │ This item is in the 95th percentile (slower than       │
   │ 95% of team's PBIs).                                   │
   │                                                         │
   │ Time in "Approved" State:                              │
   │                                                         │
   │ This Item:  ████████████ 5 days ⚠                     │
   │ Team Avg:   ████ 3 days                                │
   │ Team P90:   ████████ 4 days                            │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

7. **Identify Bottlenecks**
   ```
   [⚠ Bottleneck Analysis] section:
   
   ┌─────────────────────────────────────────────────────────┐
   │ Identified Bottlenecks                                  │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ 🔴 Multiple Block/Unblock Cycles                       │
   │    Item blocked 2 times, total 2 days blocked          │
   │    Suggestion: Investigate root cause of blocking      │
   │                                                         │
   │ 🟡 Extended Time in "Approved"                         │
   │    5 days waiting to start (67% above average)         │
   │    Suggestion: Improve sprint planning handoff         │
   │                                                         │
   │ 🟢 Active development time is normal                    │
   │    When actively worked, progresses at expected rate   │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

8. **View All State Transitions**
   ```
   Click [📋 Transition Log] tab
   
   ┌─────────────────────────────────────────────────────────┐
   │ State Transition Log                                    │
   ├─────────────────────────────────────────────────────────┤
   │ Date/Time        │ From        │ To          │ By       │
   ├──────────────────┼─────────────┼─────────────┼──────────┤
   │ Jan 5, 10:00 AM  │ -           │ New         │ Sarah L. │
   │ Jan 7, 2:30 PM   │ New         │ Approved    │ Sarah L. │
   │ Jan 12, 9:00 AM  │ Approved    │ Committed   │ Sarah L. │
   │ Jan 13, 10:15 AM │ Committed   │ In Progress │ John S.  │
   │ Jan 15, 3:45 PM  │ In Progress │ Blocked     │ John S.  │
   │ Jan 16, 11:00 AM │ Blocked     │ In Progress │ John S.  │
   │ Jan 18, 4:30 PM  │ In Progress │ Blocked     │ John S.  │
   │ Jan 19, 9:15 AM  │ Blocked     │ In Progress │ John S.  │
   └─────────────────────────────────────────────────────────┘
   
   Notes/Comments:
   • Jan 15: Blocked - Vendor API credentials not working
   • Jan 18: Blocked - Vendor API rate limit exceeded
   ```

9. **View Aging Items Report**
   ```
   Click [📊 Aging Report] button (from main page)
   
   ┌─────────────────────────────────────────────────────────┐
   │ Aging Work Items                                        │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Items "In Progress" > 5 days:                          │
   │                                                         │
   │ ID     │ Title             │ State       │ Days │ Owner│
   │────────┼───────────────────┼─────────────┼──────┼──────│
   │ 12345  │ SMS Integration   │ In Progress │  8🔴│ John │
   │ 12342  │ OAuth Callback    │ In Progress │  7🟡│ Mary │
   │ 12339  │ User Settings     │ In Progress │  6🟡│ Tom  │
   │────────┴───────────────────┴─────────────┴──────┴──────│
   │                                                         │
   │ Total: 3 items need attention                          │
   │ Average "In Progress" time: 7 days (vs 3 day target)   │
   │                                                         │
   │ [Export Report] [Email Team]                           │
   └─────────────────────────────────────────────────────────┘
   ```

10. **Export Timeline**
    ```
    [📊 Export] → Options:
    
    - Timeline Image (PNG) - Visual for presentations
    - State Report (PDF) - Detailed analysis
    - Data (Excel) - Raw state history
    - Comparison (Excel) - Item vs. team averages
    ```

**Tips:**
- Review aging report weekly
- Set alerts for items "In Progress" > 5 days
- Use cycle time to improve sprint planning accuracy
- Compare similar work items to identify patterns
- High efficiency (>80%) = good process flow
- Low efficiency (<50%) = too much waiting time
- Blocked state should be explicit (don't hide in "In Progress")
- Use state timeline in retrospectives to identify improvements

**Process Improvement Workflow:**

**Monthly Review:**
1. Run aging items report
2. Analyze bottleneck patterns
3. Calculate average cycle time by work item type
4. Identify states where items spend most time
5. Discuss in retrospective
6. Implement one improvement
7. Measure impact next month

**Common Bottlenecks and Fixes:**
- **Long time in "Approved":** Improve grooming/sprint planning handoff
- **Long time in "In Progress":** Reduce WIP limits, smaller stories
- **Frequent "Blocked":** Better dependency management, unblock process
- **Long cycle time:** Break down work items, reduce scope per item

---

## Dependency Graph

### Use Case: Dependency Visualization

**What It Is:**  
The Dependency Graph visualizes relationships between work items, showing parent-child hierarchies, predecessor-successor links, and related items. It helps you understand the web of dependencies, identify critical paths, and spot blocking relationships that could delay delivery.

**Why It Matters:**  
Modern software projects have complex dependencies. Feature A can't start until Feature B completes. Epic X depends on infrastructure from Epic Y. Without visualization, these dependencies are invisible until they cause problems. The Dependency Graph makes dependencies explicit, helping you plan more effectively and avoid surprises.

**Sub-Features:**
- Interactive node-link graph
- Multiple relationship types (parent-child, predecessor-successor, related)
- Critical path highlighting
- Blocking relationship identification
- Zoom and pan navigation
- Filter by relationship type
- Dependency chain analysis
- Export graph images

### Day-to-Day Usage Scenario

**Scenario: Release Planning - Dependency Management**

Sarah plans Q2 release across both products:

1. **Visualize Product A Dependencies**
   - Opens Dependency Graph
   - Filters to Product A epics
   - Graph shows 5 epics, 12 features, 45 PBIs
   - Complex web of dependencies visible

2. **Identify Critical Path**
   - Highlights critical path (longest dependency chain)
   - Critical path: Epic "User Auth" → Epic "Payment" → Epic "Checkout"
   - These must complete in sequence
   - Any delay in Auth delays entire release

3. **Spot Blocking Dependencies**
   - Epic "Admin Dashboard" has red link to "User Auth"
   - Meaning: Admin Dashboard can't start until User Auth completes
   - User Auth currently Sprint 16-17
   - Admin Dashboard can't start before Sprint 18

4. **Analyze Cross-Product Dependencies**
   - Switches to "All Products" view
   - Discovers: Product B's "Shared Components" epic blocks Product A
   - This dependency wasn't in sprint plans!
   - Team Beta must prioritize Shared Components

5. **Replan Based on Dependencies**
   - Moves "Admin Dashboard" from Sprint 17 to Sprint 18
   - Adds "Shared Components" to Team Beta's Sprint 15
   - Updates stakeholder timeline: Delivery shifts 2 weeks

6. **Document Dependencies**
   - Exports graph as image for release plan documentation
   - Shares with both teams
   - Adds dependency notes in Azure DevOps

### How To Use Dependency Graph

**Navigation:**
- **Page Location:** `/dependency-graph`
- **Menu:** Click "Dependency Graph" under "Metrics"
- **Alternate:** From Work Items → Right-click item → "Show Dependencies"

**Step-by-Step:**

1. **Open Dependency Graph**
   ```
   Navigation → Metrics → Dependency Graph
   ```
   - Graph loads showing work item relationships

2. **Understand the Graph**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │                    Dependency Graph                     │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │              [Epic] User Auth                           │
   │                      │                                  │
   │              ┌───────┼───────┐                         │
   │              ▼       ▼       ▼                         │
   │         [Feature] [Feature] [Feature]                  │
   │          Social   2FA      Password                    │
   │          Login             Reset                        │
   │              │                                          │
   │              ├──────────► [Epic] Payment               │
   │              │                 │                        │
   │              │                 ▼                        │
   │              │          [Epic] Checkout                 │
   │              │                                          │
   │              └────────────► [Epic] Admin                │
   │                             Dashboard                   │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```
   
   **Graph Elements:**
   - **Nodes (boxes):** Work items
   - **Arrows (→):** Dependencies
   - **Colors:** 
     - Blue: Epic
     - Green: Feature
     - Yellow: PBI
     - Gray: Task
   - **Line styles:**
     - Solid (─): Parent-child
     - Dashed (┄): Predecessor-successor
     - Dotted (···): Related

3. **Navigate the Graph**
   - **Zoom:** Scroll wheel or pinch gesture
   - **Pan:** Click and drag background
   - **Select:** Click node to highlight and show details
   - **Focus:** Double-click node to center and show only related items

4. **View Node Details**
   ```
   Click any node → Detail panel opens
   
   ┌─────────────────────────────────────────────────────────┐
   │ [Epic] User Authentication                  [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ ID: 12300                                              │
   │ State: In Progress                                     │
   │ Effort: 48 points (12 complete, 36 remaining)         │
   │                                                         │
   │ Dependencies:                                           │
   │                                                         │
   │ ⬆ Parent:                                              │
   │    [Objective] Improve User Experience                 │
   │                                                         │
   │ ⬇ Children (3):                                        │
   │    [Feature] Social Login                              │
   │    [Feature] Two-Factor Auth                           │
   │    [Feature] Password Reset                            │
   │                                                         │
   │ ➡ Blocks (2):                                          │
   │    [Epic] Payment Integration ⚠                        │
   │    [Epic] Admin Dashboard ⚠                            │
   │                                                         │
   │ ⬅ Blocked By: None                                     │
   │                                                         │
   │ [View in Work Items] [Edit Dependencies]               │
   └─────────────────────────────────────────────────────────┘
   ```

5. **Filter Graph**
   ```
   Filters (top toolbar):
   
   Product: [Product A ▼]
   Type:    [All ▼]  or  [Epics] [Features] [PBIs]
   Links:   ☑ Parent-Child  ☑ Predecessor  ☐ Related
   State:   [Active ▼]
   
   [Apply Filters]
   ```
   
   - **Product:** Focus on single product or view all
   - **Type:** Show only specific work item types
   - **Links:** Toggle relationship types
   - **State:** Filter by work item state

6. **Highlight Critical Path**
   ```
   Click [🔍 Show Critical Path] button
   
   - Longest dependency chain highlighted in red
   - Shows minimum time to complete all dependencies
   - Helps identify schedule risks
   ```

7. **Identify Blocking Items**
   ```
   Click [⚠ Show Blockers] button
   
   - Items blocking others highlighted in orange
   - Shows count of items blocked by each
   - Focus on these to unblock progress
   ```

8. **Analyze Dependency Chain**
   ```
   Right-click node → "Show Dependency Chain"
   
   ┌─────────────────────────────────────────────────────────┐
   │ Dependency Chain for [Epic] Checkout                   │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Upstream Dependencies (must complete first):           │
   │                                                         │
   │ 1. [Epic] User Authentication (Sprint 16-17)           │
   │    └─> [Feature] Social Login (Sprint 16)              │
   │    └─> [Feature] Two-Factor Auth (Sprint 17)           │
   │                                                         │
   │ 2. [Epic] Payment Integration (Sprint 18)              │
   │    └─> [Feature] Stripe API (Sprint 18)                │
   │                                                         │
   │ 3. [Epic] Checkout (Sprint 19) ◄── This item           │
   │                                                         │
   │ Downstream Dependencies (blocked by this):             │
   │                                                         │
   │ 4. [Epic] Order Tracking (Sprint 20)                   │
   │ 5. [Epic] Shipping Integration (Sprint 21)             │
   │                                                         │
   │ Total chain length: 5 epics across 6 sprints           │
   │ Critical path: Yes (longest chain in product)          │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

9. **Add/Edit Dependencies**
   ```
   Right-click node → "Edit Dependencies"
   
   ┌─────────────────────────────────────────────────────────┐
   │ Edit Dependencies for [Epic] Admin Dashboard [×]       │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ This item is blocked by:                                │
   │                                                         │
   │ [Epic] User Authentication ✓                           │
   │ [Remove]                                                │
   │                                                         │
   │ Add new predecessor:                                    │
   │ Search: [🔍 Search work items...                     ] │
   │ [Add Dependency]                                        │
   │                                                         │
   │ This item blocks:                                       │
   │                                                         │
   │ [Epic] Reporting Suite ✓                               │
   │ [Remove]                                                │
   │                                                         │
   │ [Save] [Cancel]                                        │
   └─────────────────────────────────────────────────────────┘
   ```
   - Add/remove dependencies
   - Changes sync to Azure DevOps
   - Graph updates immediately

10. **Export Graph**
    ```
    [📊 Export] → Options:
    
    - Image (PNG/SVG) - For presentations
    - Data (Excel) - Dependency matrix
    - DOT Format - For external graph tools
    - Report (PDF) - Analysis with graph image
    ```

**Tips:**
- Review dependency graph during quarterly planning
- Update dependencies when planning new features
- Check critical path before making schedule commitments
- Use graph in cross-team planning meetings
- Color-code teams to see cross-team dependencies
- Export graph for release documentation
- Don't create circular dependencies (tool will warn)
- Keep dependency links up to date (stale links mislead)

**Dependency Management Best Practices:**

**Do:**
- ✓ Document dependencies early (during epic creation)
- ✓ Review dependencies in refinement
- ✓ Plan work to minimize dependencies
- ✓ Work on critical path items first
- ✓ Communicate dependencies across teams
- ✓ Update graph when dependencies change

**Don't:**
- ✗ Ignore dependencies until sprint planning
- ✗ Create circular dependencies (A blocks B blocks A)
- ✗ Over-depend (every item depends on everything)
- ✗ Hide dependencies (makes planning impossible)
- ✗ Assume dependencies will "work out"

**Common Dependency Patterns:**

1. **Sequential (A → B → C):** 
   - Clear order, easy to plan
   - Risk: Any delay cascades

2. **Fan-out (A → B, A → C, A → D):**
   - One item blocks many
   - Risk: A is critical bottleneck

3. **Fan-in (A → C, B → C):**
   - Many items must complete before C
   - Risk: Coordination required

4. **Diamond (A → B, A → C, B → D, C → D):**
   - Complex coordination
   - Risk: High probability of delays

---

## PR Insights

### Use Case: Code Review Process Optimization

**What It Is:**  
PR Insights analyzes your pull request metrics from the last 6 months, tracking time open, iteration counts, file changes, and linking PRs to work items. It helps you optimize the code review process, identify bottlenecks, and ensure PRs are efficiently merged.

**Why It Matters:**  
Long-lived PRs create integration problems, slow feature delivery, and frustrate developers. By tracking PR metrics, you can identify patterns: Are PRs too large? Do certain developers' PRs languish? Is the review process efficient? This data helps you improve team collaboration and delivery speed.

**Sub-Features:**
- PR metrics dashboard (last 6 months)
- Time-to-merge tracking
- Iteration count analysis
- File change size metrics
- Work item linking (via AB#[ID] convention)
- Reviewer response time
- PR age alerts
- Team comparison views

### Day-to-Day Usage Scenario

**Scenario: Retrospective - Improving Code Review Process**

During sprint retrospective, team discusses slow PR merges:

1. **Review PR Metrics**
   - Opens PR Insights page
   - Last sprint stats:
     - Average time to merge: 4.5 days
     - Average iterations: 3.2 (reviews + changes)
     - 8 PRs merged, 3 still open

2. **Identify Problem Pattern**
   - Sorts PRs by "time open"
   - Top 3 PRs open for 8+ days:
     - PR #245: Authentication refactor (12 files, 8 days)
     - PR #242: Payment integration (18 files, 9 days)
     - PR #238: Database migration (25 files, 11 days)
   - Pattern: Large PRs (>10 files) take 2x longer

3. **Analyze Iteration Counts**
   - PRs with 4+ review iterations take longest
   - Root cause discussion:
     - Large PRs hard to review thoroughly
     - Reviewers overwhelmed by size
     - Multiple rounds of feedback

4. **Team Decision**
   - New team agreement: Max 5 files per PR
   - Break large features into smaller PRs
   - Target: <3 days time-to-merge
   - Target: <2 review iterations

5. **Set Up Monitoring**
   - Adds PR Insights to weekly standup agenda
   - Track trend over next month
   - Measure if agreement improves metrics

6. **Link PRs to Work Items**
   - Reviews which PRs lack work item links
   - Reminds team: Use "AB#12345" in PR descriptions
   - Ensures PRs traceable to features/bugs

### How To Use PR Insights

**Navigation:**
- **Page Location:** `/pr-insights`
- **Menu:** Click "PR Insights" under "DevOps Insights"
- **Keyboard Shortcut:** None

**Step-by-Step:**

1. **Open PR Insights Page**
   ```
   Navigation → DevOps Insights → PR Insights
   ```
   - Dashboard loads showing PR metrics

2. **View Key Metrics**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │               PR Insights - Last 6 Months               │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
   │  │ Avg Time    │  │ Avg         │  │ PRs Merged  │   │
   │  │ to Merge    │  │ Iterations  │  │ This Month  │   │
   │  │  4.5 days   │  │    3.2      │  │     42      │   │
   │  │    ↑ +0.8   │  │    ↑ +0.5   │  │    ↓ -8     │   │
   │  └─────────────┘  └─────────────┘  └─────────────┘   │
   │                                                         │
   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
   │  │ Avg Files   │  │ Open PRs    │  │ Reviewers   │   │
   │  │ Changed     │  │ > 5 days    │  │ Avg/PR      │   │
   │  │    8.3      │  │      5      │  │    2.1      │   │
   │  │    → stable │  │    ⚠ +2    │  │    ↑ +0.3   │   │
   │  └─────────────┘  └─────────────┘  └─────────────┘   │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

3. **View PR Trends Chart**
   ```
   Time to Merge Trend (Last 3 Months)
   
   Days
    10├────────────────────────────────────────────
      │                              ●
     8│                        ●  ●
      │                  ●  ●          
     6│            ●  ●                    ●
      │      ●  ●                    ●  ●
     4│●  ●                                    ●──●
      │
     2├────────────────────────────────────────────
      Oct      Nov      Dec      Jan      Feb
   
   ⚠ Trend: Time to merge increasing
   ```

4. **View PR List**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Pull Requests                                           │
   ├─────────────────────────────────────────────────────────┤
   │ PR#  │ Title              │ Days │ Iter │ Files │ WI   │
   ├──────┼────────────────────┼──────┼──────┼───────┼──────┤
   │ 245⚠│ Auth refactor      │  8   │  4   │  12   │12345│
   │ 243 │ Fix login bug      │  2   │  1   │   3   │12340│
   │ 242⚠│ Payment API        │  9   │  5   │  18   │12338│
   │ 240 │ Update deps        │  1   │  1   │   1   │  -  │
   │ 238🔴 DB migration        │ 11   │  6   │  25   │12335│
   └─────────────────────────────────────────────────────────┘
   
   Legend: ⚠ > 5 days open   🔴 > 10 days open
   ```
   
   - Click column headers to sort
   - Click PR to view details

5. **View PR Details**
   ```
   Click PR #245 →
   
   ┌─────────────────────────────────────────────────────────┐
   │ PR #245: Authentication Refactoring         [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Author: John Smith                                      │
   │ Created: Jan 14, 2026                                  │
   │ Status: Open (8 days)                                  │
   │                                                         │
   │ Work Item: [PBI] Modernize Auth (#12345)               │
   │                                                         │
   │ Metrics:                                                │
   │  Files Changed:     12                                  │
   │  Lines Added:       +342                                │
   │  Lines Deleted:     -156                                │
   │  Review Iterations: 4                                   │
   │                                                         │
   │ Timeline:                                               │
   │  Jan 14: Created                                        │
   │  Jan 15: First review by Mary (requested changes)      │
   │  Jan 16: Updated by John                                │
   │  Jan 17: Second review by Tom (requested changes)      │
   │  Jan 18: Updated by John                                │
   │  Jan 19: Third review by Mary (approved)               │
   │  Jan 20: Fourth review by Tom (requested changes)      │
   │  Jan 21: Updated by John (pending review)              │
   │                                                         │
   │ [View in Azure DevOps] [View Work Item]                │
   └─────────────────────────────────────────────────────────┘
   ```

6. **Filter PRs**
   ```
   Filters:  
   
   Status:     [All ▼]  (All, Open, Merged, Closed)
   Author:     [All ▼]  (Team members)
   Time Open:  [All ▼]  (< 3 days, 3-5 days, > 5 days)
   Work Item:  ☐ Linked  ☐ Unlinked
   
   [Apply Filters]
   ```

7. **View Team Comparison**
   ```
   Click [📊 Team Comparison] tab
   
   ┌─────────────────────────────────────────────────────────┐
   │ PR Metrics by Team Member                               │
   ├─────────────────────────────────────────────────────────┤
   │ Author   │ PRs  │ Avg Days │ Avg Iter │ Avg Files     │
   ├──────────┼──────┼──────────┼──────────┼───────────────┤
   │ John S.  │  18  │   5.2⚠  │   3.8    │   11.2⚠      │
   │ Mary T.  │  24  │   3.1✓  │   2.4✓  │    6.5✓      │
   │ Tom R.   │  16  │   4.8    │   3.2    │    8.1       │
   │ Sarah L. │   8  │   2.9✓  │   2.1✓  │    4.2✓      │
   └─────────────────────────────────────────────────────────┘
   
   Insights:
   • John's PRs tend to be larger and take longer
   • Mary consistently creates small, fast-merging PRs
   • Consider sharing Mary's PR practices with team
   ```
   
   **Important:** Use for process improvement, not performance evaluation

8. **Identify Unlinked PRs**
   ```
   Click [⚠ Unlinked PRs] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ PRs Without Work Item Links                             │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ 8 PRs do not reference work items                       │
   │                                                         │
   │ PR#  │ Title                    │ Author  │ Status     │
   │──────┼──────────────────────────┼─────────┼────────────│
   │ 240  │ Update dependencies      │ John S. │ Merged    │
   │ 236  │ Fix typo in README       │ Tom R.  │ Merged    │
   │ 233  │ Refactor utils           │ Mary T. │ Open      │
   │                                                         │
   │ Reminder: Use "AB#12345" in PR description to link     │
   │ PRs to work items for better traceability.              │
   │                                                         │
   │ [Close]                                                 │
   └─────────────────────────────────────────────────────────┘
   ```

9. **Export PR Report**
   ```
   [📊 Export] → Options:
   
   - Summary Report (PDF) - Key metrics
   - PR List (Excel) - All PRs with details
   - Team Report (PDF) - By-author metrics
   - Trends (Image) - Charts for presentations
   ```

**Tips:**
- Review PR metrics weekly
- Target <3 days average time-to-merge
- Keep PRs small (<10 files ideal)
- Link all PRs to work items using "AB#[WorkItemId]"
- <2 review iterations indicates good quality
- Discuss PR patterns in retrospectives
- Don't use metrics to evaluate individuals
- Focus on process improvement, not blame

**PR Best Practices:**

**Good PR Habits:**
- ✓ Small, focused changes (one feature/fix)
- ✓ Clear description with "AB#[ID]" link
- ✓ Self-review before requesting review
- ✓ Request 1-2 specific reviewers
- ✓ Respond to feedback promptly

**PR Anti-Patterns:**
- ✗ Large PRs (>15 files) - hard to review
- ✗ Multiple unrelated changes in one PR
- ✗ No description or context
- ✗ No work item link
- ✗ Ignoring reviewer feedback

---

## Pipeline Insights

### Use Case: CI/CD Health Monitoring

**What It Is:**  
Pipeline Insights monitors your build and release pipeline health metrics, including success rates, build durations, and failing pipelines. It helps you maintain reliable CI/CD, identify flaky tests or infrastructure issues, and improve build performance.

**Why It Matters:**  
Broken pipelines block feature delivery. Long build times slow iteration speed. Flaky tests erode confidence. Pipeline Insights gives you visibility into CI/CD health, helping you prioritize fixes and maintain fast, reliable deployments that support continuous delivery.

**Sub-Features:**
- Build success rate tracking
- Build duration trends
- Failing pipeline identification
- Flaky test detection
- Pipeline performance metrics
- Historical trend analysis
- Alert configuration
- Team pipeline comparison

### Day-to-Day Usage Scenario

**Scenario: Monday Morning CI/CD Health Check**

Sarah checks pipeline health at start of week:

1. **Dashboard Review**
   - Opens Pipeline Insights
   - Overall success rate: 87% (down from 93% last week)
   - Red flag: Something changed

2. **Identify Failing Pipelines**
   - 3 pipelines failing consistently:
     - "Product A - Main Build": 12 failures in 20 runs (60%)
     - "Product B - Integration Tests": 3 failures in 15 runs (80%)
     - "Deployment - Staging": 5 failures in 10 runs (50%)

3. **Investigate Main Build Failures**
   - Clicks "Product A - Main Build"
   - Failure pattern: Started Jan 17
   - Error: "npm install timeout"
   - 10 builds affected
   - Root cause: New dependency added, large size

4. **Investigate Integration Test Failures**
   - Clicks "Product B - Integration Tests"
   - Flaky test detected: "Login_Successful_Test"
   - Passes some runs, fails others (intermittent)
   - Root cause: Race condition in test

5. **Take Action**
   - Product A: Update dependency to smaller alternative
   - Product B: Quarantine flaky test, assign developer to fix
   - Staging deploy: Infrastructure issue, escalate to DevOps

6. **Set Up Alerts**
   - Configures alert: Success rate < 85%
   - Email notification to team
   - Prevents pipeline degradation

### How To Use Pipeline Insights

**Navigation:**
- **Page Location:** `/pipeline-insights`
- **Menu:** Click "Pipeline Insights" under "DevOps Insights"
- **Keyboard Shortcut:** None

**Step-by-Step:**

1. **Open Pipeline Insights**
   ```
   Navigation → DevOps Insights → Pipeline Insights
   ```
   - Dashboard loads with pipeline health metrics

2. **View Key Metrics**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │          Pipeline Health - Last 30 Days                 │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
   │  │ Success     │  │ Avg Build   │  │ Total       │   │
   │  │ Rate        │  │ Duration    │  │ Builds      │   │
   │  │    87%      │  │   8.2 min   │  │    248      │   │
   │  │  ↓ -6%⚠   │  │   ↑ +1.3m   │  │   ↑ +12     │   │
   │  └─────────────┘  └─────────────┘  └─────────────┘   │
   │                                                         │
   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
   │  │ Failing     │  │ Flaky       │  │ Longest     │   │
   │  │ Pipelines   │  │ Tests       │  │ Build       │   │
   │  │      3      │  │      5      │  │   24 min    │   │
   │  │   ⚠ +1     │  │   ⚠ +2     │  │   🔴 slow   │   │
   │  └─────────────┘  └─────────────┘  └─────────────┘   │
   │                                                         │
   └─────────────────────────────────────────────────────────┘
   ```

3. **View Success Rate Trend**
   ```
   Success Rate Trend - Last 90 Days
   
   100%├────────────────────────────────────────────
       │  ●──●──●──●
    95%│              ●
       │                ●──●
    90%│                      ●
       │                        ●──●
    85%│                              ●──●
       │
    80%├────────────────────────────────────────────
       Nov      Dec      Jan      Feb
   
   ⚠ Declining trend since Jan 17
   ```

4. **View Pipeline List**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Pipelines                                               │
   ├─────────────────────────────────────────────────────────┤
   │ Pipeline Name           │ Success│ Runs │ Avg Time    │
   ├─────────────────────────┼────────┼──────┼─────────────┤
   │ Product A - Main Build🔴 │  60%  │  20  │  12.5 min  │
   │ Product A - Unit Tests   │  98%  │  20  │   4.2 min  │
   │ Product B - Main Build   │  93%  │  15  │   9.8 min  │
   │ Product B - Int Tests⚠  │  80%  │  15  │   8.5 min  │
   │ Deployment - Staging🔴   │  50%  │  10  │  15.2 min  │
   │ Deployment - Production  │ 100%  │   5  │  18.0 min  │
   └─────────────────────────────────────────────────────────┘
   
   Legend: 🔴 < 70%   ⚠ 70-90%   ✓ > 90%
   ```

5. **View Pipeline Details**
   ```
   Click "Product A - Main Build" →
   
   ┌─────────────────────────────────────────────────────────┐
   │ Pipeline: Product A - Main Build            [×]         │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Success Rate: 60% (12/20 runs) 🔴                      │
   │ Avg Duration: 12.5 minutes                             │
   │                                                         │
   │ Recent Runs:                                            │
   │ Jan 22  ●  Success    (11.2 min)                       │
   │ Jan 22  ✗  Failed     (8.3 min) - npm timeout          │
   │ Jan 21  ✗  Failed     (8.5 min) - npm timeout          │
   │ Jan 21  ●  Success    (12.1 min)                       │
   │ Jan 20  ✗  Failed     (8.2 min) - npm timeout          │
   │ Jan 20  ●  Success    (11.8 min)                       │
   │ ...                                                     │
   │                                                         │
   │ Common Failure Reasons:                                 │
   │  ├─ npm install timeout (10 occurrences) ⚠            │
   │  ├─ Test "Auth_Test" failed (1 occurrence)             │
   │  └─ Out of disk space (1 occurrence)                   │
   │                                                         │
   │ Failure Pattern:                                        │
   │  Failures started: Jan 17                              │
   │  Trigger: Commit #abc123 (added new dependency)        │
   │                                                         │
   │ [View in Azure DevOps] [View Related Commits]          │
   └─────────────────────────────────────────────────────────┘
   ```

6. **Identify Flaky Tests**
   ```
   Click [⚠ Flaky Tests] tab
   
   ┌─────────────────────────────────────────────────────────┐
   │ Flaky Tests Detected                                    │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Test Name                │ Pass Rate │ Last Fail      │
   ├──────────────────────────┼───────────┼────────────────┤
   │ Login_Successful_Test    │   65%⚠   │ Jan 21        │
   │ Payment_Process_Test     │   80%⚠   │ Jan 19        │
   │ Profile_Update_Test      │   75%⚠   │ Jan 18        │
   │ Search_Results_Test      │   85%    │ Jan 15        │
   │ Notification_Send_Test   │   90%    │ Jan 12        │
   └─────────────────────────────────────────────────────────┘
   
   Flaky Test Definition: Pass rate between 50-95%
   
   Recommendation: Quarantine or fix flaky tests
   They erode confidence in CI/CD pipeline.
   ```

7. **View Build Duration Trends**
   ```
   Build Duration Trend
   
   Min
    20├────────────────────────────────────────────
      │                                    ●
    16│                              ●  ●
      │                        ●  ●
    12│                  ●  ●              ●──●──●
      │            ●  ●
     8│      ●  ●
      │●  ●
     4├────────────────────────────────────────────
      Dec      Jan      Feb
   
   ⚠ Duration increasing - investigate performance
   ```

8. **Filter Pipelines**
   ```
   Filters:
   
   Product:     [All ▼]
   Status:      [All ▼]  (Passing, Failing, Flaky)
   Duration:    [All ▼]  (< 5min, 5-15min, > 15min)
   Date Range:  [Last 30 days ▼]
   
   [Apply Filters]
   ```

9. **Configure Alerts**
   ```
   Click [🔔 Configure Alerts] button
   
   ┌─────────────────────────────────────────────────────────┐
   │ Pipeline Alerts Configuration           [×]             │
   ├─────────────────────────────────────────────────────────┤
   │                                                         │
   │ Alert Conditions:                                       │
   │                                                         │
   │ ☑ Success rate drops below [85]%                       │
   │ ☑ Build duration exceeds [20] minutes                  │
   │ ☑ Pipeline fails [3] consecutive times                 │
   │ ☑ Flaky test detected (pass rate 50-95%)               │
   │ ☐ New failing test detected                             │
   │                                                         │
   │ Notification Method:                                    │
   │ ☑ Email team                                           │
   │ ☑ Show in dashboard                                    │
   │ ☐ Send Slack message                                   │
   │                                                         │
   │ Recipients:                                             │
   │ [sarah@company.com; team@company.com              ]   │
   │                                                         │
   │ [Save] [Cancel]                                        │
   └─────────────────────────────────────────────────────────┘
   ```

10. **Export Pipeline Report**
    ```
    [📊 Export] → Options:
    
    - Health Summary (PDF) - Overview metrics
    - Pipeline Details (Excel) - All pipelines
    - Failure Analysis (PDF) - Common failures
    - Trends (Image) - Charts for presentations
    ```

**Tips:**
- Check pipeline health Monday morning
- Target >95% success rate
- Fix failing pipelines immediately
- Quarantine flaky tests until fixed
- Optimize slow builds (target <10 min)
- Set up alerts for degradation
- Track duration trends - builds often slow over time
- Investigate sudden success rate drops
- Keep build agents healthy and updated

**Pipeline Health Best Practices:**

**Healthy CI/CD:**
- ✓ >95% success rate
- ✓ <10 minute build times
- ✓ No flaky tests (100% or 0%)
- ✓ Fast feedback (<5 min for unit tests)
- ✓ Automated rollback on failure

**Pipeline Anti-Patterns:**
- ✗ Ignoring failing builds
- ✗ Accepting flaky tests as "normal"
- ✗ Slow builds (>20 min) that block developers
- ✗ Manual intervention required
- ✗ No monitoring or alerts

---

## Settings Pages

### Manage Products

**Page Location:** `/settings/products`

**Purpose:** Configure all products across your organization. Add new products, edit existing ones, and identify orphaned products not assigned to any Product Owner.

**How To Use:**

1. **View Products**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Manage Products                                         │
   ├─────────────────────────────────────────────────────────┤
   │ Product Name        │ Area Paths       │ PO     │ Edit │
   ├─────────────────────┼──────────────────┼────────┼──────┤
   │ Mobile App          │ Project\Mobile   │ Sarah  │ [✏] │
   │ Web Portal          │ Project\Web      │ Sarah  │ [✏] │
   │ Admin Dashboard     │ Project\Admin    │ (None) │ [✏] │
   │                                                         │
   │ [+ Add Product]                                         │
   └─────────────────────────────────────────────────────────┘
   ```

2. **Add Product**
   - Click "+ Add Product"
   - Fill in: Name, Description, Area Path(s)
   - Optionally assign Product Owner
   - Save

3. **Edit Product**
   - Click edit button
   - Update fields
   - Save changes

4. **Orphaned Products**
   - Products without PO shown in yellow
   - Assign PO to activate metrics tracking

### Manage Teams

**Page Location:** `/settings/teams`

**Purpose:** Manage team configurations, add new teams, show/hide archived teams. Teams are used for filtering in metrics and analytics.

**How To Use:**

1. **View Teams**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Manage Teams                                            │
   ├─────────────────────────────────────────────────────────┤
   │ Team Name    │ Members │ Product      │ Active │ Edit  │
   ├──────────────┼─────────┼──────────────┼────────┼───────┤
   │ Team Alpha   │    5    │ Mobile App   │   ✓    │ [✏]  │
   │ Team Beta    │    7    │ Web Portal   │   ✓    │ [✏]  │
   │ Team Gamma   │    3    │ Mobile App   │   ✗    │ [✏]  │
   │                                                         │
   │ [+ Add Team]  [☐ Show Archived]                        │
   └─────────────────────────────────────────────────────────┘
   ```

2. **Add Team**
   - Click "+ Add Team"
   - Enter: Name, Member count, Associated product
   - Set active status
   - Save

3. **Archive Team**
   - Edit team
   - Uncheck "Active"
   - Team hidden from most filters but data preserved

4. **Show Archived**
   - Check "Show Archived" to see inactive teams
   - Useful for historical analysis

### Manage Product Owner

**Page Location:** `/settings/productowner/{ProfileId}` or `/settings/productowner/edit/{ProfileId}`

**Purpose:** View and edit Product Owner profile information, including name, email, and product assignments.

**How To Use:**

1. **View Profile**
   - Shows: Name, Email, Products managed
   - Click "Edit" to modify

2. **Edit Profile**
   - Update name, email
   - Add/remove product assignments
   - Save changes

### Work Item States

**Page Location:** `/settings/workitem-states`

**Purpose:** Configure work item state mappings and validation rules. Map Azure DevOps states to PO Companion's workflow states.

**How To Use:**

1. **View State Mappings**
   ```
   ┌─────────────────────────────────────────────────────────┐
   │ Work Item State Configuration                           │
   ├─────────────────────────────────────────────────────────┤
   │ Azure DevOps State  │ PO Companion State │ Validation │
   ├─────────────────────┼────────────────────┼────────────┤
   │ New                 │ New                │ Required   │
   │ Approved            │ Approved           │ Optional   │
   │ Committed           │ Committed          │ Required   │
   │ In Progress         │ In Progress        │ Required   │
   │ Done                │ Done               │ Required   │
   │ Removed             │ Closed             │ Optional   │
   └─────────────────────────────────────────────────────────┘
   ```

2. **Edit Mappings**
   - Click state to edit
   - Map to standard workflow state
   - Set validation requirements
   - Save

3. **Validation Rules**
   - "Required": State must exist for work items
   - "Optional": State can be skipped in workflow
   - Affects validation score calculations

---

