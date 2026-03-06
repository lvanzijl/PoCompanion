using Mediator;
using PoTool.Shared.PullRequests;

namespace PoTool.Core.PullRequests.Queries;

/// <summary>
/// Query to compute PR Delivery Insights for a team over a date range.
/// PRs are classified by their linked work items and traced to Features and Epics.
/// Uses only cached data — no live TFS/Azure DevOps calls.
/// </summary>
/// <param name="TeamId">Optional team ID. When provided, PRs are scoped to products linked to that team.</param>
/// <param name="SprintId">Optional sprint ID. When provided, FromDate and ToDate are derived from the sprint boundaries.</param>
/// <param name="FromDate">Start of the date range (PR created date lower bound).</param>
/// <param name="ToDate">End of the date range (PR created date upper bound).</param>
public sealed record GetPrDeliveryInsightsQuery(
    int? TeamId,
    int? SprintId,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate
) : IQuery<PrDeliveryInsightsDto>;
