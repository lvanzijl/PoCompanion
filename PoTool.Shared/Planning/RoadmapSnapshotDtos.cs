namespace PoTool.Shared.Planning;

/// <summary>
/// Summary DTO for a roadmap snapshot (used in list views).
/// </summary>
public sealed record RoadmapSnapshotDto(
    int Id,
    DateTimeOffset CreatedAtUtc,
    string? Description,
    int ProductCount,
    int EpicCount
);

/// <summary>
/// Full snapshot detail including all captured items.
/// </summary>
public sealed record RoadmapSnapshotDetailDto(
    int Id,
    DateTimeOffset CreatedAtUtc,
    string? Description,
    IReadOnlyList<RoadmapSnapshotProductDto> Products
);

/// <summary>
/// A product within a snapshot detail.
/// </summary>
public sealed record RoadmapSnapshotProductDto(
    string ProductName,
    IReadOnlyList<RoadmapSnapshotEpicDto> Epics
);

/// <summary>
/// An epic within a snapshot product.
/// </summary>
public sealed record RoadmapSnapshotEpicDto(
    int EpicTfsId,
    string EpicTitle,
    int EpicOrder
);

/// <summary>
/// Request DTO for creating a snapshot from the current roadmap state.
/// </summary>
public sealed record CreateRoadmapSnapshotRequest(
    string? Description,
    IReadOnlyList<CreateRoadmapSnapshotProductRequest> Products
);

/// <summary>
/// A product entry in a snapshot creation request.
/// </summary>
public sealed record CreateRoadmapSnapshotProductRequest(
    string ProductName,
    IReadOnlyList<CreateRoadmapSnapshotEpicRequest> Epics
);

/// <summary>
/// An epic entry in a snapshot creation request.
/// </summary>
public sealed record CreateRoadmapSnapshotEpicRequest(
    int EpicTfsId,
    string EpicTitle,
    int EpicOrder
);
