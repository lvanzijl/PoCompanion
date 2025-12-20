# GitHub Issues for Deferred Work

This document contains detailed issue templates for all deferred work identified in the compliance and quality assurance review.

---

## HIGH PRIORITY (P0) - Critical for Production


### Issue 2: Simplify Error Messages for End Users

**Title**: Improve error message UX by hiding technical details from end users

**Labels**: `ux`, `P0`, `user-experience`

**Description**:

Error messages currently expose technical details like HTTP status codes, exception types, and stack traces, which confuse non-technical users.

**Current State**:
Error messages show:
- HTTP status codes (401, 404, 500, etc.)
- Exception class names (TfsException, HttpRequestException)
- Technical error details
- No option to view details for technical users

**Expected State**:
- User-friendly error messages by default
- Technical details hidden behind "Show Details" button
- Consistent error message formatting across app
- Context-specific error suggestions

**User Story**:
As a product owner, I want error messages that explain what went wrong and what I should do, not technical details about HTTP status codes and exception types.

**Implementation Steps**:
1. Create centralized error message service
   - Define user-friendly messages for common errors
   - Map technical exceptions to user messages
   - Provide "Show Details" option for debugging

2. Update error handling in all services
   - TfsConfigService error messages
   - WorkItemService error messages
   - SettingsService error messages
   - PullRequestService error messages

3. Improve TFS error handling specifically
   - 401 → "Authentication failed. Please check your Personal Access Token."
   - 403 → "Access denied. Please verify you have permission to access this resource."
   - 404 → "Resource not found. Please verify your configuration."
   - 429 → "Too many requests. Please wait a moment before trying again."
   - 500+ → "Server error. Please try again or contact support if the problem persists."

4. Add "Show Technical Details" expansion
   - Collapsible section for HTTP status, exception type, stack trace
   - Only visible when user expands it
   - Helpful for debugging and support

5. Add contextual help
   - Links to documentation for specific errors
   - Suggested next steps for resolution

**Acceptance Criteria**:
- [ ] All error messages are user-friendly by default
- [ ] Technical details available via "Show Details"
- [ ] Consistent error formatting across application
- [ ] No HTTP status codes shown in primary error message
- [ ] Context-specific resolution suggestions provided

**Estimated Effort**: 2-3 days

**Files Affected**:
- Create: PoTool.Client/Services/ErrorMessageService.cs
- PoTool.Client/Services/TfsConfigService.cs
- PoTool.Client/Services/WorkItemService.cs
- PoTool.Client/Services/SettingsService.cs
- PoTool.Client/Services/PullRequestService.cs
- PoTool.Client/Pages/TfsConfig.razor
- PoTool.Client/Components/WorkItems/WorkItemExplorer.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-005
- EXECUTIVE_SUMMARY.md Section on UX Issues

---

### Issue 3: Add User Onboarding Wizard

**Title**: Create first-time user onboarding wizard for initial setup

**Labels**: `ux`, `P0`, `enhancement`, `user-experience`

**Description**:

New users are currently dropped into an empty work item explorer with no guidance on how to get started. This creates a confusing first-time experience.

**Current State**:
- No guidance for first-time users
- Empty tree view with no explanation
- Users don't know to configure TFS first
- No explanation of features

**Expected State**:
- Welcome wizard on first launch
- Step-by-step setup guide
- Feature tour after setup
- Option to skip or revisit wizard

**User Story**:
As a first-time user, I want to be guided through the initial setup so I can quickly start using the application without confusion.

**Implementation Steps**:
1. Create onboarding wizard component
   - Multi-step dialog/overlay
   - Progress indicator
   - Skip option
   - "Don't show again" checkbox

2. Define wizard steps
   - Step 1: Welcome & Overview
     - What is PO Companion
     - Key features overview
     - Who should use it
   
   - Step 2: TFS Configuration
     - Link to TFS config page
     - Explain what information is needed
     - Help with finding organization URL
   
   - Step 3: Initial Sync
     - Explain sync process
     - Full vs incremental sync
     - Expected duration
   
   - Step 4: Feature Tour
     - Filters and search
     - Validation features
     - Keyboard shortcuts
     - Settings options

3. Add wizard triggers
   - Check if first launch (local storage)
   - Show wizard automatically
   - Add "Help" menu item to reopen wizard
   - "Getting Started" button on empty states

4. Track completion state
   - Store in local storage
   - Remember if user completed setup
   - Allow manual trigger from help menu

5. Add tooltips for key features
   - Validation filters explanation
   - Sync button descriptions
   - Multi-selection hints

**Acceptance Criteria**:
- [ ] Wizard appears on first launch
- [ ] User can complete TFS setup from wizard
- [ ] User can skip wizard
- [ ] Wizard can be reopened from Help menu
- [ ] Completion state persisted
- [ ] Clear explanation of all key features
- [ ] Links to relevant documentation

**Estimated Effort**: 3-4 days

**Files Affected**:
- Create: PoTool.Client/Components/Onboarding/OnboardingWizard.razor
- Create: PoTool.Client/Components/Onboarding/OnboardingStep.razor
- Create: PoTool.Client/Services/OnboardingService.cs
- PoTool.Client/Layout/MainLayout.razor
- PoTool.Client/Pages/Home.razor
- PoTool.Client/Components/WorkItems/WorkItemExplorer.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-001
- EXECUTIVE_SUMMARY.md Must Address Items

---

## MEDIUM PRIORITY (P1) - Next Sprint

### Issue 4: Add Validation Filter Tooltips and Explanations

**Title**: Add tooltips and help text to validation filters

**Labels**: `ux`, `P1`, `enhancement`

**Description**:

Validation filter checkboxes ("Parent Progress Issues", "Missing Effort Issues") don't explain what they filter, causing user confusion.

**Current State**:
- Filter labels are unclear
- No explanation of what each filter does
- No indication of how many items have issues
- Users must guess filter behavior

**Expected State**:
- Tooltip on each filter explaining what it does
- Count of items with issues shown in label
- Help icon with detailed explanation
- Examples of what triggers each filter

**Implementation Steps**:
1. Add MudTooltip to each filter checkbox
   - Parent Progress: "Highlights work items that are in progress but their parent/ancestor is not"
   - Missing Effort: "Highlights work items in 'In Progress' state without effort/story points"

2. Add issue counts to filter labels
   - "Parent Progress Issues (5)" instead of just "Parent Progress Issues"
   - Update count when data changes
   - Show "(0)" if no issues

3. Add help icon with detailed modal
   - Click help icon opens explanation dialog
   - Shows examples of when filter triggers
   - Suggests how to resolve issues

4. Improve filter naming
   - Consider more descriptive names
   - "Items with Parent/Ancestor Not In Progress" vs "Parent Progress Issues"

**Acceptance Criteria**:
- [ ] Each filter has tooltip explaining behavior
- [ ] Issue counts shown in filter labels
- [ ] Help icon with detailed explanation
- [ ] Clear examples of filter triggers
- [ ] Counts update when data changes

**Estimated Effort**: 1 day

**Files Affected**:
- PoTool.Client/Components/WorkItems/SubComponents/WorkItemToolbar.razor
- PoTool.Client/Components/WorkItems/WorkItemExplorer.razor
- Create: PoTool.Client/Components/Help/ValidationFilterHelp.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-002

---

### Issue 5: Add Multi-Selection Visual Feedback

**Title**: Improve multi-selection UX with clear visual indicators

**Labels**: `ux`, `P1`, `enhancement`

**Description**:

Multi-selection is possible but users don't know how many items are selected or that bulk actions are available.

**Current State**:
- Multi-selection works but no visual feedback
- No count of selected items
- No indication of multi-select capability
- No bulk action toolbar

**Expected State**:
- Selection counter showing "X items selected"
- Bulk action toolbar when multiple items selected
- Clear visual indication of selection mode
- Select All / Clear Selection buttons

**Implementation Steps**:
1. Add selection counter to toolbar
   - Show "X items selected" when > 0
   - Update on selection change
   - Position prominently

2. Add selection action buttons
   - "Select All" button
   - "Clear Selection" button
   - Position near filter controls

3. Create bulk action toolbar (phase 2 - see Feature issue)
   - Appears when items selected
   - Shows available bulk actions
   - Context-sensitive actions

4. Improve visual selection indicators
   - Consider checkboxes instead of just highlighting
   - Clearer selected state styling
   - Multi-select affordance indicators

5. Add keyboard shortcuts for selection
   - Ctrl+A for select all
   - Escape for clear selection
   - Ctrl+Click for multi-select

**Acceptance Criteria**:
- [ ] Selection count always visible
- [ ] "Select All" and "Clear Selection" buttons work
- [ ] Clear visual indication of selected items
- [ ] Keyboard shortcuts functional
- [ ] Selection state persists during filtering

**Estimated Effort**: 2 days

**Files Affected**:
- PoTool.Client/Components/WorkItems/SubComponents/WorkItemToolbar.razor
- PoTool.Client/Components/WorkItems/WorkItemExplorer.razor
- PoTool.Client/Components/WorkItems/SubComponents/WorkItemTreeNode.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-004

---

### Issue 6: Enable Skipped TfsConfig Blazor Tests

**Title**: Implement and enable skipped TfsConfig page bUnit tests

**Labels**: `testing`, `P1`, `technical-debt`

**Description**:

Three bUnit tests for TfsConfig page are currently skipped, reducing test coverage of critical configuration UI.

**Current State**:
- `TfsConfig_RendersFormElements` - Skipped
- `TfsConfig_DisplaysSaveButton` - Skipped
- `TfsConfig_LoadsExistingConfiguration` - Skipped

**Expected State**:
- All TfsConfig tests implemented and passing
- Proper mocking of TfsConfigService
- Test coverage for form validation
- Test coverage for save/test connection flows

**Implementation Steps**:
1. Analyze why tests are skipped
   - Check for missing dependencies
   - Identify mocking requirements
   - Determine test infrastructure needs

2. Implement TfsConfig_RendersFormElements
   - Verify URL input field renders
   - Verify Project input field renders
   - Verify PAT input field renders
   - Verify Auth Mode dropdown renders
   - Verify Save and Test Connection buttons render

3. Implement TfsConfig_DisplaysSaveButton
   - Verify Save button is present
   - Verify button enabled/disabled states
   - Test button text and styling

4. Implement TfsConfig_LoadsExistingConfiguration
   - Mock TfsConfigService.GetConfigAsync
   - Provide test configuration data
   - Verify form fields populated with existing config
   - Verify last validated timestamp displayed

5. Add additional test coverage
   - Form validation tests
   - Save button click handler
   - Test connection button click handler
   - Error display tests

**Acceptance Criteria**:
- [ ] All three skipped tests implemented
- [ ] All tests passing
- [ ] TfsConfigService properly mocked
- [ ] Form rendering fully tested
- [ ] Configuration loading tested

**Estimated Effort**: 1 day

**Files Affected**:
- PoTool.Tests.Blazor/TfsConfigPageTests.cs (currently empty or incomplete)

**References**:
- COMPLIANCE_FINDINGS.md Section 2.2
- Test results showing 3 skipped tests

---

### Issue 7: Add Sync Operation Tooltips

**Title**: Add tooltips explaining Full Sync vs Incremental Sync

**Labels**: `ux`, `P1`, `enhancement`

**Description**:

Users don't understand the difference between "Full Sync" and "Incremental Sync" buttons, leading to inefficient usage.

**Current State**:
- Two sync buttons with no explanation
- No indication of which to use when
- No feedback on sync duration or data volume

**Expected State**:
- Tooltip on Full Sync explaining behavior and use case
- Tooltip on Incremental Sync explaining behavior and use case
- Optional: Duration estimate or progress indicator

**Implementation Steps**:
1. Add MudTooltip to Full Sync button
   - Text: "Downloads all work items from Azure DevOps (slower, but complete)"
   - Suggest when to use: "Use when syncing for first time or after major changes"

2. Add MudTooltip to Incremental Sync button
   - Text: "Downloads only changes since last sync (faster)"
   - Suggest when to use: "Use for regular updates"

3. Consider adding sync status feedback
   - Show "Last synced: X minutes ago"
   - Show "Syncing X items..." during sync
   - Show completion notification

4. Add help icon with detailed explanation
   - Opens modal with full explanation
   - Includes recommendations for sync frequency
   - Explains caching behavior

**Acceptance Criteria**:
- [ ] Full Sync has clear tooltip
- [ ] Incremental Sync has clear tooltip
- [ ] Tooltips explain when to use each option
- [ ] Last sync time displayed
- [ ] Sync progress feedback shown

**Estimated Effort**: 0.5 days

**Files Affected**:
- PoTool.Client/Components/WorkItems/SubComponents/WorkItemToolbar.razor
- PoTool.Client/Components/WorkItems/WorkItemExplorer.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-003

---

### Issue 8: Standardize Loading States Across Application

**Title**: Implement consistent loading indicators for all async operations

**Labels**: `ux`, `P1`, `enhancement`

**Description**:

Loading states are inconsistent - some operations show spinners, others show nothing, creating uncertainty about whether actions are processing.

**Current State**:
- Inconsistent loading indicators
- Some async operations have no feedback
- Mix of spinners, progress bars, and nothing
- Users uncertain if action is processing

**Expected State**:
- All async operations show loading state
- Consistent loading indicator style
- Skeleton screens for data loading
- Progress indicators for long operations

**Implementation Steps**:
1. Create LoadingIndicator component
   - Standardized spinner/progress design
   - Configurable size and position
   - Consistent with MudBlazor theme

2. Create SkeletonLoader component
   - For list/tree data loading
   - Better UX than blank space + spinner
   - Matches expected content layout

3. Audit all async operations
   - WorkItem sync operations
   - TFS connection testing
   - Settings save/load
   - PR metrics loading
   - Initial data loading

4. Add loading states to each operation
   - WorkItemExplorer data loading
   - TfsConfig save/test operations
   - PRInsight data loading and filtering
   - Settings save operations

5. Add progress indication for long operations
   - Sync operations show progress
   - "Syncing X of Y items"
   - Estimated time remaining

**Acceptance Criteria**:
- [ ] All async operations show loading state
- [ ] Consistent loading indicator design
- [ ] Skeleton screens for data loading
- [ ] Progress indicators for long operations
- [ ] No operations without feedback

**Estimated Effort**: 2 days

**Files Affected**:
- Create: PoTool.Client/Components/Common/LoadingIndicator.razor
- Create: PoTool.Client/Components/Common/SkeletonLoader.razor
- PoTool.Client/Components/WorkItems/WorkItemExplorer.razor
- PoTool.Client/Pages/TfsConfig.razor
- PoTool.Client/Pages/PullRequests/PRInsight.razor
- PoTool.Client/Components/Settings/AppSettingsDialog.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-006

---

## NICE TO HAVE (P2) - Backlog

### Issue 9: Implement Bulk Operations on Work Items

**Title**: Add bulk operations for selected work items

**Labels**: `feature`, `P2`, `enhancement`

**Description**:

Enable users to perform actions on multiple selected work items at once.

**User Story**:
As a product owner, I want to perform actions on multiple work items at once so I can save time when managing large backlogs.

**Proposed Actions**:
- Export selected items (CSV, Excel, JSON)
- Copy work item IDs to clipboard
- Copy work item URLs to clipboard
- Generate summary report of selected items
- Mark items with custom tags (future)

**Implementation Steps**:
1. Create bulk action toolbar component
   - Appears when items selected
   - Shows available actions
   - Context-sensitive actions

2. Implement export functionality
   - CSV export with selected fields
   - Excel export with formatting
   - JSON export for API integration
   - Configurable field selection

3. Implement copy to clipboard actions
   - Copy IDs as comma-separated list
   - Copy URLs for sharing
   - Copy formatted list for documents

4. Implement report generation
   - Summary of selected items
   - Distribution by type/state
   - Export as PDF/Markdown

**Acceptance Criteria**:
- [ ] Bulk action toolbar appears on selection
- [ ] Export to CSV/Excel/JSON works
- [ ] Copy IDs to clipboard works
- [ ] Copy URLs to clipboard works
- [ ] Report generation functional

**Estimated Effort**: 3-4 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-001

---

### Issue 10: Add Keyboard Shortcuts Help Panel

**Title**: Create discoverable keyboard shortcuts help panel

**Labels**: `ux`, `P2`, `enhancement`, `accessibility`

**Description**:

Keyboard navigation exists but users don't know about shortcuts, reducing power user efficiency.

**Proposed Features**:
- Help panel triggered by ? key
- Shows all available shortcuts
- Grouped by context (tree navigation, selection, etc.)
- Printable keyboard shortcut reference

**Implementation Steps**:
1. Create keyboard shortcuts component
   - Modal dialog
   - Triggered by ? key
   - Accessible from Help menu

2. Document all shortcuts
   - Tree navigation (arrows, enter, space)
   - Selection (ctrl+click, ctrl+a, escape)
   - App navigation (keyboard access)
   - Filter shortcuts

3. Add shortcut hints in tooltips
   - Show shortcut in tooltip
   - "(Ctrl+A)" in Select All tooltip

4. Implement shortcut registration system
   - Centralized shortcut management
   - Prevent conflicts
   - Allow customization (future)

**Acceptance Criteria**:
- [ ] ? key opens help panel
- [ ] All shortcuts documented
- [ ] Shortcuts shown in tooltips
- [ ] Help accessible from menu
- [ ] Shortcuts work as documented

**Estimated Effort**: 2 days

**References**:
- PRODUCT_OWNER_FINDINGS.md UX-007

---

### Issue 11: Add Work Item Quick Actions

**Title**: Implement quick actions for work items

**Labels**: `feature`, `P2`, `enhancement`

**Description**:

Add common actions directly in the UI to reduce need to switch to Azure DevOps.

**Proposed Actions**:
- Open in Azure DevOps (external link)
- Copy work item URL
- Copy work item ID
- View in browser
- Add to favorites (future)

**Implementation Steps**:
1. Add action buttons to detail panel
   - Icon buttons for common actions
   - Tooltip explains each action
   - Context menu for secondary actions

2. Implement open in Azure DevOps
   - Construct URL from config
   - Open in new browser tab
   - Handle missing URL gracefully

3. Implement copy actions
   - Copy URL to clipboard
   - Copy ID to clipboard
   - Show confirmation snackbar

4. Add context menu to tree nodes
   - Right-click menu
   - Quick actions without opening detail

**Acceptance Criteria**:
- [ ] Quick action buttons in detail panel
- [ ] Open in Azure DevOps works
- [ ] Copy URL works
- [ ] Copy ID works
- [ ] Context menu on tree nodes

**Estimated Effort**: 2 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-002

---

### Issue 12: Fix PAT Field UX (Cleared After Test)

**Title**: Improve PAT field UX - don't clear on successful test

**Labels**: `bug`, `ux`, `P2`

**Description**:

PAT field is cleared after testing connection, requiring re-entry if user wants to test again.

**Current Behavior**:
1. User enters PAT
2. Clicks "Test Connection"
3. Connection succeeds
4. PAT field cleared
5. User wants to test again → must re-enter PAT

**Expected Behavior**:
Keep PAT in field during session, only clear when:
- User navigates away from page
- User explicitly clears field
- Session ends

**Alternative Approaches**:
1. Keep PAT in memory for session
2. Show placeholder: "••••••••" after successful test
3. Add "Edit PAT" button to re-enter
4. Show message: "PAT saved securely (click to edit)"

**Implementation Steps**:
1. Modify TfsConfig.razor
   - Remove PAT clear after test
   - Keep in component state
   - Clear only on navigation away

2. Add visual indicator
   - Show "PAT configured ✓" message
   - Option to edit/change PAT
   - Clear security icon

3. Update test connection flow
   - Don't clear PAT field
   - Show success message
   - Allow immediate retest

**Acceptance Criteria**:
- [ ] PAT not cleared after successful test
- [ ] PAT cleared on navigation away
- [ ] Visual indication PAT is configured
- [ ] Can test connection multiple times

**Estimated Effort**: 0.5 days

**Files Affected**:
- PoTool.Client/Pages/TfsConfig.razor

**References**:
- PRODUCT_OWNER_FINDINGS.md BUG-004

---

## LOW PRIORITY (P3) - Future Consideration

### Issue 13: Implement Saved Filters

**Title**: Add ability to save and load filter combinations

**Labels**: `feature`, `P3`, `enhancement`

**Description**:

Allow users to save frequently used filter combinations for quick access.

**Proposed Features**:
- Save current filter state with name
- Quick-load saved filters from dropdown
- Share filters via URL/JSON
- Predefined filters: "My Items", "Blocked", "This Sprint"

**Estimated Effort**: 3 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-003

---

### Issue 14: Implement Export and Reporting

**Title**: Add export and reporting capabilities

**Labels**: `feature`, `P3`, `enhancement`

**Description**:

Export filtered data to various formats for sharing with stakeholders.

**Proposed Formats**:
- Excel/CSV with customizable columns
- PDF report with charts
- Markdown for documentation
- Chart images for presentations

**Estimated Effort**: 4-5 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-004

---

### Issue 15: Create Customizable Dashboard

**Title**: Add customizable dashboard with widgets

**Labels**: `feature`, `P3`, `enhancement`

**Description**:

Create a dashboard page with customizable widgets for at-a-glance metrics.

**Proposed Widgets**:
- Sprint burndown
- Velocity chart
- Items by state
- Blocked items count
- Team capacity
- Recent updates

**Estimated Effort**: 5-7 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-005

---

### Issue 16: Add Offline Mode Indicator

**Title**: Show clear indication of cached vs live data

**Labels**: `ux`, `P3`, `enhancement`

**Description**:

Add visual indicator showing when user is viewing cached data and when last synced.

**Proposed Features**:
- "Last synced: X minutes ago" indicator
- Warning when data is stale (> X hours)
- Auto-refresh option
- Manual refresh button

**Estimated Effort**: 1-2 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-006

---

### Issue 17: Implement Undo/Redo for Filters

**Title**: Add filter history with undo/redo capability

**Labels**: `ux`, `P3`, `enhancement`

**Description**:

Allow users to navigate backward/forward through filter changes.

**Proposed Features**:
- Back/Forward buttons
- Breadcrumb showing filter path
- Undo last filter change
- Clear all filters

**Estimated Effort**: 2 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-007

---

### Issue 18: Add Work Item Templates

**Title**: Create and use work item hierarchy templates

**Labels**: `feature`, `P3`, `enhancement`

**Description**:

Save common work item hierarchies as templates for quick project setup.

**Proposed Features**:
- Save current hierarchy as template
- Load template to create new hierarchy
- Predefined templates (Standard Feature, Sprint Planning)
- Share templates with team

**Estimated Effort**: 3-4 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-008

---

### Issue 19: Add Collaboration Features

**Title**: Enable sharing and collaboration on views

**Labels**: `feature`, `P3`, `enhancement`

**Description**:

Allow users to share filtered views and collaborate on work item analysis.

**Proposed Features**:
- Share filtered view as URL
- Collaborative annotations
- Team-wide saved filters
- Comments on work items

**Estimated Effort**: 5-7 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-009

---

### Issue 20: Implement Advanced Validation Rules

**Title**: Add configurable validation rules engine

**Labels**: `feature`, `P3`, `enhancement`

**Description**:

Allow teams to define custom validation rules beyond built-in ones.

**Proposed Rules**:
- PBIs must have acceptance criteria
- Features require minimum number of PBIs
- Story points within configured range
- Required fields enforcement
- State transition rules

**Estimated Effort**: 5-7 days

**References**:
- PRODUCT_OWNER_FINDINGS.md FEATURE-010

---

## TESTING & QUALITY (P1-P2)

### Issue 21: Add Missing Blazor Tests

**Title**: Implement missing bUnit tests for PRInsight and enhanced WorkItemExplorer coverage

**Labels**: `testing`, `P1`, `technical-debt`

**Description**:

Several components lack comprehensive bUnit test coverage.

**Missing Tests**:
1. PRInsight page (no tests currently)
   - Rendering with data
   - Empty state
   - Date filtering
   - Chart rendering
   - Sync button functionality

2. WorkItemExplorer enhancements
   - Multi-selection behavior
   - Validation filter combinations
   - Keyboard navigation flow
   - Empty state rendering

3. Settings modal interactions
   - Open/close modal
   - Save settings
   - Goal ID configuration

**Estimated Effort**: 3-4 days

**References**:
- COMPLIANCE_FINDINGS.md Section 2.2

---

### Issue 22: Improve Test Assertion Style

**Title**: Refactor tests to use modern Assert methods

**Labels**: `testing`, `P2`, `code-quality`

**Description**:

16 test assertions use deprecated `Assert.IsTrue(x.Contains())` pattern instead of `Assert.Contains()`.

**Current Pattern**:
```csharp
Assert.IsTrue(markup.Contains("expected text"));
```

**Expected Pattern**:
```csharp
Assert.Contains("expected text", markup);
```

**Files to Update**:
- WorkItemToolbarTests.cs
- WorkItemDetailPanelTests.cs
- WorkItemTreeViewTests.cs
- WorkItemTreeNodeTests.cs
- Unit test files with similar issues

**Estimated Effort**: 1 day

**References**:
- COMPLIANCE_FINDINGS.md Section 1.3

---

### Issue 23: Add Integration Tests for Error Scenarios

**Title**: Add integration tests for error handling and edge cases

**Labels**: `testing`, `P2`, `technical-debt`

**Description**:

Integration tests currently cover happy paths but miss error scenarios.

**Missing Test Coverage**:
- Invalid authentication (401)
- Authorization failures (403)
- Not found resources (404)
- Rate limiting (429)
- Server errors (500+)
- Network timeouts
- Malformed requests

**Estimated Effort**: 2-3 days

**References**:
- COMPLIANCE_FINDINGS.md Section 2.2

---

### Issue 24: Add Unit Tests for Edge Cases

**Title**: Expand unit test coverage for edge cases and boundary conditions

**Labels**: `testing`, `P2`, `technical-debt`

**Description**:

Unit tests could be expanded to cover more edge cases.

**Missing Coverage**:
- TfsClient implementation details
- WorkItemParentProgressValidator edge cases
- TreeBuilderService with complex hierarchies
- Empty/null input handling
- Large dataset scenarios

**Estimated Effort**: 2-3 days

**References**:
- COMPLIANCE_FINDINGS.md Section 2.2

---

## ACCESSIBILITY (P2)

### Issue 25: Add Missing ARIA Labels

**Title**: Improve accessibility with comprehensive ARIA labels

**Labels**: `accessibility`, `P2`, `a11y`

**Description**:

Many interactive elements lack ARIA labels, impacting screen reader users.

**Implementation Steps**:
1. Audit all interactive elements
2. Add aria-label to buttons without visible text
3. Add aria-describedby for form fields
4. Add role attributes where needed
5. Test with screen reader

**Estimated Effort**: 2 days

**References**:
- PRODUCT_OWNER_FINDINGS.md A11Y-001

---

### Issue 26: Add Visual Patterns to Color-Coded Elements

**Title**: Add icons/patterns in addition to color for work item types

**Labels**: `accessibility`, `P2`, `a11y`

**Description**:

Work item types are distinguished only by color, creating issues for color-blind users.

**Current State**:
- Epic: Blue
- Feature: Purple
- PBI: Green
- Task: Orange

**Proposed Solution**:
Add icons in addition to colors:
- Epic: 📊 icon + blue
- Feature: ⭐ icon + purple
- PBI: 📝 icon + green
- Task: ✓ icon + orange

**Estimated Effort**: 1 day

**References**:
- PRODUCT_OWNER_FINDINGS.md A11Y-002

---

### Issue 27: Ensure High Contrast Focus Indicators

**Title**: Verify and enhance focus indicators for keyboard navigation

**Labels**: `accessibility`, `P2`, `a11y`

**Description**:

Ensure focus indicators are visible in dark theme for keyboard users.

**Implementation Steps**:
1. Audit all focusable elements
2. Test focus visibility in dark theme
3. Enhance focus rings where needed
4. Ensure minimum 3:1 contrast ratio
5. Test with keyboard only navigation

**Estimated Effort**: 1 day

**References**:
- PRODUCT_OWNER_FINDINGS.md A11Y-003

---

## PERFORMANCE (P3)

### Issue 28: Implement Virtual Scrolling for Large Trees

**Title**: Add virtual scrolling to improve performance with 1000+ work items

**Labels**: `performance`, `P3`, `enhancement`

**Description**:

Rendering large trees (1000+ items) could be slow without virtual scrolling.

**Implementation**:
- Use virtualization for tree rendering
- Only render visible nodes
- Lazy load children on expand
- Measure performance improvement

**Estimated Effort**: 3-4 days

**References**:
- PRODUCT_OWNER_FINDINGS.md PERF-001

---

### Issue 29: Add Filter Debouncing

**Title**: Debounce text filter to reduce re-renders

**Labels**: `performance`, `P3`, `enhancement`

**Description**:

Text filter triggers on every keystroke, causing unnecessary re-renders.

**Implementation**:
- Add 300ms debounce to filter input
- Show loading indicator during debounce
- Cancel pending filter if new input arrives

**Estimated Effort**: 0.5 days

**References**:
- PRODUCT_OWNER_FINDINGS.md PERF-002

---

### Issue 30: Memoize Chart Data Calculations

**Title**: Optimize chart rendering by memoizing data calculations

**Labels**: `performance`, `P3`, `enhancement`

**Description**:

Charts re-render on every state change, even when data hasn't changed.

**Implementation**:
- Memoize chart data calculations
- Only recalculate when source data changes
- Measure performance improvement

**Estimated Effort**: 1 day

**References**:
- PRODUCT_OWNER_FINDINGS.md PERF-003

---

## SECURITY (P1)

### Issue 31: Run CodeQL Security Scan

**Title**: Perform CodeQL security analysis on codebase

**Labels**: `security`, `P1`, `technical-debt`

**Description**:

Security scan needed to verify PAT encryption and identify potential vulnerabilities.

**Scope**:
- PAT encryption implementation
- Error message sanitization
- Input validation
- SQL injection prevention
- XSS prevention
- Authentication/authorization

**Implementation Steps**:
1. Set up CodeQL in CI/CD
2. Run initial scan
3. Analyze results
4. Fix identified issues
5. Document security posture

**Estimated Effort**: 2-3 days

**References**:
- COMPLIANCE_FINDINGS.md Section 4
- EXECUTIVE_SUMMARY.md Security Considerations

---

### Issue 32: Sanitize Error Messages to Prevent Information Leakage

**Title**: Review and sanitize error messages for security

**Labels**: `security`, `P1`

**Description**:

Some error messages might expose sensitive information like URLs, paths, or stack traces.

**Scope**:
- Remove stack traces from user-facing errors
- Sanitize URLs in error messages
- Remove internal paths
- Log details server-side only

**Estimated Effort**: 1 day

**References**:
- PRODUCT_OWNER_FINDINGS.md SEC-002

---

### Issue 33: Minimize PAT Exposure Time

**Title**: Review and minimize PAT storage in memory

**Labels**: `security`, `P2`

**Description**:

Ensure PAT is only kept in memory as long as needed and cleared on component disposal.

**Scope**:
- Review PAT lifecycle
- Clear PAT from memory when no longer needed
- Implement secure disposal
- Verify no PAT leaks in logs

**Estimated Effort**: 1 day

**References**:
- PRODUCT_OWNER_FINDINGS.md SEC-001

---

## Summary

**Total Issues**: 33

**By Priority**:
- P0 (High Priority): 3 issues - ~8-12 days
- P1 (Medium Priority): 12 issues - ~20-25 days
- P2 (Nice to Have): 7 issues - ~10-15 days
- P3 (Future): 11 issues - ~35-50 days

**By Category**:
- Architecture/Technical Debt: 4 issues
- UX/User Experience: 13 issues
- Features: 10 issues
- Testing: 4 issues
- Accessibility: 3 issues
- Performance: 3 issues
- Security: 3 issues
- Bug Fixes: 1 issue

**Recommended Implementation Order**:
1. Issue 1 (NSwag Migration) - Highest priority architectural fix
2. Issue 2 (Error Messages) - Critical UX improvement
3. Issue 3 (Onboarding) - Critical for first-time users
4. Issue 31 (CodeQL) - Security verification
5. Issues 4-8 - P1 UX improvements
6. Remaining issues based on business value

---

**Document Prepared**: 2025-12-20
**Source**: Compliance and Quality Assurance Review
**References**: COMPLIANCE_FINDINGS.md, PRODUCT_OWNER_FINDINGS.md, EXECUTIVE_SUMMARY.md
