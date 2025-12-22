Feature: Work Items Controller API
    As a user
    I want to access work items through the API
    So that I can view and manage my backlog

Background:
    Given the application is running
    And work items exist in the database
        | TfsId | Title          | Type      | State  |
        | 1000  | Test Goal      | Goal      | Active |
        | 1001  | Test Objective | Objective | Active |
        | 1002  | Test Epic      | Epic      | New    |

Scenario: Get all work items from controller
    When I request all work items from "/api/workitems"
    Then the response should be OK
    And I should receive at least 3 work items

Scenario: Get work item by TFS ID via controller
    When I request work item 1000 from controller
    Then the response should be OK
    And the work item should have title "Test Goal"
    And the work item should have type "Goal"

Scenario: Get non-existent work item via controller
    When I request work item 99999 from controller
    Then the response should be NotFound

Scenario: Get filtered work items
    When I request filtered work items with filter "Goal"
    Then the response should be OK
    And the results should contain work items matching "Goal"

Scenario: Get goal hierarchy
    When I request goal hierarchy for IDs "1000"
    Then the response should be OK
    And the hierarchy should include descendants of goal 1000

Scenario: Get goal hierarchy with invalid format
    When I request goal hierarchy for IDs "invalid"
    Then the response should be BadRequest

Scenario: Get goal hierarchy with negative ID
    When I request goal hierarchy for IDs "-1"
    Then the response should be BadRequest

Scenario: Get work items with validation results
    Given work items exist in the database with parent-child relationships
        | TfsId | Title            | Type      | State       | ParentTfsId |
        | 2000  | Parent Goal      | Goal      | New         |             |
        | 2001  | Child in Progress| Objective | In Progress | 2000        |
    When I request all work items with validation from "/api/workitems/validated"
    Then the response should be OK
    And work item 2001 should have validation errors about parent not in progress

Scenario: Get valid work items hierarchy with validation
    Given work items exist in the database with parent-child relationships
        | TfsId | Title            | Type      | State       | ParentTfsId | Effort |
        | 3000  | Parent Goal      | Goal      | In Progress |             | 10     |
        | 3001  | Child in Progress| Objective | In Progress | 3000        | 8      |
    When I request all work items with validation from "/api/workitems/validated"
    Then the response should be OK
    And work item 3001 should have no validation issues

Scenario: Get all goals from controller
    Given the application is running
    And work items exist in the database
        | TfsId | Title          | Type      | State  |
        | 4000  | Goal One       | Goal      | Active |
        | 4001  | Goal Two       | Goal      | New    |
        | 4002  | Test Objective | Objective | Active |
        | 4003  | Test Epic      | Epic      | New    |
    When I request all goals from "/api/workitems/goals/all"
    Then the response should be OK
    And all returned work items should be of type "Goal"

Scenario: Get filtered work items with different filters
    Given work items exist in the database
        | TfsId | Title          | Type      | State  |
        | 5000  | Epic Story     | Epic      | Active |
        | 5001  | Task Story     | Task      | New    |
        | 5002  | Feature Story  | Feature   | Done   |
    When I request filtered work items with filter "Epic"
    Then the response should be OK
    And the results should contain work items matching "Epic"

Scenario: Get filtered work items with empty filter
    When I request filtered work items with filter ""
    Then the response should be OK

Scenario: Get goal hierarchy with multiple IDs
    Given work items exist in the database
        | TfsId | Title     | Type      | State  |
        | 6000  | Goal A    | Goal      | Active |
        | 6001  | Goal B    | Goal      | Active |
    When I request goal hierarchy for IDs "6000,6001"
    Then the response should be OK

Scenario: Get goal hierarchy with zero ID
    When I request goal hierarchy for IDs "0"
    Then the response should be BadRequest

Scenario: Get goal hierarchy with overflow ID
    When I request goal hierarchy for IDs "999999999999999999999"
    Then the response should be BadRequest

Scenario: Get goal hierarchy with empty IDs
    When I request goal hierarchy for IDs ""
    Then the response should be BadRequest

Scenario: Get work item revisions
    Given work item revisions exist
        | WorkItemId | Revision | ChangedDate | ChangedBy |
        | 1000       | 1        | 2024-01-01  | Alice     |
        | 1000       | 2        | 2024-01-15  | Bob       |
    When I request work item 1000 revisions
    Then the response should be OK
    And I should receive revision history

Scenario: Get work item revisions for non-existent work item
    When I request work item 99999 revisions
    Then the response should be OK
    And I should receive empty revision list

Scenario: Get work item state timeline
    Given work item state timeline exists
        | WorkItemId | State       | EnteredDate | ExitedDate |
        | 1000       | New         | 2024-01-01  | 2024-01-05 |
        | 1000       | Active      | 2024-01-05  | 2024-01-15 |
        | 1000       | In Progress | 2024-01-15  |            |
    When I request work item 1000 state timeline
    Then the response should be OK
    And I should receive state timeline data

Scenario: Get work item state timeline for non-existent work item
    When I request work item 99999 state timeline
    Then the response should be NotFound

Scenario: Get work item by zero TFS ID
    When I request work item 0 from controller
    Then the response should be NotFound

Scenario: Get work item by negative TFS ID
    When I request work item -1 from controller
    Then the response should be NotFound
