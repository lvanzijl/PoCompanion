using PoTool.Core.Domain.EffortPlanning;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Adapters;

internal static class EffortPlanningInputMapper
{
    public static EffortPlanningWorkItem ToEffortPlanningWorkItem(this WorkItemDto dto)
    {
        return new EffortPlanningWorkItem(
            dto.TfsId,
            dto.Type ?? string.Empty,
            dto.Title ?? string.Empty,
            dto.AreaPath ?? string.Empty,
            dto.IterationPath ?? string.Empty,
            dto.State ?? string.Empty,
            dto.RetrievedAt,
            dto.Effort);
    }
}
