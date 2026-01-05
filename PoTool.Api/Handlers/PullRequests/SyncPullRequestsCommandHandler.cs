using Mediator;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Core.PullRequests.Commands;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for SyncPullRequestsCommand.
/// Synchronizes pull requests from TFS to local cache.
/// </summary>
public sealed class SyncPullRequestsCommandHandler : ICommandHandler<SyncPullRequestsCommand, int>
{
    private readonly IPullRequestRepository _repository;
    private readonly BattleshipMockDataFacade _mockDataFacade;
    private readonly ILogger<SyncPullRequestsCommandHandler> _logger;

    public SyncPullRequestsCommandHandler(
        IPullRequestRepository repository,
        BattleshipMockDataFacade mockDataFacade,
        ILogger<SyncPullRequestsCommandHandler> logger)
    {
        _repository = repository;
        _mockDataFacade = mockDataFacade;
        _logger = logger;
    }

    public async ValueTask<int> Handle(
        SyncPullRequestsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pull request sync");

        // For now, use mock data from new Battleship system
        // In future, check TfsIntegration:UseMockClient from configuration and use ITfsClient when not using mock
        var pullRequests = _mockDataFacade.GetMockPullRequests();
        var iterations = _mockDataFacade.GetMockIterations();
        var comments = _mockDataFacade.GetMockComments();
        var fileChanges = _mockDataFacade.GetMockFileChanges();

        // Save to repository
        await _repository.SaveAsync(pullRequests, cancellationToken);
        await _repository.SaveIterationsAsync(iterations, cancellationToken);
        await _repository.SaveCommentsAsync(comments, cancellationToken);
        await _repository.SaveFileChangesAsync(fileChanges, cancellationToken);

        _logger.LogInformation("Pull request sync completed. Synced {Count} PRs", pullRequests.Count);

        return pullRequests.Count;
    }
}
