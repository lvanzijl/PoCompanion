using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

public class RelationRevisionHydrator : IRelationRevisionHydrator
{
    public Task<RelationHydrationResult> HydrateAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        // REPLACE_WITH_ACTIVITY_SOURCE: hydrate relationship deltas from activity events.
        return Task.FromResult(new RelationHydrationResult
        {
            Success = true,
            WorkItemsProcessed = workItemIds.Distinct().Count(),
            RevisionsHydrated = 0
        });
    }
}
