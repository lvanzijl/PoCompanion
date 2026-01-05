using Mediator;
using Microsoft.Extensions.Configuration;
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
    private readonly ITfsClient _tfsClient;
    private readonly BattleshipMockDataFacade? _mockDataFacade;
    private readonly ILogger<SyncPullRequestsCommandHandler> _logger;
    private readonly bool _useMockClient;

    public SyncPullRequestsCommandHandler(
        IPullRequestRepository repository,
        ITfsClient tfsClient,
        ILogger<SyncPullRequestsCommandHandler> logger,
        IConfiguration configuration,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _repository = repository;
        _tfsClient = tfsClient;
        _mockDataFacade = mockDataFacade;
        _logger = logger;
        _useMockClient = configuration.GetValue<bool>("TfsIntegration:UseMockClient", false);
    }

    public async ValueTask<int> Handle(
        SyncPullRequestsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pull request sync");

        if (_useMockClient && _mockDataFacade != null)
        {
            // Use mock data from Battleship system
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
        else
        {
            // Use real TFS client to fetch pull requests
            var pullRequests = (await _tfsClient.GetPullRequestsAsync(cancellationToken: cancellationToken)).ToList();
            
            // For each PR, fetch its iterations, comments, and file changes
            var allIterations = new List<Core.PullRequests.PullRequestIterationDto>();
            var allComments = new List<Core.PullRequests.PullRequestCommentDto>();
            var allFileChanges = new List<Core.PullRequests.PullRequestFileChangeDto>();

            foreach (var pr in pullRequests)
            {
                var iterations = await _tfsClient.GetPullRequestIterationsAsync(pr.Id, pr.RepositoryName, cancellationToken);
                allIterations.AddRange(iterations);

                var comments = await _tfsClient.GetPullRequestCommentsAsync(pr.Id, pr.RepositoryName, cancellationToken);
                allComments.AddRange(comments);

                foreach (var iteration in iterations)
                {
                    var fileChanges = await _tfsClient.GetPullRequestFileChangesAsync(pr.Id, pr.RepositoryName, iteration.IterationNumber, cancellationToken);
                    allFileChanges.AddRange(fileChanges);
                }
            }

            // Save to repository
            await _repository.SaveAsync(pullRequests, cancellationToken);
            await _repository.SaveIterationsAsync(allIterations, cancellationToken);
            await _repository.SaveCommentsAsync(allComments, cancellationToken);
            await _repository.SaveFileChangesAsync(allFileChanges, cancellationToken);

            _logger.LogInformation("Pull request sync completed. Synced {Count} PRs", pullRequests.Count);
            return pullRequests.Count;
        }
    }
}
