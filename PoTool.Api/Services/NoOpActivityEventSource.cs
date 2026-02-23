using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

public sealed class NoOpActivityEventSource : IActivityEventSource
{
    public Task<IReadOnlyList<ActivityEvent>> GetActivityEventsAsync(
        IReadOnlyCollection<int> workItemIds,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        // REPLACE_WITH_ACTIVITY_SOURCE: provide activity events for sprint trend and relation resolution.
        return Task.FromResult<IReadOnlyList<ActivityEvent>>(Array.Empty<ActivityEvent>());
    }
}
