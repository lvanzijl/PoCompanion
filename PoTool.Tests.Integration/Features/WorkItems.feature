Feature: Work Items Management
    As a user
    I want to retrieve and sync work items from TFS
    So that I can view and manage my backlog

Background:
    Given the application is running

Scenario: Sync work items from TFS
    Given TFS has work items available
    When I request a work item sync
    Then the sync should complete successfully
    And the response should be OK

Scenario: Get all work items
    Given work items are synced in the database
        | TfsId | Title          | Type      | State  |
        | 1000  | Test Goal      | Goal      | Active |
        | 1001  | Test Objective | Objective | Active |
        | 1002  | Test Epic      | Epic      | New    |
    When I request all work items
    Then the response should be OK
    And I should receive 3 work items

Scenario: Get work item by ID
    Given a work item exists with ID 1000
    When I request work item 1000
    Then the response should be OK
    And the work item should have title "Test Goal"

Scenario: Get non-existent work item
    When I request work item 99999
    Then the response should be NotFound
