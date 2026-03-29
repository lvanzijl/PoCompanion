using PoTool.Core.PullRequests.Filters;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

public interface IPullRequestQueryStore
{
    Task<PullRequestInsightsQueryData> GetInsightsDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken);

    Task<PrDeliveryInsightsQueryData> GetDeliveryInsightsDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken);

    Task<PrSprintTrendQueryData> GetSprintTrendDataAsync(
        PullRequestEffectiveFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PullRequestDto>> GetByWorkItemIdAsync(
        int workItemId,
        CancellationToken cancellationToken);
}

public sealed record PullRequestConfigurationInfo(
    string? Url,
    string? Project);

public sealed record PullRequestInsightsQueryData(
    PullRequestConfigurationInfo? Configuration,
    string? TeamName,
    IReadOnlyList<PullRequestDto> PullRequests,
    IReadOnlyDictionary<int, int> IterationsByPullRequestId,
    IReadOnlyDictionary<int, int> CommentsByPullRequestId,
    IReadOnlyDictionary<int, int> DistinctFilesByPullRequestId);

public sealed record PullRequestWorkItemNode(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId);

public sealed record PrDeliveryInsightsQueryData(
    string? TeamName,
    string? SprintName,
    IReadOnlyList<PullRequestDto> PullRequests,
    IReadOnlyDictionary<int, int> IterationsByPullRequestId,
    IReadOnlyDictionary<int, int> DistinctFilesByPullRequestId,
    IReadOnlyDictionary<int, IReadOnlyList<int>> LinkedWorkItemIdsByPullRequestId,
    IReadOnlyDictionary<int, PullRequestWorkItemNode> WorkItemsById,
    IReadOnlyDictionary<int, int> PbiCountByFeatureId);

public sealed record PrSprintWindow(
    int Id,
    string Name,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc);

public sealed record PrSprintTrendPullRequest(
    int Id,
    string CreatedBy,
    DateTime CreatedDateUtc,
    DateTimeOffset? CompletedDate,
    string Status);

public sealed record PrSprintTrendFileChange(
    int PullRequestId,
    string FilePath,
    int LinesAdded,
    int LinesDeleted);

public sealed record PrSprintTrendComment(
    int PullRequestId,
    string Author,
    DateTime CreatedDateUtc);

public sealed record PrSprintTrendQueryData(
    IReadOnlyList<PrSprintWindow> Sprints,
    IReadOnlyList<PrSprintTrendPullRequest> PullRequests,
    IReadOnlyList<PrSprintTrendFileChange> FileChanges,
    IReadOnlyList<PrSprintTrendComment> Comments);
