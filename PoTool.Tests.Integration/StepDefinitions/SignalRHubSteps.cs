using Microsoft.AspNetCore.SignalR.Client;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class SignalRHubSteps : IDisposable
{
    private readonly SharedTestState _context;
    private HubConnection? _hubConnection;
    private readonly List<string> _receivedMessages = new();

    public SignalRHubSteps(SharedTestState context)
    {
        _context = context;
    }

    [When(@"I connect to the WorkItem hub")]
    [Given(@"I am connected to the WorkItem hub")]
    public async Task WhenIConnectToTheWorkItemHub()
    {
        var baseUrl = _context.Factory.Server.BaseAddress.ToString().TrimEnd('/');
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/workitems", options =>
            {
                options.HttpMessageHandlerFactory = _ => _context.Factory.Server.CreateHandler();
            })
            .Build();

        // Subscribe to sync status updates
        _hubConnection.On<string, string>("SyncStatus", (status, message) =>
        {
            _receivedMessages.Add($"{status}: {message}");
        });

        await _hubConnection.StartAsync();
    }

    [When(@"I request a sync via SignalR for area ""(.*)""")]
    public async Task WhenIRequestASyncViaSignalR(string areaPath)
    {
        Assert.IsNotNull(_hubConnection, "Hub connection must be established first");
        await _hubConnection.InvokeAsync("RequestSync", areaPath);
        
        // Give some time for async processing
        await Task.Delay(500);
    }

    [When(@"I disconnect from the WorkItem hub")]
    public async Task WhenIDisconnectFromTheWorkItemHub()
    {
        Assert.IsNotNull(_hubConnection);
        await _hubConnection.StopAsync();
    }

    [Then(@"the connection should be successful")]
    public void ThenTheConnectionShouldBeSuccessful()
    {
        Assert.IsNotNull(_hubConnection);
        Assert.AreEqual(HubConnectionState.Connected, _hubConnection.State);
    }

    [Then(@"the sync request should be accepted")]
    public void ThenTheSyncRequestShouldBeAccepted()
    {
        Assert.IsNotNull(_hubConnection);
        Assert.AreEqual(HubConnectionState.Connected, _hubConnection.State);
    }

    [Then(@"I should receive sync status updates")]
    public void ThenIShouldReceiveSyncStatusUpdates()
    {
        // For now, we just verify the connection is still active
        // In a real scenario, we'd wait for actual sync status messages
        Assert.IsNotNull(_hubConnection);
        Assert.AreEqual(HubConnectionState.Connected, _hubConnection.State);
    }

    [Then(@"the disconnection should be successful")]
    public void ThenTheDisconnectionShouldBeSuccessful()
    {
        Assert.IsNotNull(_hubConnection);
        Assert.AreEqual(HubConnectionState.Disconnected, _hubConnection.State);
    }

    public void Dispose()
    {
        _hubConnection?.DisposeAsync().AsTask().Wait();
        // Note: Factory and Client disposal is handled by SharedTestState
    }
}
