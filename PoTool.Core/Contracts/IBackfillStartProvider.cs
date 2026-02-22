namespace PoTool.Core.Contracts;

/// <summary>
/// Provides the earliest relevant timestamp for V2 backfill start derivation.
/// </summary>
public interface IBackfillStartProvider
{
    /// <summary>
    /// Returns the earliest ChangedDate (UTC) for the given work item IDs,
    /// or null if it cannot be determined.
    /// </summary>
    Task<DateTimeOffset?> GetEarliestChangedDateUtcAsync(
        IReadOnlyCollection<int> workItemIds,
        CancellationToken cancellationToken = default);
}
