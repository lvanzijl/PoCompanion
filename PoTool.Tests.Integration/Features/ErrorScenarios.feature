Feature: Error Scenario Handling
    As a system tester
    I want to ensure the API handles error scenarios correctly
    So that clients receive appropriate error responses

Background:
    Given the application is running

Scenario: Request with invalid authentication returns 401
    When I request an endpoint without authentication
    Then the response should be Unauthorized

Scenario: Request to non-existent resource returns 404
    When I request a non-existent work item with ID 999999
    Then the response should be NotFound

Scenario: Request with malformed data returns 400
    When I send a malformed request to create a work item
    Then the response should be BadRequest

Scenario: Multiple concurrent requests are handled correctly
    When I send 10 concurrent requests to get work items
    Then all responses should be OK
    And all responses should complete within 5 seconds

Scenario: Request with missing required field returns 400
    When I send a TFS configuration without URL
    Then the response should be BadRequest
    And the error message should mention "URL"

Scenario: Request to endpoint with invalid method returns 405
    When I send a PUT request to a GET-only endpoint
    Then the response should be MethodNotAllowed

Scenario: Large dataset request completes successfully
    When I request work items with a large result set
    Then the response should be OK
    And the response should contain multiple items

Scenario: Request with invalid content type returns 415
    When I send a request with unsupported content type
    Then the response should be UnsupportedMediaType
