#!/bin/bash
# Script to create GitHub issues for test fixes
# Usage: ./create_test_issues.sh

set -e

# Check if gh CLI is available
if ! command -v gh &> /dev/null; then
    echo "Error: GitHub CLI (gh) is not installed"
    exit 1
fi

# Check if we're in a git repository
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    echo "Error: Not in a git repository"
    exit 1
fi

echo "Creating GitHub issues for test fixes..."
echo ""

# Issue 1: Fix failing integration tests
echo "Creating Issue 1: Fix failing integration tests (12 tests)"
gh issue create \
  --title "Fix failing integration tests (12 tests)" \
  --label "test,bug,integration-tests" \
  --body "## Overview
12 integration tests are currently failing. The integration test infrastructure is now functional (fixed in recent PR), but the test scenarios themselves need investigation and fixes.

## Failing Tests

### Settings Management (3 tests)
- \`GetSettingsWhenNoneExist\`
- \`GetSavedSettings\`
- \`UpdateSettings\`

### Health Check (1 test)
- \`CheckApplicationHealth\`

### Work Items Controller (8 tests)
- \`GetAllWorkItemsFromController\`
- \`GetWorkItemByTFSIDViaController\`
- \`GetNon_ExistentWorkItemViaController\`
- \`GetFilteredWorkItems\`
- \`GetGoalHierarchy\`
- \`GetGoalHierarchyWithInvalidFormat\`
- \`GetGoalHierarchyWithNegativeID\`
- \`UpdateSettingsWithMultipleGoalIDs\`

## Root Cause
These are likely test assertion or test data setup issues, not application code issues. The integration test infrastructure is working correctly.

## Test Location
- **Project:** \`PoTool.Tests.Integration\`
- **Framework:** Reqnroll (BDD/Gherkin)
- **Test runner:** MSTest
- **Features:** Located in \`PoTool.Tests.Integration/Features/\` directory

## Investigation Steps
1. Run individual failing tests to get detailed error messages
2. Check test data setup in step definitions
3. Verify API endpoint behavior matches test expectations
4. Check for timing issues or async problems
5. Validate response assertion logic

## Acceptance Criteria
- [ ] All 12 integration tests pass
- [ ] Test assertions are correct
- [ ] Test data setup is correct
- [ ] No changes to application code (unless actual bugs are found)
- [ ] Add explanatory comments if test expectations were incorrect

## Current Status
- **Passing:** 8 out of 24 integration tests (33%)
- **Failing:** 12 tests
- **Skipped:** 4 tests (TFS sync operations - intentional)

## Related
- Integration test infrastructure was fixed in PR that resolved DbContext provider conflict
- All unit tests are passing (31/31 = 100%)"

echo "✓ Issue 1 created"
echo ""

# Issue 2: Fix failing Blazor tests
echo "Creating Issue 2: Fix failing Blazor component tests"
gh issue create \
  --title "Fix failing Blazor component tests - event simulation issues" \
  --label "test,bug,blazor,ui-tests" \
  --body "## Overview
2 Blazor component tests are failing due to bUnit event simulation methodology issues, not component code issues.

## Failing Tests

### WorkItemToolbar Tests
- **Test:** \`WorkItemToolbar_FilterInput_InvokesCallback\`
- **Location:** \`PoTool.Tests.Blazor/WorkItemToolbarTests.cs\`
- **Issue:** Test uses \`InputAsync\` which triggers \`input\` event, but component uses \`@onkeyup\` event
- **Component:** \`PoTool.Client/Components/WorkItems/SubComponents/WorkItemToolbar.razor\`

### WorkItemTreeNode Tests
- **Test:** \`WorkItemTreeNode_Click_InvokesSelectionCallback\`
- **Location:** \`PoTool.Tests.Blazor/WorkItemTreeNodeTests.cs\`
- **Issue:** Test uses \`.Click()\` on title span but callback is not being properly invoked in test
- **Component:** \`PoTool.Client/Components/WorkItems/SubComponents/WorkItemTreeNode.razor\`

## Root Cause
These are bUnit test framework event simulation issues. The components work correctly in the actual application, but the tests don't properly simulate the DOM events that trigger the callbacks.

## Test Location
- **Project:** \`PoTool.Tests.Blazor\`
- **Framework:** bUnit with MSTest
- **Test files:** 
  - \`WorkItemToolbarTests.cs\` (line ~60-81)
  - \`WorkItemTreeNodeTests.cs\` (line ~110-135)

## Solution Approaches

### For WorkItemToolbar filter test:
Instead of:
\`\`\`csharp
await filterInput.InputAsync(new ChangeEventArgs { Value = \"test filter\" });
\`\`\`

Use:
\`\`\`csharp
await filterInput.TriggerEventAsync(\"onkeyup\", new KeyboardEventArgs());
\`\`\`

### For WorkItemTreeNode click test:
Ensure the click event properly bubbles through the component's event handling chain. May need to use \`InvokeAsync\` or verify the component is fully rendered.

## Acceptance Criteria
- [ ] Both Blazor component tests pass
- [ ] Test methodology correctly simulates user interactions
- [ ] No changes to component code (components are working correctly)
- [ ] Add comments explaining the correct bUnit event simulation pattern

## Current Status
- **Passing:** 19 out of 24 Blazor tests (79%)
- **Failing:** 2 tests (event simulation issues)
- **Skipped:** 3 tests (TfsConfig component - incomplete)

## Related
- Components work correctly in actual application
- This is a test framework usage issue, not a component bug
- All component rendering tests pass"

echo "✓ Issue 2 created"
echo ""

# Issue 3: Complete skipped integration tests
echo "Creating Issue 3: Complete skipped integration tests"
gh issue create \
  --title "Complete skipped integration tests for TFS sync operations" \
  --label "test,enhancement,integration-tests" \
  --body "## Overview
4 integration tests are currently skipped. These tests cover TFS sync operations and need to be completed.

## Skipped Tests
- \`SyncWorkItemsFromTFS\`
- \`GetAllWorkItems\`
- \`GetWorkItemByID\`
- \`GetNon_ExistentWorkItem\`

## Location
- **Project:** \`PoTool.Tests.Integration\`
- **Feature file:** \`PoTool.Tests.Integration/Features/WorkItems.feature\`

## Why Skipped
These tests likely require more complex TFS mock setup or were intentionally skipped during initial development.

## Implementation Steps
1. Review the feature file to understand test scenarios
2. Implement missing step definitions
3. Ensure MockTfsClient provides appropriate test data
4. Verify tests pass with mock data

## Acceptance Criteria
- [ ] All 4 skipped tests are implemented
- [ ] Tests use MockTfsClient (no real TFS calls)
- [ ] Tests pass consistently
- [ ] Test scenarios cover happy path and error cases

## Current Status
- **Total integration tests:** 24
- **Skipped:** 4 tests
- These tests are intentionally skipped, not failing"

echo "✓ Issue 3 created"
echo ""

# Issue 4: Complete skipped Blazor tests
echo "Creating Issue 4: Complete skipped Blazor tests"
gh issue create \
  --title "Complete skipped Blazor tests for TfsConfig component" \
  --label "test,enhancement,blazor,ui-tests" \
  --body "## Overview
3 Blazor component tests for the TfsConfig component are currently skipped and need to be completed.

## Skipped Tests
- \`TfsConfig_RendersFormElements\`
- \`TfsConfig_DisplaysSaveButton\`
- \`TfsConfig_LoadsExistingConfiguration\`

## Location
- **Project:** \`PoTool.Tests.Blazor\`
- **Component:** \`PoTool.Client/Pages/TfsConfig.razor\`

## Why Skipped
These tests were likely skipped during initial development when the TfsConfig component was being built.

## Implementation Steps
1. Review the TfsConfig component implementation
2. Write bUnit tests for form element rendering
3. Test save button display and functionality
4. Test configuration loading from service

## Acceptance Criteria
- [ ] All 3 skipped tests are implemented
- [ ] Tests verify form elements render correctly
- [ ] Tests verify save button functionality
- [ ] Tests verify configuration loading
- [ ] Tests follow existing bUnit patterns in the project

## Current Status
- **Total Blazor tests:** 24
- **Skipped:** 3 tests
- These tests are intentionally skipped, not failing"

echo "✓ Issue 4 created"
echo ""

echo "════════════════════════════════════════════════════════════"
echo "All 4 issues created successfully!"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "Summary:"
echo "  • Issue 1: Fix 12 failing integration tests (HIGH PRIORITY)"
echo "  • Issue 2: Fix 2 failing Blazor tests (HIGH PRIORITY)"
echo "  • Issue 3: Complete 4 skipped integration tests (MEDIUM PRIORITY)"
echo "  • Issue 4: Complete 3 skipped Blazor tests (MEDIUM PRIORITY)"
echo ""
echo "Next steps:"
echo "  1. Review the created issues on GitHub"
echo "  2. Assign issues to team members as appropriate"
echo "  3. Add any additional labels or milestones"
echo ""
