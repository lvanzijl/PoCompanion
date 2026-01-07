Feature: SignalR WorkItem Hub
    As a user
    I want real-time updates via SignalR
    So that I can see work item changes immediately

Background:
    Given the application is running

# Connection Lifecycle Tests

Scenario: Connect to SignalR hub
    When I connect to the WorkItem hub
    Then the connection should be successful

Scenario: Disconnect from SignalR hub
    Given I am connected to the WorkItem hub
    When I disconnect from the WorkItem hub
    Then the disconnection should be successful

Scenario: Reconnect to SignalR hub after disconnect
    Given I am connected to the WorkItem hub
    When I disconnect from the WorkItem hub
    And I reconnect to the WorkItem hub
    Then the connection should be successful

# Multiple Concurrent Client Connections

Scenario: Multiple clients can request sync independently
    Given I have 2 connected clients to the WorkItem hub
    When client 1 requests a sync via SignalR for area "TestArea1"
    And client 2 requests a sync via SignalR for area "TestArea2"
    Then both clients should receive their respective sync notifications

# Error Handling and Edge Cases

Scenario: Handle connection error gracefully
    When I attempt to connect to an invalid hub endpoint
    Then the connection should fail with an error

Scenario: Request sync on disconnected connection should fail
    Given I was connected to the WorkItem hub but disconnected
    When I attempt to request a sync via SignalR for area "TestArea"
    Then the request should fail due to disconnection

Scenario: Hub method invocation with null parameter
    Given I am connected to the WorkItem hub
    When I request a sync via SignalR with null area path
    Then the request should complete without throwing
