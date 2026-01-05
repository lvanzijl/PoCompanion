Feature: Settings Controller API
    As a user
    I want to manage settings through the API
    So that I can configure the application

Background:
    Given the application is running

# All scenarios below are obsolete as DataMode and ConfiguredGoalIds have been removed from Settings
# Settings now only contain ActiveProfileId which is managed through ProfilesController (/api/profiles/active)
@ignore
Scenario: Get settings when settings exist (OBSOLETE)
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

@ignore
Scenario: Update settings with valid data (OBSOLETE)
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I update settings with DataMode "Tfs" and goal IDs "4,5,6"
    Then the response should be OK
    And the updated settings should have DataMode "Tfs"
    And the updated settings should have goal IDs "4,5,6"

@ignore
Scenario: Update settings with empty goal IDs (OBSOLETE)
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I update settings with DataMode "Mock" and empty goal IDs
    Then the response should be OK

@ignore
Scenario: Update settings to Tfs mode (OBSOLETE)
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1,2,3             |
    When I update settings with DataMode "Tfs" and goal IDs "1,2,3"
    Then the response should be OK
    And the updated settings should have DataMode "Tfs"

@ignore
Scenario: Update settings to Mock mode (OBSOLETE)
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Tfs      | 1,2,3             |
    When I update settings with DataMode "Mock" and goal IDs "1,2,3"
    Then the response should be OK
    And the updated settings should have DataMode "Mock"

@ignore
Scenario: Update settings with single goal ID (OBSOLETE)
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1                 |
    When I update settings with DataMode "Mock" and goal IDs "999"
    Then the response should be OK
    And the updated settings should have goal IDs "999"

@ignore
Scenario: Update settings with multiple goal IDs (OBSOLETE)
    Given settings exist in the database
        | DataMode | ConfiguredGoalIds |
        | Mock     | 1                 |
    When I update settings with DataMode "Mock" and goal IDs "1,2,3,4,5"
    Then the response should be OK
    And the updated settings should have goal IDs "1,2,3,4,5"
