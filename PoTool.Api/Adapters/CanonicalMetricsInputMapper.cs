using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.Models;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Adapters;

internal static class CanonicalMetricsInputMapper
{
    public static CanonicalWorkItem ToCanonicalWorkItem(this WorkItemEntity entity)
    {
        return new CanonicalWorkItem(
            entity.TfsId,
            entity.Type,
            entity.ParentTfsId,
            entity.BusinessValue,
            entity.StoryPoints,
            entity.TimeCriticality,
            entity.ProjectNumber,
            entity.ProjectElement);
    }

    public static CanonicalWorkItem ToCanonicalWorkItem(this WorkItemDto dto)
    {
        return new CanonicalWorkItem(
            dto.TfsId,
            dto.Type,
            dto.ParentTfsId,
            dto.BusinessValue,
            dto.StoryPoints,
            dto.TimeCriticality,
            dto.ProjectNumber,
            dto.ProjectElement);
    }
}
