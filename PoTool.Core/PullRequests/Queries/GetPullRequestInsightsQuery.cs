using Mediator;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to compute Pull Request Insights for a team over a date range.
/// Uses only cached data — no live TFS/Azure DevOps calls.
/// </summary>
/// <param name="TeamId">Optional team ID. When provided, PRs are scoped to products linked to this team.</param>
/// <param name="FromDate">Start of the date range (PR created date lower bound).</param>
/// <param name="ToDate">End of the date range (PR created date upper bound).</param>
/// <param name="RepositoryName">Optional repository name filter.</param>
public sealed record GetPullRequestInsightsQuery(
    int? TeamId,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    string? RepositoryName = null
) : IQuery<PoTool.Shared.PullRequests.PullRequestInsightsDto>;
