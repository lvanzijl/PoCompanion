Feature: TFS Configuration Management
    As a user
    I want to configure my TFS connection settings
    So that I can connect to Azure DevOps / TFS

Background:
    Given the application is running

Scenario: Get TFS configuration when none exists
    When I request the TFS configuration
    Then the response should be NoContent

Scenario: Save TFS configuration
    Given I have valid TFS credentials
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |
        | Pat     | test-pat-token-12345            |
    When I save the TFS configuration
    Then the configuration should be saved successfully
    And the response should be OK

Scenario: Get saved TFS configuration
    Given I have saved TFS configuration
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |
    When I request the TFS configuration
    Then the response should be OK
    And the returned configuration should match
        | Field   | Value                           |
        | Url     | https://dev.azure.com/testorg   |
        | Project | TestProject                     |

Scenario: Validate TFS connection
    Given I have saved valid TFS configuration
    When I validate the TFS connection
    Then the validation should succeed
    And the response should be OK
