Feature: Work Items Management
    As a user
    I want to retrieve and manage work items
    So that I can view and manage my backlog

Background:
    Given the application is running

# NOTE: Tests requiring a live TFS server have been removed
# as they are not suitable for CI/CD environments.
# The application uses mocked TFS client in integration tests.
