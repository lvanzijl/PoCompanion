Feature: Settings Management
    As a user
    I want to manage application settings
    So that I can configure my preferences

Background:
    Given the application is running

Scenario: Get settings when none exist
    When I request the application settings
    Then the response should be NotFound

# The following scenarios are obsolete as DataMode and ConfiguredGoalIds have been removed
# Settings now only contain ActiveProfileId which is managed through ProfilesController
@ignore
Scenario: Update settings (OBSOLETE - DataMode removed)
    Given I have settings to update
        | Field              | Value |
        | DataMode           | Mock  |
        | ConfiguredGoalIds  | 1000  |
    When I update the application settings
    Then the response should be OK
    And the settings should be updated successfully

@ignore
Scenario: Get saved settings (OBSOLETE - DataMode removed)
    Given I have updated the settings with DataMode "Mock" and GoalIds "1000,2000"
    When I request the application settings
    Then the response should be OK
    And the returned settings should have DataMode "Mock"
    And the returned settings should have 2 goal IDs

@ignore
Scenario: Update settings with multiple goal IDs (OBSOLETE - ConfiguredGoalIds removed)
    Given I have settings to update
        | Field              | Value      |
        | DataMode           | Tfs        |
        | ConfiguredGoalIds  | 1,2,3,4,5  |
    When I update the application settings
    Then the response should be OK
