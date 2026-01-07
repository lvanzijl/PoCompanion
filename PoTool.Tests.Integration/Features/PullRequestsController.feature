Feature: Pull Requests Controller API
    As a user
    I want to access pull requests through the API
    So that I can view and analyze my code reviews

Background:
    Given the application is running
    And pull requests exist in the database
        | PullRequestId | Title                  | Status    | CreatedBy | CreatedDate | RepositoryName |
        | 1             | Feature A              | Active    | Alice     | 2024-01-15  | MainRepo       |
        | 2             | Bug Fix B              | Completed | Bob       | 2024-01-20  | MainRepo       |
        | 3             | Refactor C             | Active    | Charlie   | 2024-01-25  | SecondRepo     |

Scenario: Get all pull requests from controller
    When I request all pull requests from "/api/pullrequests"
    Then the response should be OK
    And I should receive at least 3 pull requests

Scenario: Get pull request by ID via controller
    When I request pull request 1 from controller
    Then the response should be OK
    And the pull request should have title "Feature A"

Scenario: Get non-existent pull request via controller
    When I request pull request 99999 from controller
    Then the response should be NotFound

Scenario: Get pull request metrics
    When I request pull request metrics from "/api/pullrequests/metrics"
    Then the response should be OK
    And the metrics should contain aggregated data

Scenario: Get filtered pull requests with iteration path
    When I request filtered pull requests with iterationPath "Iteration1"
    Then the response should be OK

Scenario: Get filtered pull requests with created by
    When I request filtered pull requests with createdBy "Alice"
    Then the response should be OK

Scenario: Get filtered pull requests with date range
    When I request filtered pull requests from "2024-01-01" to "2024-01-31"
    Then the response should be OK

Scenario: Get filtered pull requests with status
    When I request filtered pull requests with status "Active"
    Then the response should be OK

Scenario: Get filtered pull requests with all parameters
    When I request filtered pull requests with all parameters
        | IterationPath | CreatedBy | FromDate   | ToDate     | Status |
        | Iteration1    | Alice     | 2024-01-01 | 2024-12-31 | Active |
    Then the response should be OK

Scenario: Get pull request iterations
    When I request pull request 1 iterations
    Then the response should be OK

Scenario: Get pull request comments
    When I request pull request 1 comments
    Then the response should be OK

Scenario: Get pull request file changes
    When I request pull request 1 file changes
    Then the response should be OK

Scenario: Get PR review bottleneck with default parameters
    When I request PR review bottleneck analysis
    Then the response should be OK

Scenario: Get PR review bottleneck with custom maxPRs
    When I request PR review bottleneck with maxPRs 50 and daysBack 15
    Then the response should be OK

Scenario: Get PR review bottleneck with invalid maxPRs too low
    When I request PR review bottleneck with maxPRs 0 and daysBack 30
    Then the response should be BadRequest

Scenario: Get PR review bottleneck with invalid maxPRs too high
    When I request PR review bottleneck with maxPRs 600 and daysBack 30
    Then the response should be BadRequest

Scenario: Get PR review bottleneck with invalid daysBack too low
    When I request PR review bottleneck with maxPRs 100 and daysBack 0
    Then the response should be BadRequest

Scenario: Get PR review bottleneck with invalid daysBack too high
    When I request PR review bottleneck with maxPRs 100 and daysBack 400
    Then the response should be BadRequest
