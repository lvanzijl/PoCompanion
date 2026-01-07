Feature: Error Scenario Handling
    As a system tester
    I want to ensure the API handles error scenarios correctly
    So that clients receive appropriate error responses

Background:
    Given the application is running

Scenario: Request to non-existent resource returns 404
    When I request a non-existent work item with ID 999999
    Then the response should be NotFound

Scenario: Multiple concurrent requests are handled correctly
    When I send 10 concurrent requests to get work items
    Then all responses should be OK
    And all responses should complete within 5 seconds

Scenario: Request to endpoint with invalid method returns 405
    When I send a PUT request to a GET-only endpoint
    Then the response should be MethodNotAllowed
