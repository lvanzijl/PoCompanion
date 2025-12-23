Feature: Profiles Controller API
    As a user
    I want to manage profiles through the API
    So that I can organize work items by area paths and teams

Background:
    Given the application is running

Scenario: Get all profiles when none exist
    When I request all profiles from "/api/profiles"
    Then the response should be OK
    And the profiles list should be empty

Scenario: Create a new profile with area paths
    When I create a profile with name "Product A Team" and area paths "Project\\ProductA,Project\\ProductA\\Mobile"
    Then the response should be Created
    And the created profile should have name "Product A Team"
    And the created profile should have 2 area paths

Scenario: Create a profile with team name and goals
    When I create a profile with name "Team Alpha" team "Alpha Team" and goals "1,2,3"
    Then the response should be Created
    And the created profile should have team name "Alpha Team"
    And the created profile should have 3 goal IDs

Scenario: Get profile by ID
    Given a profile exists with name "Test Profile" and area paths "Project\\Test"
    When I request the profile by its ID
    Then the response should be OK
    And the returned profile should have name "Test Profile"

Scenario: Get all profiles when multiple exist
    Given a profile exists with name "Profile 1" and area paths "Project\\A"
    And a profile exists with name "Profile 2" and area paths "Project\\B"
    And a profile exists with name "Profile 3" and area paths "Project\\C"
    When I request all profiles from "/api/profiles"
    Then the response should be OK
    And the profiles list should contain 3 profiles

Scenario: Update an existing profile
    Given a profile exists with name "Old Name" and area paths "Project\\Old"
    When I update the profile with name "New Name" and area paths "Project\\New"
    Then the response should be OK
    And the updated profile should have name "New Name"
    And the updated profile should have area path "Project\\New"

Scenario: Update profile area paths
    Given a profile exists with name "Multi-Path Profile" and area paths "Project\\A"
    When I update the profile with area paths "Project\\A,Project\\B,Project\\C"
    Then the response should be OK
    And the updated profile should have 3 area paths

Scenario: Delete a profile
    Given a profile exists with name "Profile To Delete" and area paths "Project\\Delete"
    When I delete the profile by its ID
    Then the response should be NoContent
    And the profile should not exist anymore

Scenario: Set active profile
    Given a profile exists with name "Active Profile" and area paths "Project\\Active"
    When I set the profile as active
    Then the response should be successful
    And the settings should have the active profile ID set

Scenario: Get active profile
    Given a profile exists with name "Current Profile" and area paths "Project\\Current"
    And the profile is set as active
    When I request the active profile from "/api/profiles/active"
    Then the response should be OK
    And the returned profile should have name "Current Profile"

Scenario: Get active profile when none is set
    When I request the active profile from "/api/profiles/active"
    Then the response should be NotFound

Scenario: Create profile with empty area paths
    When I create a profile with name "Empty Profile" and empty area paths
    Then the response should be Created
    And the created profile should have 0 area paths

Scenario: Create profile with hierarchical area paths
    When I create a profile with name "Hierarchical Profile" and area paths "Project\\Product,Project\\Product\\Mobile,Project\\Product\\Web"
    Then the response should be Created
    And the created profile should have 3 area paths
    And the area paths should be hierarchical
