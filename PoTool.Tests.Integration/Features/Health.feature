Feature: Health Check Endpoint
    As a system administrator
    I want to check the application health
    So that I can monitor its status

Background:
    Given the application is running

Scenario: Check application health
    When I request the health endpoint
    Then the response should be OK
    And the health status should be "Healthy"
    And the response should include a timestamp
