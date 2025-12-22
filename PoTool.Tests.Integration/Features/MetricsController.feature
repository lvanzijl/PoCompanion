Feature: Metrics Controller API
    As a user
    I want to access metrics through the API
    So that I can view sprint and velocity data

Background:
    Given the application is running

Scenario: Get sprint metrics with valid iteration path
    Given work items exist for iteration "Project\2024\Sprint1"
        | TfsId | Title    | Type | State | Effort | IterationPath       |
        | 5000  | Story 1  | Task | Done  | 5      | Project\2024\Sprint1 |
        | 5001  | Story 2  | Task | New   | 3      | Project\2024\Sprint1 |
    When I request sprint metrics for iteration "Project\2024\Sprint1"
    Then the response should be OK
    And the sprint metrics should have data

Scenario: Get sprint metrics with empty iteration path
    When I request sprint metrics with empty iteration path
    Then the response should be BadRequest

Scenario: Get sprint metrics for non-existent iteration
    When I request sprint metrics for iteration "NonExistent\Sprint"
    Then the response should be NotFound

Scenario: Get velocity trend with default parameters
    Given work items exist for multiple iterations
        | TfsId | Title    | Type | State | Effort | IterationPath           |
        | 6000  | Story 1  | Task | Done  | 5      | Project\2024\Sprint1  |
        | 6001  | Story 2  | Task | Done  | 3      | Project\2024\Sprint2  |
        | 6002  | Story 3  | Task | Done  | 8      | Project\2024\Sprint3  |
    When I request velocity trend with default parameters
    Then the response should be OK

Scenario: Get velocity trend with custom maxSprints
    When I request velocity trend with maxSprints 5
    Then the response should be OK

Scenario: Get velocity trend with maxSprints below minimum
    When I request velocity trend with maxSprints 0
    Then the response should be BadRequest

Scenario: Get velocity trend with maxSprints above maximum
    When I request velocity trend with maxSprints 100
    Then the response should be BadRequest

Scenario: Get velocity trend with area path filter
    When I request velocity trend with areaPath "Project\TeamA"
    Then the response should be OK

Scenario: Get backlog health with valid iteration path
    Given work items exist for iteration "Project\2024\Sprint1"
        | TfsId | Title    | Type | State | Effort | IterationPath           |
        | 7000  | Story 1  | Task | Done  | 5      | Project\2024\Sprint1  |
        | 7001  | Story 2  | Task | New   | 3      | Project\2024\Sprint1  |
    When I request backlog health for iteration "Project\2024\Sprint1"
    Then the response should be OK

Scenario: Get backlog health with empty iteration path
    When I request backlog health with empty iteration path
    Then the response should be BadRequest

Scenario: Get backlog health for non-existent iteration
    When I request backlog health for iteration "NonExistent\\Sprint"
    Then the response should be NotFound

Scenario: Get multi-iteration backlog health with default parameters
    Given work items exist for multiple iterations
        | TfsId | Title    | Type | State | Effort | IterationPath           |
        | 8000  | Story 1  | Task | Done  | 5      | Project\2024\Sprint1  |
        | 8001  | Story 2  | Task | New   | 3      | Project\2024\Sprint2  |
    When I request multi-iteration backlog health
    Then the response should be OK

Scenario: Get multi-iteration backlog health with custom maxIterations
    When I request multi-iteration backlog health with maxIterations 10
    Then the response should be OK

Scenario: Get multi-iteration backlog health with maxIterations below minimum
    When I request multi-iteration backlog health with maxIterations 0
    Then the response should be BadRequest

Scenario: Get multi-iteration backlog health with maxIterations above maximum
    When I request multi-iteration backlog health with maxIterations 50
    Then the response should be BadRequest

Scenario: Get multi-iteration backlog health with area path filter
    When I request multi-iteration backlog health with areaPath "Project\TeamA"
    Then the response should be OK

Scenario: Get effort distribution with default parameters
    Given work items exist for multiple iterations
        | TfsId | Title    | Type | State | Effort | IterationPath           | AreaPath       |
        | 9000  | Story 1  | Task | Done  | 5      | Project\2024\Sprint1  | Project\TeamA |
        | 9001  | Story 2  | Task | Done  | 3      | Project\2024\Sprint1  | Project\TeamB |
    When I request effort distribution
    Then the response should be OK

Scenario: Get effort distribution with custom maxIterations
    When I request effort distribution with maxIterations 15
    Then the response should be OK

Scenario: Get effort distribution with maxIterations below minimum
    When I request effort distribution with maxIterations 0
    Then the response should be BadRequest

Scenario: Get effort distribution with maxIterations above maximum
    When I request effort distribution with maxIterations 50
    Then the response should be BadRequest

Scenario: Get effort distribution with negative default capacity
    When I request effort distribution with defaultCapacity -10
    Then the response should be BadRequest

Scenario: Get effort distribution with area path filter
    When I request effort distribution with areaPathFilter "Project\TeamA"
    Then the response should be OK

Scenario: Get effort distribution with all parameters
    When I request effort distribution with areaPathFilter "Project\TeamA" maxIterations 8 and defaultCapacity 40
    Then the response should be OK

Scenario: Get sprint capacity plan with valid iteration path
    Given work items exist for iteration "Project\2024\Sprint1"
        | TfsId | Title    | Type | State       | Effort | IterationPath           | AssignedTo |
        | 10000 | Story 1  | Task | In Progress | 5      | Project\2024\Sprint1  | Alice      |
        | 10001 | Story 2  | Task | New         | 3      | Project\2024\Sprint1  | Bob        |
    When I request sprint capacity plan for iteration "Project\2024\Sprint1"
    Then the response should be OK

Scenario: Get sprint capacity plan with empty iteration path
    When I request sprint capacity plan with empty iteration path
    Then the response should be BadRequest

Scenario: Get sprint capacity plan for non-existent iteration
    When I request sprint capacity plan for iteration "NonExistent\\Sprint"
    Then the response should be NotFound

Scenario: Get sprint capacity plan with custom default capacity
    Given work items exist for iteration "Project\2024\Sprint1"
        | TfsId | Title    | Type | State       | Effort | IterationPath           |
        | 11000 | Story 1  | Task | In Progress | 5      | Project\2024\Sprint1  |
    When I request sprint capacity plan for iteration "Project\2024\Sprint1" with defaultCapacity 50
    Then the response should be OK

Scenario: Get sprint capacity plan with negative default capacity
    When I request sprint capacity plan for iteration "Project\2024\Sprint1" with defaultCapacity -10
    Then the response should be BadRequest
