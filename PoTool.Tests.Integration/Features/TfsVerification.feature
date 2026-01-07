Feature: TFS API Verification
    As a user
    I want to verify TFS API capabilities
    So that I can troubleshoot integration issues

Background:
    Given the application is running

Scenario: Verify TFS API with read-only checks
    Given I have saved TFS configuration
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |
    When I request TFS API verification with read-only checks
    Then the verification response should be OK
    And the verification report should include read-only checks
    And all checks should pass

Scenario: Verify TFS API with write checks
    Given I have saved TFS configuration
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |
    When I request TFS API verification with write checks for work item 1000
    Then the verification response should be OK
    And the verification report should include write checks
    And the write check should target work item 1000

Scenario: Verify TFS API without write checks when requested
    Given I have saved TFS configuration
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |
    When I request TFS API verification with write checks but no work item ID
    Then the verification response should be OK
    And the verification report should not include write checks

Scenario: Verify TFS API returns all expected capability checks
    Given I have saved TFS configuration
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |
    When I request TFS API verification with read-only checks
    Then the verification response should be OK
    And the verification report should include capability "server-reachability"
    And the verification report should include capability "project-access"
    And the verification report should include capability "work-item-query"
    And the verification report should include capability "work-item-fields"
    And the verification report should include capability "batch-read"
    And the verification report should include capability "work-item-revisions"
    And the verification report should include capability "pull-requests"
