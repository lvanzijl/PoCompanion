using Microsoft.AspNetCore.SignalR.Client;
using PoTool.Tests.Integration.Support;
using Reqnroll;
using System.Text.Json;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class SignalRHubSteps : IDisposable
{
    // Configuration constants for timing
    private const int StandardDelayMs = 1000;
    private const int ShortDelayMs = 500;
    private const int ConnectionTimeoutMs = 5000;

    private readonly IntegrationTestWebApplicationFactory _factory;
    private HubConnection? _hubConnection;
    private readonly List<HubConnection> _multipleConnections = new();
    private readonly object _messagesLock = new(); // Single lock for all message collections
    private readonly List<SyncStatusMessage> _receivedMessages = new();
    private readonly Dictionary<int, List<SyncStatusMessage>> _clientMessages = new(); // Track messages per client
    private Exception? _connectionError;
    private Exception? _invocationError;

    public SignalRHubSteps(SharedTestContext sharedContext)
    {
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
    }

    // Helper class to capture sync status messages
    private class SyncStatusMessage
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }

    // Connection lifecycle steps

    [When(@"I connect to the WorkItem hub")]
    [Given(@"I am connected to the WorkItem hub")]
    public async Task WhenIConnectToTheWorkItemHub()
    {
        await ConnectToHub();
    }

    [Given(@"I was connected to the WorkItem hub but disconnected")]
    public async Task GivenIWasConnectedButDisconnected()
    {
        await ConnectToHub();
        await _hubConnection!.StopAsync();
    }

    [When(@"I reconnect to the WorkItem hub")]
    public async Task WhenIReconnectToTheWorkItemHub()
    {
        await ConnectToHub();
    }

    [When(@"I disconnect from the WorkItem hub")]
    public async Task WhenIDisconnectFromTheWorkItemHub()
    {
        Assert.IsNotNull(_hubConnection, "Hub connection must exist");
        await _hubConnection.StopAsync();
    }

    [When(@"I attempt to connect to an invalid hub endpoint")]
    public async Task WhenIAttemptToConnectToInvalidEndpoint()
    {
        try
        {
            var baseUrl = _factory.Server.BaseAddress.ToString().TrimEnd('/');
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{baseUrl}/hubs/invalid", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            _connectionError = ex;
        }
    }

    // Multiple client connection steps

    [Given(@"I have (\d+) connected clients to the WorkItem hub")]
    public async Task GivenIHaveMultipleConnectedClients(int clientCount)
    {
        for (int i = 0; i < clientCount; i++)
        {
            _clientMessages[i] = new List<SyncStatusMessage>();
            var connection = await CreateAndConnectHub(i);
            _multipleConnections.Add(connection);
        }
    }

    // Sync request steps

    [When(@"I request a sync via SignalR for area ""(.*)""")]
    public async Task WhenIRequestASyncViaSignalR(string areaPath)
    {
        Assert.IsNotNull(_hubConnection, "Hub connection must be established first");
        await _hubConnection.InvokeAsync("RequestSync", areaPath);

        // Give time for async processing and notifications
        await Task.Delay(StandardDelayMs);
    }

    [When(@"I request a sync via SignalR with null area path")]
    public async Task WhenIRequestASyncWithNullAreaPath()
    {
        try
        {
            Assert.IsNotNull(_hubConnection, "Hub connection must be established first");
            await _hubConnection.InvokeAsync("RequestSync", (string?)null);
            await Task.Delay(ShortDelayMs);
        }
        catch (Exception ex)
        {
            _invocationError = ex;
        }
    }

    [When(@"I attempt to request a sync via SignalR for area ""(.*)""")]
    public async Task WhenIAttemptToRequestASync(string areaPath)
    {
        try
        {
            Assert.IsNotNull(_hubConnection, "Hub connection must exist");
            await _hubConnection.InvokeAsync("RequestSync", areaPath);
            await Task.Delay(ShortDelayMs);
        }
        catch (Exception ex)
        {
            _invocationError = ex;
        }
    }

    [When(@"any client requests a sync via SignalR for area ""(.*)""")]
    public async Task WhenAnyClientRequestsASync(string areaPath)
    {
        Assert.IsTrue(_multipleConnections.Count > 0, "Multiple connections must be established");
        await _multipleConnections[0].InvokeAsync("RequestSync", areaPath);

        // Give time for all clients to receive notifications
        await Task.Delay(StandardDelayMs);
    }

    [When(@"client (\d+) requests a sync via SignalR for area ""(.*)""")]
    public async Task WhenClientRequestsASync(int clientIndex, string areaPath)
    {
        var index = clientIndex - 1; // Convert to 0-based index
        Assert.IsTrue(index < _multipleConnections.Count, $"Client {clientIndex} must be connected");
        await _multipleConnections[index].InvokeAsync("RequestSync", areaPath);
        await Task.Delay(StandardDelayMs);
    }

    // TFS client configuration steps

    [Given(@"the TFS client is configured to return empty results")]
    public void GivenTheTfsClientIsConfiguredToReturnEmptyResults()
    {
        // Get the mock TFS client and configure it to return no work items
        var serviceProvider = _factory.Services;
        var mockClient = serviceProvider.GetService(typeof(PoTool.Core.Contracts.ITfsClient)) as MockTfsClient;
        if (mockClient != null)
        {
            // Clear mock work items so sync returns 0 items
            mockClient.ClearMockWorkItems();
        }
    }

    // Assertion steps

    [Then(@"the connection should be successful")]
    public void ThenTheConnectionShouldBeSuccessful()
    {
        Assert.IsNotNull(_hubConnection, "Hub connection must be established");
        Assert.AreEqual(HubConnectionState.Connected, _hubConnection.State, "Connection should be in Connected state");
    }

    [Then(@"the disconnection should be successful")]
    public void ThenTheDisconnectionShouldBeSuccessful()
    {
        Assert.IsNotNull(_hubConnection, "Hub connection must exist");
        Assert.AreEqual(HubConnectionState.Disconnected, _hubConnection.State, "Connection should be in Disconnected state");
    }

    [Then(@"the connection should fail with an error")]
    public void ThenTheConnectionShouldFailWithError()
    {
        Assert.IsNotNull(_connectionError, "Connection error should have been captured");
    }

    [Then(@"I should receive a SyncStatus notification with status ""(.*)""")]
    public void ThenIShouldReceiveSyncStatusNotificationWithStatus(string expectedStatus)
    {
        var message = _receivedMessages.FirstOrDefault(m => m.Status == expectedStatus);
        Assert.IsNotNull(message, $"Expected to receive a SyncStatus notification with status '{expectedStatus}'");
    }

    [Then(@"the notification message should contain ""(.*)""")]
    public void ThenTheNotificationMessageShouldContain(string expectedText)
    {
        // Get all messages and check if any contains the expected text
        Assert.IsTrue(_receivedMessages.Count > 0, "Expected to have received at least one notification");

        var matchingMessage = _receivedMessages.FirstOrDefault(m =>
            m.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(matchingMessage,
            $"Expected to find a message containing '{expectedText}', but got messages: {string.Join("; ", _receivedMessages.Select(m => m.Message))}");
    }

    [Then(@"I should receive sync notifications in order: ""(.*)""")]
    public void ThenIShouldReceiveSyncNotificationsInOrder(string expectedOrder)
    {
        var expectedStatuses = expectedOrder.Split(',').Select(s => s.Trim()).ToList();

        Assert.IsTrue(_receivedMessages.Count >= expectedStatuses.Count,
            $"Expected at least {expectedStatuses.Count} notifications, but received {_receivedMessages.Count}");

        for (int i = 0; i < expectedStatuses.Count; i++)
        {
            Assert.AreEqual(expectedStatuses[i], _receivedMessages[i].Status,
                $"Expected notification {i + 1} to have status '{expectedStatuses[i]}', but got '{_receivedMessages[i].Status}'");
        }

        // Verify timestamps are in ascending order
        for (int i = 1; i < _receivedMessages.Count; i++)
        {
            Assert.IsTrue(_receivedMessages[i].Timestamp >= _receivedMessages[i - 1].Timestamp,
                "Notifications should be received in chronological order");
        }
    }

    [Then(@"all (\d+) clients should receive SyncStatus notifications")]
    public void ThenAllClientsShouldReceiveNotifications(int expectedClientCount)
    {
        // Each client should have received at least one notification
        Assert.AreEqual(expectedClientCount, _clientMessages.Count,
            $"Expected {expectedClientCount} clients to be tracked");

        foreach (var clientId in _clientMessages.Keys)
        {
            Assert.IsTrue(_clientMessages[clientId].Count > 0,
                $"Client {clientId} should have received at least one notification");
        }
    }

    [Then(@"both clients should receive their respective sync notifications")]
    public void ThenBothClientsShouldReceiveNotifications()
    {
        // Verify that both clients received notifications
        Assert.AreEqual(2, _clientMessages.Count, "Expected 2 clients to be tracked");

        foreach (var clientId in _clientMessages.Keys)
        {
            Assert.IsTrue(_clientMessages[clientId].Count > 0,
                $"Client {clientId} should have received sync notifications");
        }
    }

    [Then(@"the request should fail due to disconnection")]
    public void ThenTheRequestShouldFailDueToDisconnection()
    {
        Assert.IsNotNull(_invocationError, "Expected an error when invoking method on disconnected connection");
    }

    [Then(@"the request should complete without throwing")]
    public void ThenTheRequestShouldCompleteWithoutThrowing()
    {
        Assert.IsNull(_invocationError, $"Request should complete without throwing, but got: {_invocationError?.Message}");
    }

    // Helper methods

    /// <summary>
    /// Gets a property value from a dictionary with case-insensitive key lookup.
    /// </summary>
    private static string? GetPropertyValue(Dictionary<string, JsonElement> dictionary, string propertyName)
    {
        // Try exact match first
        if (dictionary.TryGetValue(propertyName, out var value))
        {
            return value.GetString();
        }

        // Try case-insensitive match
        var key = dictionary.Keys.FirstOrDefault(k =>
            k.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

        return key != null ? dictionary[key].GetString() : null;
    }

    private async Task<HubConnection> CreateAndConnectHub(int? clientId = null)
    {
        var baseUrl = _factory.Server.BaseAddress.ToString().TrimEnd('/');
        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/workitems", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        // Capture clientId in local variable for closure
        var capturedClientId = clientId;

        // Subscribe to sync status updates
        connection.On<object>("SyncStatus", (message) =>
        {
            try
            {
                // Parse the message object (it's sent as an anonymous object with Status and Message properties)
                var json = JsonSerializer.Serialize(message);
                var syncMessage = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (syncMessage != null)
                {
                    var status = GetPropertyValue(syncMessage, "Status") ?? string.Empty;
                    var msg = GetPropertyValue(syncMessage, "Message") ?? string.Empty;

                    var statusMessage = new SyncStatusMessage
                    {
                        Status = status,
                        Message = msg,
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    // Add to collections using single lock for thread safety
                    lock (_messagesLock)
                    {
                        _receivedMessages.Add(statusMessage);

                        // Add to client-specific list if client ID is provided
                        if (capturedClientId.HasValue && _clientMessages.ContainsKey(capturedClientId.Value))
                        {
                            _clientMessages[capturedClientId.Value].Add(statusMessage);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle exceptions to avoid failing tests due to parsing errors
                // In production, this would be logged properly
            }
        });

        await connection.StartAsync();
        return connection;
    }

    private async Task ConnectToHub()
    {
        _hubConnection = await CreateAndConnectHub();
    }

    public void Dispose()
    {
        // Dispose hub connection
        if (_hubConnection != null)
        {
            try
            {
                _hubConnection.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        // Dispose multiple connections
        foreach (var connection in _multipleConnections)
        {
            try
            {
                connection?.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        _multipleConnections.Clear();

        // Dispose factory
        try
        {
            _factory?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}
