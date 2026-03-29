using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPullRequestMetricsQuery.
/// Calculates aggregated metrics for all pull requests.
/// Uses the cache-backed pull request read provider registered for analytical reads.
/// </summary>
public sealed class GetPullRequestMetricsQueryHandler : IQueryHandler<GetPullRequestMetricsQuery, IEnumerable<PullRequestMetricsDto>>
{
    private readonly IPullRequestReadProvider _pullRequestReadProvider;
    private readonly ILogger<GetPullRequestMetricsQueryHandler> _logger;

    public GetPullRequestMetricsQueryHandler(
        IPullRequestReadProvider pullRequestReadProvider,
        ILogger<GetPullRequestMetricsQueryHandler> logger)
    {
        _pullRequestReadProvider = pullRequestReadProvider;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<PullRequestMetricsDto>> Handle(
        GetPullRequestMetricsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetPullRequestMetricsQuery with repository scope {RepositoryCount}, range [{RangeStartUtc}, {RangeEndUtc}]",
            query.EffectiveFilter.RepositoryScope.Count,
            query.EffectiveFilter.RangeStartUtc,
            query.EffectiveFilter.RangeEndUtc);

        var allPrs = (await _pullRequestReadProvider.GetByRepositoryNamesAsync(
            query.EffectiveFilter.RepositoryScope,
            query.EffectiveFilter.RangeStartUtc,
            query.EffectiveFilter.RangeEndUtc,
            cancellationToken)).ToList();

        if (allPrs.Count == 0)
        {
            return Array.Empty<PullRequestMetricsDto>();
        }

        var pullRequestIds = allPrs
            .Select(pr => pr.Id)
            .Distinct()
            .ToArray();
        var repositoryNamesByPullRequestId = allPrs
            .GroupBy(pr => pr.Id)
            .ToDictionary(group => group.Key, group => group.Last().RepositoryName);
        var iterationsByPullRequestId = await _pullRequestReadProvider.GetIterationsForPullRequestsAsync(
            pullRequestIds,
            repositoryNamesByPullRequestId,
            cancellationToken);
        var commentsByPullRequestId = await _pullRequestReadProvider.GetCommentsForPullRequestsAsync(
            pullRequestIds,
            repositoryNamesByPullRequestId,
            cancellationToken);
        var fileChangesByPullRequestId = await _pullRequestReadProvider.GetFileChangesForPullRequestsAsync(
            pullRequestIds,
            repositoryNamesByPullRequestId,
            cancellationToken);
        var metrics = new List<PullRequestMetricsDto>();

        foreach (var pr in allPrs)
        {
            var iterations = iterationsByPullRequestId.TryGetValue(pr.Id, out var pullRequestIterations)
                ? pullRequestIterations
                : Array.Empty<PullRequestIterationDto>();
            var comments = commentsByPullRequestId.TryGetValue(pr.Id, out var pullRequestComments)
                ? pullRequestComments
                : Array.Empty<PullRequestCommentDto>();
            var fileChanges = fileChangesByPullRequestId.TryGetValue(pr.Id, out var pullRequestFileChanges)
                ? pullRequestFileChanges
                : Array.Empty<PullRequestFileChangeDto>();

            var totalTimeOpen = CalculateTotalTimeOpen(pr);
            var effectiveWorkTime = CalculateEffectiveWorkTime(pr, iterations);
            var totalFileCount = fileChanges.GroupBy(fc => fc.FilePath).Count();
            var totalLinesAdded = fileChanges.Sum(fc => fc.LinesAdded);
            var totalLinesDeleted = fileChanges.Sum(fc => fc.LinesDeleted);
            var avgLinesPerFile = totalFileCount > 0 ? (double)(totalLinesAdded + totalLinesDeleted) / totalFileCount : 0;

            metrics.Add(new PullRequestMetricsDto(
                PullRequestId: pr.Id,
                Title: pr.Title,
                CreatedBy: pr.CreatedBy,
                CreatedDate: pr.CreatedDate,
                CompletedDate: pr.CompletedDate,
                Status: pr.Status,
                IterationPath: pr.IterationPath,
                TotalTimeOpen: totalTimeOpen,
                EffectiveWorkTime: effectiveWorkTime,
                IterationCount: iterations.Count(),
                CommentCount: comments.Count(),
                UnresolvedCommentCount: comments.Count(c => !c.IsResolved),
                TotalFileCount: totalFileCount,
                TotalLinesAdded: totalLinesAdded,
                TotalLinesDeleted: totalLinesDeleted,
                AverageLinesPerFile: Math.Round(avgLinesPerFile, 2)
            ));
        }

        return metrics;
    }

    private static TimeSpan CalculateTotalTimeOpen(PullRequestDto pr)
    {
        var endDate = pr.CompletedDate ?? DateTimeOffset.UtcNow;
        return endDate - pr.CreatedDate;
    }

    private static TimeSpan? CalculateEffectiveWorkTime(PullRequestDto pr, IReadOnlyList<PullRequestIterationDto> iterations)
    {
        if (iterations.Count == 0)
        {
            return null;
        }

        // Effective work time excludes waiting periods between iterations
        // This is a simplified calculation: sum of time between iteration creation dates
        TimeSpan effectiveTime = TimeSpan.Zero;

        for (int i = 0; i < iterations.Count; i++)
        {
            if (i == 0)
            {
                // First iteration: time from PR creation to iteration creation
                effectiveTime += iterations[i].CreatedDate - pr.CreatedDate;
            }
            else
            {
                // Subsequent iterations: time from previous iteration to current
                effectiveTime += iterations[i].CreatedDate - iterations[i - 1].UpdatedDate;
            }
        }

        // Add time from last iteration to completion (or now if still open)
        var lastIteration = iterations[^1];
        var endDate = pr.CompletedDate ?? DateTimeOffset.UtcNow;
        effectiveTime += endDate - lastIteration.UpdatedDate;

        return effectiveTime;
    }
}
