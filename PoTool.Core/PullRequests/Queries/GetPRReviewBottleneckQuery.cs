using Mediator;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to get PR review bottleneck analysis.
/// Analyzes reviewer performance and identifies bottlenecks in the review process.
/// </summary>
public sealed record GetPRReviewBottleneckQuery(
    int MaxPRsToAnalyze = 100,
    int DaysBack = 30
) : IQuery<PRReviewBottleneckDto>;
