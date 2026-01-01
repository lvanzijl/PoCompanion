Feature: Filtering Controller API
    As a user
    I want to filter work items through the API
    So that I can apply validation and hierarchy filters

Background:
    Given the application is running

Scenario: Filter work items by validation with ancestors returns response
    When I request filtering by validation with target IDs "3002"
    Then the filtering response should be OK

Scenario: Get work item IDs by validation filter returns response
    When I request work item IDs by validation filter "missingEffort"
    Then the filtering response should be OK

Scenario: Count work items by validation filter returns response
    When I count work items by validation filter "missingEffort"
    Then the filtering response should be OK

Scenario: Check if work item is descendant of goals returns response
    When I check if work item 3002 is descendant of goals "3000"
    Then the filtering response should be OK
