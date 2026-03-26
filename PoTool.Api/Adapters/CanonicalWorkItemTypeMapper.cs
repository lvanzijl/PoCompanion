using PoTool.Core.Domain.WorkItems;
using RawWorkItemType = PoTool.Core.WorkItems.WorkItemType;

namespace PoTool.Api.Adapters;

internal static class CanonicalWorkItemTypeMapper
{
    public static string ToCanonicalWorkItemType(this string workItemType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workItemType);

        return workItemType.Trim() switch
        {
            RawWorkItemType.Goal => CanonicalWorkItemTypes.Goal,
            RawWorkItemType.Objective => CanonicalWorkItemTypes.Objective,
            RawWorkItemType.Epic => CanonicalWorkItemTypes.Epic,
            RawWorkItemType.Feature => CanonicalWorkItemTypes.Feature,
            RawWorkItemType.Pbi => CanonicalWorkItemTypes.Pbi,
            RawWorkItemType.PbiShort => CanonicalWorkItemTypes.Pbi,
            RawWorkItemType.UserStory => CanonicalWorkItemTypes.Pbi,
            RawWorkItemType.Bug => CanonicalWorkItemTypes.Bug,
            RawWorkItemType.Task => CanonicalWorkItemTypes.Task,
            _ => CanonicalWorkItemTypes.Other
        };
    }
}
