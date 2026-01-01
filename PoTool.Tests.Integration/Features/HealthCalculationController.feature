Feature: Health Calculation Controller API
    As a user
    I want to calculate backlog health scores through the API
    So that I can assess iteration health

Background:
    Given the application is running

Scenario: Calculate health score with no issues
    When I request health score calculation with
        | TotalWorkItems | WorkItemsWithoutEffort | WorkItemsInProgressWithoutEffort | ParentProgressIssues | BlockedItems |
        | 10             | 0                      | 0                                | 0                    | 0            |
    Then the health calculation response should be OK
    And the health score should be 100

Scenario: Calculate health score with some issues
    When I request health score calculation with
        | TotalWorkItems | WorkItemsWithoutEffort | WorkItemsInProgressWithoutEffort | ParentProgressIssues | BlockedItems |
        | 10             | 2                      | 0                                | 1                    | 0            |
    Then the health calculation response should be OK
    And the health score should be 70

Scenario: Calculate health score with all items having issues
    When I request health score calculation with
        | TotalWorkItems | WorkItemsWithoutEffort | WorkItemsInProgressWithoutEffort | ParentProgressIssues | BlockedItems |
        | 10             | 5                      | 3                                | 1                    | 1            |
    Then the health calculation response should be OK
    And the health score should be 0

Scenario: Calculate health score with no work items
    When I request health score calculation with
        | TotalWorkItems | WorkItemsWithoutEffort | WorkItemsInProgressWithoutEffort | ParentProgressIssues | BlockedItems |
        | 0              | 0                      | 0                                | 0                    | 0            |
    Then the health calculation response should be OK
    And the health score should be 100

Scenario: Calculate health score with more issues than items
    When I request health score calculation with
        | TotalWorkItems | WorkItemsWithoutEffort | WorkItemsInProgressWithoutEffort | ParentProgressIssues | BlockedItems |
        | 5              | 3                      | 2                                | 1                    | 1            |
    Then the health calculation response should be OK
    And the health score should be greater than or equal to 0
