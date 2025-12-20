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
    And I should receive at least 2 work items
    And all returned work items should be of type "Goal"
