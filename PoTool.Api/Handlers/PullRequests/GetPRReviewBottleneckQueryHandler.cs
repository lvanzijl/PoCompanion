using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Api.Handlers.PullRequests;

/// <summary>
/// Handler for GetPRReviewBottleneckQuery.
/// Analyzes PR review performance and identifies bottlenecks.
/// </summary>
public sealed class GetPRReviewBottleneckQueryHandler
    : IQueryHandler<GetPRReviewBottleneckQuery, PRReviewBottleneckDto>
{
    private readonly IPullRequestRepository _repository;
    private readonly ILogger<GetPRReviewBottleneckQueryHandler> _logger;

    public GetPRReviewBottleneckQueryHandler(
        IPullRequestRepository repository,
        ILogger<GetPRReviewBottleneckQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<PRReviewBottleneckDto> Handle(
        GetPRReviewBottleneckQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Handling GetPRReviewBottleneckQuery with MaxPRs: {MaxPRs}, DaysBack: {DaysBack}",
            query.MaxPRsToAnalyze,
            query.DaysBack);

        var allPRs = await _repository.GetAllAsync(cancellationToken);

        // Filter to recent PRs
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-query.DaysBack);
        var recentPRs = allPRs
            .Where(pr => pr.CreatedDate >= cutoffDate)
            .OrderByDescending(pr => pr.CreatedDate)
            .Take(query.MaxPRsToAnalyze)
            .ToList();

        _logger.LogDebug("Analyzing {Count} pull requests", recentPRs.Count);

        // Calculate reviewer performance (simplified - using CreatedBy as proxy for reviewer)
        var reviewerPerformances = CalculateReviewerPerformance(recentPRs);

        // Find PRs waiting for review (active PRs)
        var prsWaitingForReview = CalculatePRsWaitingForReview(recentPRs);

        // Calculate summary metrics
        var summary = CalculateSummary(recentPRs, reviewerPerformances);

        return new PRReviewBottleneckDto(
            ReviewerPerformances: reviewerPerformances,
            PRsWaitingLongest: prsWaitingForReview,
            Summary: summary,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<ReviewerPerformance> CalculateReviewerPerformance(List<PullRequestDto> prs)
    {
        // Group by created by (simplified - in real implementation would use actual reviewers)
        var byReviewer = prs
            .GroupBy(pr => pr.CreatedBy)
            .Select(group =>
            {
                var completedPRs = group.Where(pr => pr.Status == "completed").ToList();
                var avgResponseTime = completedPRs.Any()
                    ? completedPRs.Average(pr => (pr.CompletedDate ?? pr.CreatedDate).Subtract(pr.CreatedDate).TotalHours)
                    : 0;

                var status = avgResponseTime switch
                {
                    < 4 => ReviewerStatus.Fast,
                    >= 4 and < 24 => ReviewerStatus.Normal,
                    >= 24 and < 48 => ReviewerStatus.Slow,
                    _ => ReviewerStatus.Bottleneck
                };

                return new ReviewerPerformance(
                    ReviewerName: group.Key,
                    TotalReviewsAssigned: group.Count(),
                    ReviewsCompleted: completedPRs.Count,
                    AverageResponseTimeHours: avgResponseTime,
                    MedianResponseTimeHours: avgResponseTime, // Simplified
                    PRsWaitingForReview: group.Count(pr => pr.Status == "active"),
                    Status: status
                );
            })
            .OrderByDescending(r => r.AverageResponseTimeHours)
            .ToList();

        return byReviewer;
    }

    private static List<PRWaitingForReview> CalculatePRsWaitingForReview(List<PullRequestDto> prs)
    {
        var activePRs = prs
            .Where(pr => pr.Status == "active")
            .Select(pr => new PRWaitingForReview(
                PullRequestId: pr.Id,
                Title: pr.Title,
                Author: pr.CreatedBy,
                CreatedDate: pr.CreatedDate,
                HoursWaiting: (DateTimeOffset.UtcNow - pr.CreatedDate).TotalHours,
                PendingReviewers: new List<string> { "Reviewers TBD" } // Simplified
            ))
            .OrderByDescending(pr => pr.HoursWaiting)
            .Take(10)
            .ToList();

        return activePRs;
    }

    private static ReviewMetricsSummary CalculateSummary(
        List<PullRequestDto> prs,
        List<ReviewerPerformance> reviewers)
    {
        var completedPRs = prs.Where(pr => pr.Status == "completed" && pr.CompletedDate.HasValue).ToList();

        var avgTimeToComplete = completedPRs.Any()
            ? completedPRs.Average(pr => (pr.CompletedDate!.Value - pr.CreatedDate).TotalHours)
            : 0;

        var totalPending = prs.Count(pr => pr.Status == "active");

        var bottleneckReviewer = reviewers
            .Where(r => r.Status == ReviewerStatus.Bottleneck || r.Status == ReviewerStatus.Slow)
            .OrderByDescending(r => r.AverageResponseTimeHours)
            .FirstOrDefault()?.ReviewerName ?? "None";

        var fastestReviewer = reviewers
            .Where(r => r.ReviewsCompleted > 0)
            .OrderBy(r => r.AverageResponseTimeHours)
            .FirstOrDefault()?.ReviewerName ?? "None";

        return new ReviewMetricsSummary(
            AverageTimeToFirstReviewHours: avgTimeToComplete / 2, // Simplified estimate
            AverageTimeToCompleteReviewsHours: avgTimeToComplete,
            TotalPRsPendingReview: totalPending,
            BottleneckReviewer: bottleneckReviewer,
            FastestReviewer: fastestReviewer
        );
    }
}
