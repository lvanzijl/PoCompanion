using PoTool.Core.Domain.Models;

namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Minimal work item data required by sprint delivery projection calculations.
/// </summary>
public sealed record DeliveryTrendWorkItem(
    int WorkItemId,
    string WorkItemType,
    string Title,
    int? ParentWorkItemId,
    string? State,
    string? IterationPath,
    int? Effort,
    int? StoryPoints,
    int? BusinessValue,
    DateTimeOffset? CreatedDate,
    double? TimeCriticality = null);

/// <summary>
/// Product-scoped hierarchy resolution data required by sprint delivery projection calculations.
/// </summary>
public sealed record DeliveryTrendResolvedWorkItem(
    int WorkItemId,
    string WorkItemType,
    int? ResolvedProductId,
    int? ResolvedFeatureId,
    int? ResolvedEpicId,
    int? ResolvedSprintId);

/// <summary>
/// Prepared domain inputs required to compute one sprint/product delivery projection.
/// </summary>
public sealed record SprintDeliveryProjectionRequest(
    SprintDefinition Sprint,
    int ProductId,
    IReadOnlyList<DeliveryTrendResolvedWorkItem> ResolvedItems,
    IReadOnlyDictionary<int, DeliveryTrendWorkItem> WorkItemsById,
    IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> ActivityByWorkItem,
    DateTimeOffset SprintStart,
    DateTimeOffset SprintEnd,
    IReadOnlySet<int> CommittedWorkItemIds,
    IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? StateLookup = null,
    IReadOnlyDictionary<int, DateTimeOffset>? FirstDoneByWorkItem = null,
    string? NextSprintPath = null,
    IReadOnlyDictionary<int, WorkItemSnapshot>? WorkItemSnapshotsById = null,
    IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? StateEventsByWorkItem = null,
    IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>>? IterationEventsByWorkItem = null);

/// <summary>
/// Prepared domain inputs required to compute sprint progression delta.
/// </summary>
public sealed record SprintDeliveryProgressionRequest(
    IReadOnlyList<DeliveryTrendResolvedWorkItem> ResolvedItems,
    IReadOnlyDictionary<int, DeliveryTrendWorkItem> WorkItemsById,
    IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> ActivityByWorkItem,
    IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? StateLookup = null);

/// <summary>
/// Prepared domain inputs required to compute feature progress rollups.
/// </summary>
public sealed record DeliveryFeatureProgressRequest(
    IReadOnlyList<DeliveryTrendResolvedWorkItem> ResolvedItems,
    IReadOnlyDictionary<int, DeliveryTrendWorkItem> WorkItemsById,
    IReadOnlyList<int> ProductIds,
    IReadOnlyCollection<int>? ActiveWorkItemIds = null,
    IReadOnlyCollection<int>? SprintCompletedPbiIds = null,
    IReadOnlyDictionary<int, int>? SprintEffortDeltaByWorkItem = null,
    IReadOnlyCollection<int>? SprintAssignedPbiIds = null,
    IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? StateLookup = null,
    FeatureProgressMode Mode = FeatureProgressMode.StoryPoints);

/// <summary>
/// Prepared domain inputs required to compute epic progress rollups.
/// </summary>
public sealed record DeliveryEpicProgressRequest(
    IReadOnlyList<FeatureProgress> FeatureProgress,
    IReadOnlyDictionary<int, DeliveryTrendWorkItem> WorkItemsById,
    IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? StateLookup = null);
