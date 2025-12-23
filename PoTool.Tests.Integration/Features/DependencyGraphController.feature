Feature: Dependency Graph API
    As a user
    I want to access dependency graph through the API
    So that I can visualize work item dependencies and detect circular dependencies

Background:
    Given the application is running

Scenario: Get dependency graph with basic work items
    Given work items with dependencies exist
        | TfsId | Title     | Type    | State | Effort | AreaPath      | JsonPayload                                                                                                                                       |
        | 1001  | Epic 1    | Epic    | New   | 20     | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Hierarchy-Forward","url":"http://tfs/workItems/1002"}]}                                                     |
        | 1002  | Feature 1 | Feature | New   | 10     | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Hierarchy-Reverse","url":"http://tfs/workItems/1001"},{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/1003"}]} |
        | 1003  | Task 1    | Task    | New   | 5      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Reverse","url":"http://tfs/workItems/1002"}]}                                                    |
    When I request dependency graph with no filters
    Then the response should be OK
    And the dependency graph should have 3 nodes
    And the dependency graph should have links

Scenario: Get dependency graph filtered by area path
    Given work items with dependencies exist
        | TfsId | Title     | Type    | State | Effort | AreaPath      | JsonPayload                                                                                                                   |
        | 2001  | Epic 1    | Epic    | New   | 20     | Project\TeamA | {"relations":[]}                                                                                                               |
        | 2002  | Epic 2    | Epic    | New   | 15     | Project\TeamB | {"relations":[]}                                                                                                               |
    When I request dependency graph with areaPathFilter "TeamA"
    Then the response should be OK
    And the dependency graph should have 1 nodes

Scenario: Get dependency graph filtered by work item type
    Given work items with dependencies exist
        | TfsId | Title     | Type    | State | Effort | AreaPath      | JsonPayload                                                                                                                   |
        | 3001  | Epic 1    | Epic    | New   | 20     | Project\TeamA | {"relations":[]}                                                                                                               |
        | 3002  | Feature 1 | Feature | New   | 10     | Project\TeamA | {"relations":[]}                                                                                                               |
        | 3003  | Task 1    | Task    | New   | 5      | Project\TeamA | {"relations":[]}                                                                                                               |
    When I request dependency graph with workItemTypes "Epic,Feature"
    Then the response should be OK
    And the dependency graph should have 2 nodes

Scenario: Get dependency graph filtered by specific work item IDs
    Given work items with dependencies exist
        | TfsId | Title     | Type    | State | Effort | AreaPath      | JsonPayload                                                                                                                   |
        | 4001  | Epic 1    | Epic    | New   | 20     | Project\TeamA | {"relations":[]}                                                                                                               |
        | 4002  | Epic 2    | Epic    | New   | 15     | Project\TeamA | {"relations":[]}                                                                                                               |
        | 4003  | Epic 3    | Epic    | New   | 10     | Project\TeamA | {"relations":[]}                                                                                                               |
    When I request dependency graph with workItemIds "4001,4003"
    Then the response should be OK
    And the dependency graph should have 2 nodes

Scenario: Get dependency graph with circular dependencies
    Given work items with circular dependencies exist
        | TfsId | Title  | Type | State | Effort | AreaPath      | JsonPayload                                                                                                                   |
        | 5001  | Task A | Task | New   | 5      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/5002"}]}                               |
        | 5002  | Task B | Task | New   | 5      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/5003"}]}                               |
        | 5003  | Task C | Task | New   | 5      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/5001"}]}                               |
    When I request dependency graph with no filters
    Then the response should be OK
    And the dependency graph should have 3 nodes
    And the dependency graph should have circular dependencies

Scenario: Get dependency graph with critical paths
    Given work items with long dependency chain exist
        | TfsId | Title  | Type | State | Effort | AreaPath      | JsonPayload                                                                                                                   |
        | 6001  | Task 1 | Task | New   | 5      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/6002"}]}                               |
        | 6002  | Task 2 | Task | New   | 8      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/6003"}]}                               |
        | 6003  | Task 3 | Task | New   | 10     | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/6004"}]}                               |
        | 6004  | Task 4 | Task | New   | 15     | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/6005"}]}                               |
        | 6005  | Task 5 | Task | New   | 20     | Project\TeamA | {"relations":[]}                                                                                                               |
    When I request dependency graph with no filters
    Then the response should be OK
    And the dependency graph should have 5 nodes
    And the dependency graph should have critical paths

Scenario: Get dependency graph with blocking work items
    Given work items with blocking relationships exist
        | TfsId | Title     | Type | State | Effort | AreaPath      | JsonPayload                                                                                                                   |
        | 7001  | Blocker   | Task | New   | 10     | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Reverse","url":"http://tfs/workItems/7002"}]}                               |
        | 7002  | Dependent | Task | New   | 5      | Project\TeamA | {"relations":[{"rel":"System.LinkTypes.Dependency-Forward","url":"http://tfs/workItems/7001"}]}                               |
    When I request dependency graph with no filters
    Then the response should be OK
    And the dependency graph should have blocked work items

Scenario: Get dependency graph with invalid work item IDs format
    When I request dependency graph with invalid workItemIds "abc,xyz"
    Then the response should be BadRequest
