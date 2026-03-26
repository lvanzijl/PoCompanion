using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.WorkItems;

namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Minimal delivery-trend work item data required by sprint delivery calculations.
/// Irrelevant funding/override fields are normalized away in the domain model.
/// </summary>
public sealed record DeliveryTrendWorkItem
{
    public DeliveryTrendWorkItem(
        int workItemId,
        string workItemType,
        string title,
        int? parentWorkItemId,
        string? state,
        string? iterationPath,
        int? effort,
        int? storyPoints,
        int? businessValue,
        DateTimeOffset? createdDate,
        double? timeCriticality = null,
        string? projectNumber = null,
        string? projectElement = null)
    {
        CanonicalWorkItemTypes.EnsureCanonical(workItemType, nameof(workItemType));

        WorkItemId = workItemId;
        WorkItemType = workItemType;
        Title = title;
        ParentWorkItemId = parentWorkItemId;
        State = state;
        IterationPath = iterationPath;
        Effort = effort;
        StoryPoints = storyPoints;
        BusinessValue = businessValue;
        CreatedDate = createdDate;
        TimeCriticality = WorkItemFieldSemantics.NormalizeTimeCriticality(workItemType, timeCriticality);
        ProjectNumber = WorkItemFieldSemantics.NormalizeProjectNumber(workItemType, projectNumber);
        ProjectElement = WorkItemFieldSemantics.NormalizeProjectElement(workItemType, projectElement);
    }

    public int WorkItemId { get; }

    public string WorkItemType { get; }

    public string Title { get; }

    public int? ParentWorkItemId { get; }

    public string? State { get; }

    public string? IterationPath { get; }

    public int? Effort { get; }

    public int? StoryPoints { get; }

    public int? BusinessValue { get; }

    public DateTimeOffset? CreatedDate { get; }

    public double? TimeCriticality { get; }

    public string? ProjectNumber { get; }

    public string? ProjectElement { get; }
}

/// <summary>
/// Product-scoped hierarchy resolution data required by sprint delivery projection calculations.
/// </summary>
public sealed record DeliveryTrendResolvedWorkItem
{
    public DeliveryTrendResolvedWorkItem(
        int workItemId,
        string workItemType,
        int? resolvedProductId,
        int? resolvedFeatureId,
        int? resolvedEpicId,
        int? resolvedSprintId)
    {
        CanonicalWorkItemTypes.EnsureCanonical(workItemType, nameof(workItemType));

        WorkItemId = workItemId;
        WorkItemType = workItemType;
        ResolvedProductId = resolvedProductId;
        ResolvedFeatureId = resolvedFeatureId;
        ResolvedEpicId = resolvedEpicId;
        ResolvedSprintId = resolvedSprintId;
    }

    public int WorkItemId { get; }

    public string WorkItemType { get; }

    public int? ResolvedProductId { get; }

    public int? ResolvedFeatureId { get; }

    public int? ResolvedEpicId { get; }

    public int? ResolvedSprintId { get; }
}

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
    FeatureProgressMode Mode,
    IReadOnlyCollection<int>? ActiveWorkItemIds = null,
    IReadOnlyCollection<int>? SprintCompletedPbiIds = null,
    IReadOnlyDictionary<int, int>? SprintEffortDeltaByWorkItem = null,
    IReadOnlyCollection<int>? SprintAssignedPbiIds = null,
    IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? StateLookup = null);

/// <summary>
/// Prepared domain inputs required to compute epic progress rollups.
/// </summary>
public sealed record DeliveryEpicProgressRequest(
    IReadOnlyList<FeatureProgress> FeatureProgress,
    IReadOnlyDictionary<int, DeliveryTrendWorkItem> WorkItemsById,
    IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? StateLookup = null);
