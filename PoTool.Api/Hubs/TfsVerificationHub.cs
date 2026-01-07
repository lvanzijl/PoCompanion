using Microsoft.AspNetCore.SignalR;
using PoTool.Core.Contracts;
using PoTool.Core.Contracts.TfsVerification;

namespace PoTool.Api.Hubs;

/// <summary>
/// SignalR hub for real-time TFS verification progress updates.
/// </summary>
public class TfsVerificationHub : Hub
{
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<TfsVerificationHub> _logger;

    public TfsVerificationHub(
        ITfsClient tfsClient,
        ILogger<TfsVerificationHub> logger)
    {
        _tfsClient = tfsClient;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Verification client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Verification client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Runs the comprehensive TFS verification and streams progress updates to the client.
    /// </summary>
    /// <param name="includeWriteChecks">Whether to include write capability checks.</param>
    /// <param name="workItemIdForWriteCheck">Optional work item ID for write verification.</param>
    public async Task StartVerification(bool includeWriteChecks, int? workItemIdForWriteCheck)
    {
        _logger.LogInformation("Verification requested by client {ConnectionId}, WriteChecks: {WriteChecks}, WorkItemId: {WorkItemId}",
            Context.ConnectionId, includeWriteChecks, workItemIdForWriteCheck);

        try
        {
            // Notify client that verification is starting
            await Clients.Caller.SendAsync("VerificationStarted", new { TotalSteps = GetTotalSteps(includeWriteChecks) });

            // Run verification and get the report
            var report = await _tfsClient.VerifyCapabilitiesAsync(includeWriteChecks, workItemIdForWriteCheck);

            // Send the final report
            await Clients.Caller.SendAsync("VerificationCompleted", report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verification failed for client {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("VerificationFailed", new { Message = ex.Message, ExceptionType = ex.GetType().Name });
        }
    }

    private static int GetTotalSteps(bool includeWriteChecks)
    {
        // Base steps: Server, Project, WIQL, Fields, Batch, Revisions, PRs, Hierarchy, Pipelines
        var baseSteps = 9;
        return includeWriteChecks ? baseSteps + 1 : baseSteps;
    }
}
