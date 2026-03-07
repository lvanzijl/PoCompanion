namespace PoTool.Client.Services;

/// <summary>
/// Service for creating roadmap snapshots and detecting drift between
/// a snapshot and the current roadmap state.
/// Read-only — never modifies TFS data. Snapshots are stored application-side.
/// </summary>
public class RoadmapSnapshotService
{
    /// <summary>
    /// Creates a snapshot from the current roadmap state.
    /// </summary>
    /// <param name="lanes">Current roadmap product entries (already filtered and ordered).</param>
    /// <param name="description">Optional description for the snapshot.</param>
    /// <returns>A new <see cref="RoadmapSnapshot"/>.</returns>
    public RoadmapSnapshot CreateSnapshot(IReadOnlyList<RoadmapProductEntry> lanes, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(lanes);

        return new RoadmapSnapshot
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Description = description,
            Products = lanes.Select(lane => new SnapshotProductEntry
            {
                ProductName = lane.ProductName,
                Epics = lane.Epics.Select(e => new SnapshotEpicEntry
                {
                    Order = e.Order,
                    Title = e.Title,
                    TfsId = e.TfsId
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Compares a snapshot against the current roadmap state and produces a drift report.
    /// Detects: order changes, added epics, removed epics.
    /// Comparison is based on TFS ID (stable identifier), not title.
    /// </summary>
    /// <param name="snapshot">The snapshot to compare from.</param>
    /// <param name="currentLanes">Current roadmap product entries.</param>
    /// <returns>A <see cref="DriftReport"/> describing differences.</returns>
    public DriftReport Compare(RoadmapSnapshot snapshot, IReadOnlyList<RoadmapProductEntry> currentLanes)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(currentLanes);

        // Build lookup of current lanes by product name
        var currentByName = currentLanes.ToDictionary(l => l.ProductName, l => l);

        var productDrifts = new List<ProductDrift>();

        // Check each snapshot product
        foreach (var snapshotProduct in snapshot.Products)
        {
            currentByName.TryGetValue(snapshotProduct.ProductName, out var currentProduct);
            var currentEpics = currentProduct?.Epics ?? [];

            var epicDrifts = CompareEpics(snapshotProduct.Epics, currentEpics);
            productDrifts.Add(new ProductDrift
            {
                ProductName = snapshotProduct.ProductName,
                Epics = epicDrifts
            });
        }

        // Check for products in current that are not in snapshot
        foreach (var currentLane in currentLanes)
        {
            if (snapshot.Products.Any(p => p.ProductName == currentLane.ProductName))
                continue;

            // New product — all its epics are "Added"
            var epicDrifts = currentLane.Epics.Select(e => new EpicDrift
            {
                TfsId = e.TfsId,
                Title = e.Title,
                ChangeType = DriftChangeType.Added,
                SnapshotOrder = null,
                CurrentOrder = e.Order
            }).ToList();

            productDrifts.Add(new ProductDrift
            {
                ProductName = currentLane.ProductName,
                Epics = epicDrifts
            });
        }

        return new DriftReport
        {
            SnapshotId = snapshot.Id,
            SnapshotCreatedAtUtc = snapshot.CreatedAtUtc,
            ComparedAtUtc = DateTimeOffset.UtcNow,
            Products = productDrifts
        };
    }

    private static List<EpicDrift> CompareEpics(
        IReadOnlyList<SnapshotEpicEntry> snapshotEpics,
        IReadOnlyList<RoadmapEpicEntry> currentEpics)
    {
        var currentByTfsId = currentEpics.ToDictionary(e => e.TfsId, e => e);
        var snapshotByTfsId = snapshotEpics.ToDictionary(e => e.TfsId, e => e);

        var drifts = new List<EpicDrift>();

        // Check each snapshot epic against current
        foreach (var snapshotEpic in snapshotEpics)
        {
            if (currentByTfsId.TryGetValue(snapshotEpic.TfsId, out var currentEpic))
            {
                // Epic exists in both — check for order change
                var changeType = DriftChangeType.Unchanged;
                if (currentEpic.Order < snapshotEpic.Order)
                    changeType = DriftChangeType.MovedEarlier;
                else if (currentEpic.Order > snapshotEpic.Order)
                    changeType = DriftChangeType.MovedLater;

                drifts.Add(new EpicDrift
                {
                    TfsId = snapshotEpic.TfsId,
                    Title = currentEpic.Title, // Use current title (may have changed)
                    ChangeType = changeType,
                    SnapshotOrder = snapshotEpic.Order,
                    CurrentOrder = currentEpic.Order
                });
            }
            else
            {
                // Epic was in snapshot but not in current — removed
                drifts.Add(new EpicDrift
                {
                    TfsId = snapshotEpic.TfsId,
                    Title = snapshotEpic.Title,
                    ChangeType = DriftChangeType.Removed,
                    SnapshotOrder = snapshotEpic.Order,
                    CurrentOrder = null
                });
            }
        }

        // Check for epics in current that are not in snapshot — added
        foreach (var currentEpic in currentEpics)
        {
            if (!snapshotByTfsId.ContainsKey(currentEpic.TfsId))
            {
                drifts.Add(new EpicDrift
                {
                    TfsId = currentEpic.TfsId,
                    Title = currentEpic.Title,
                    ChangeType = DriftChangeType.Added,
                    SnapshotOrder = null,
                    CurrentOrder = currentEpic.Order
                });
            }
        }

        // Sort by current order (added/unchanged first), then removed at end
        return drifts
            .OrderBy(d => d.ChangeType == DriftChangeType.Removed ? 1 : 0)
            .ThenBy(d => d.CurrentOrder ?? int.MaxValue)
            .ToList();
    }
}

// --- Snapshot Data Model ---

/// <summary>
/// A point-in-time capture of the roadmap state.
/// Stored application-side. Never modifies TFS.
/// </summary>
public sealed record RoadmapSnapshot
{
    public string Id { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<SnapshotProductEntry> Products { get; init; } = [];
}

/// <summary>
/// A product entry within a snapshot.
/// </summary>
public sealed record SnapshotProductEntry
{
    public string ProductName { get; init; } = string.Empty;
    public IReadOnlyList<SnapshotEpicEntry> Epics { get; init; } = [];
}

/// <summary>
/// An epic entry within a snapshot product.
/// </summary>
public sealed record SnapshotEpicEntry
{
    public int Order { get; init; }
    public string Title { get; init; } = string.Empty;
    public int TfsId { get; init; }
}

// --- Drift Detection Model ---

/// <summary>
/// Result of comparing a snapshot against the current roadmap state.
/// </summary>
public sealed record DriftReport
{
    public string SnapshotId { get; init; } = string.Empty;
    public DateTimeOffset SnapshotCreatedAtUtc { get; init; }
    public DateTimeOffset ComparedAtUtc { get; init; }
    public IReadOnlyList<ProductDrift> Products { get; init; } = [];
    public bool HasDrift => Products.Any(p => p.HasDrift);
}

/// <summary>
/// Drift results for a single product.
/// </summary>
public sealed record ProductDrift
{
    public string ProductName { get; init; } = string.Empty;
    public IReadOnlyList<EpicDrift> Epics { get; init; } = [];
    public bool HasDrift => Epics.Any(e => e.ChangeType != DriftChangeType.Unchanged);
}

/// <summary>
/// Drift result for a single epic.
/// </summary>
public sealed record EpicDrift
{
    public int TfsId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DriftChangeType ChangeType { get; init; }
    public int? SnapshotOrder { get; init; }
    public int? CurrentOrder { get; init; }
}

/// <summary>
/// Classification of how an epic changed between snapshot and current state.
/// </summary>
public enum DriftChangeType
{
    Unchanged,
    MovedEarlier,
    MovedLater,
    Added,
    Removed
}
