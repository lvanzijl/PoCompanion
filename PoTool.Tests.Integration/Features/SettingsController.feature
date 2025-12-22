Feature: Settings Controller API
    As a user
    I want to manage settings through the API
    So that I can configure the application

Background:
    Given the application is running

Scenario: Get settings when settings exist
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I request settings from "/api/settings"
    Then the response should be OK
    And the settings should have DataMode "Mock"
    And the settings should have goal IDs "1,2,3"

Scenario: Get settings when no settings exist
    When I request settings from "/api/settings"
    Then the response should be NotFound

Scenario: Update settings with valid data
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I update settings with DataMode "Live" and goal IDs "4,5,6"
    Then the response should be OK
    And the updated settings should have DataMode "Live"
    And the updated settings should have goal IDs "4,5,6"

Scenario: Update settings with empty goal IDs
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I update settings with DataMode "Mock" and empty goal IDs
    Then the response should be OK

Scenario: Update settings to Live mode
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I update settings with DataMode "Live" and goal IDs "1,2,3"
    Then the response should be OK
    And the updated settings should have DataMode "Live"

Scenario: Update settings to Mock mode
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Live     | 1,2,3             |
    When I update settings with DataMode "Mock" and goal IDs "1,2,3"
    Then the response should be OK
    And the updated settings should have DataMode "Mock"

Scenario: Update settings with single goal ID
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1                 |
    When I update settings with DataMode "Mock" and goal IDs "999"
    Then the response should be OK
    And the updated settings should have goal IDs "999"

Scenario: Update settings with multiple goal IDs
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1                 |
    When I update settings with DataMode "Mock" and goal IDs "1,2,3,4,5"
    Then the response should be OK
    And the updated settings should have goal IDs "1,2,3,4,5"
