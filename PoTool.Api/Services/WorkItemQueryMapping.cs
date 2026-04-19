using System.Text.Json;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

internal static class WorkItemQueryMapping
{
    public static WorkItemDto MapToDto(WorkItemEntity entity)
    {
        List<WorkItemRelation>? relations = null;
        if (!string.IsNullOrEmpty(entity.Relations))
        {
            try
            {
                relations = JsonSerializer.Deserialize<List<WorkItemRelation>>(entity.Relations);
            }
            catch (JsonException)
            {
                // Ignore deserialization errors to preserve current cache-read behavior.
            }
        }

        return new WorkItemDto(
            TfsId: entity.TfsId,
            Type: entity.Type,
            Title: entity.Title,
            ParentTfsId: entity.ParentTfsId,
            AreaPath: entity.AreaPath,
            IterationPath: entity.IterationPath,
            State: entity.State,
            RetrievedAt: entity.RetrievedAt,
            Effort: entity.Effort,
            Description: entity.Description,
            CreatedDate: entity.CreatedDate,
            ClosedDate: entity.ClosedDate,
            Severity: entity.Severity,
            Tags: entity.Tags,
            IsBlocked: entity.IsBlocked,
            Relations: relations,
            ChangedDate: entity.TfsChangedDate,
            BusinessValue: entity.BusinessValue,
            BacklogPriority: entity.BacklogPriority,
            StoryPoints: entity.StoryPoints,
            TimeCriticality: entity.TimeCriticality,
            ProjectNumber: entity.ProjectNumber,
            ProjectElement: entity.ProjectElement,
            StartDate: entity.StartDate,
            TargetDate: entity.TargetDate
        );
    }
}
