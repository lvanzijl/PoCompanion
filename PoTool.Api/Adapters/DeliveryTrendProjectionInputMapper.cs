using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Api.Adapters;

internal static class DeliveryTrendProjectionInputMapper
{
    public static DeliveryTrendWorkItem ToDeliveryTrendWorkItem(this WorkItemEntity entity)
    {
        return new DeliveryTrendWorkItem(
            entity.TfsId,
            entity.Type.ToCanonicalWorkItemType(),
            entity.Title,
            entity.ParentTfsId,
            entity.State,
            entity.IterationPath,
            entity.Effort,
            entity.StoryPoints,
            entity.BusinessValue,
            entity.CreatedDate,
            entity.TimeCriticality,
            entity.ProjectNumber,
            entity.ProjectElement);
    }

    public static DeliveryTrendResolvedWorkItem ToDeliveryTrendResolvedWorkItem(this ResolvedWorkItemEntity entity)
    {
        return new DeliveryTrendResolvedWorkItem(
            entity.WorkItemId,
            entity.WorkItemType.ToCanonicalWorkItemType(),
            entity.ResolvedProductId,
            entity.ResolvedFeatureId,
            entity.ResolvedEpicId,
            entity.ResolvedSprintId);
    }
}
