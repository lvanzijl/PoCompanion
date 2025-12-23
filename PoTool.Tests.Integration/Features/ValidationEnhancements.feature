Feature: Validation Enhancements API
    As a user
    I want to access validation violation history and impact analysis through the API
    So that I can understand and fix validation issues in my backlog

Background:
    Given the application is running
    And work items exist in the database with validation violations
        | TfsId | Title              | Type    | State       | ParentTfsId | AreaPath    | IterationPath     |
        | 3000  | Goal A             | Goal    | New         |             | TestArea    | TestIteration1    |
        | 3001  | Epic A1            | Epic    | In Progress | 3000        | TestArea    | TestIteration1    |
        | 3002  | Feature A1a        | Feature | In Progress | 3001        | TestArea    | TestIteration1    |
        | 3010  | Goal B             | Goal    | In Progress |             | OtherArea   | TestIteration2    |
        | 3011  | Epic B1            | Epic    | In Progress | 3010        | OtherArea   | TestIteration2    |

Scenario: Get validation violation history
    When I request validation history from "/api/workitems/validation-history"
    Then the response should be OK
    And I should receive validation history records
    And the history should include violations for work item 3001

Scenario: Get validation violation history with area path filter
    When I request validation history with area path filter "TestArea"
    Then the response should be OK
    And all history records should have area path starting with "TestArea"

Scenario: Get validation violation history with date filter
    When I request validation history with start date "2024-01-01"
    Then the response should be OK
    And all history records should be after start date

Scenario: Get validation impact analysis
    When I request validation impact analysis from "/api/workitems/validation-impact-analysis"
    Then the response should be OK
    And I should receive impact analysis with violations
    And the analysis should include recommendations

Scenario: Get validation impact analysis with area filter
    When I request validation impact analysis with area filter "TestArea"
    Then the response should be OK
    And all violations should be from area path "TestArea"

Scenario: Fix validation violations in batch
    Given I have fix suggestions for violations
        | WorkItemId | FixType          | NewState    | Description              |
        | 3000       | SetToInProgress  | In Progress | Set Goal A to In Progress |
    When I send batch fix request to "/api/workitems/fix-validation-violations"
    Then the response should be OK
    And the fix result should show 1 successful fix
    And the fix result should show 0 failed fixes

Scenario: Fix validation violations with non-existent work item
    Given I have fix suggestions for violations
        | WorkItemId | FixType          | NewState    | Description                  |
        | 99999      | SetToInProgress  | In Progress | Set non-existent to In Progress |
    When I send batch fix request to "/api/workitems/fix-validation-violations"
    Then the response should be OK
    And the fix result should show 0 successful fixes
    And the fix result should show 1 failed fix

Scenario: Fix validation violations with empty request
    When I send empty batch fix request to "/api/workitems/fix-validation-violations"
    Then the response should be BadRequest

Scenario: Fix multiple validation violations
    Given I have fix suggestions for violations
        | WorkItemId | FixType          | NewState    | Description                  |
        | 3000       | SetToInProgress  | In Progress | Set Goal A to In Progress     |
        | 3010       | SetToInProgress  | In Progress | Set Goal B to In Progress     |
    When I send batch fix request to "/api/workitems/fix-validation-violations"
    Then the response should be OK
    And the fix result should show 2 successful fixes
    And the fix result should show 0 failed fixes
