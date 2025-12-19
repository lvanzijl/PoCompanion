Feature: SignalR WorkItem Hub
    As a user
    I want real-time updates via SignalR
    So that I can see work item changes immediately

Background:
    Given the application is running

Scenario: Connect to SignalR hub
    When I connect to the WorkItem hub
    Then the connection should be successful

Scenario: Request sync via SignalR
    Given I am connected to the WorkItem hub
    When I request a sync via SignalR for area "TestArea"
    Then the sync request should be accepted
    And I should receive sync status updates

Scenario: Disconnect from SignalR hub
    Given I am connected to the WorkItem hub
    When I disconnect from the WorkItem hub
    Then the disconnection should be successful
