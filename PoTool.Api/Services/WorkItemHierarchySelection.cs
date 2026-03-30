using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

internal static class WorkItemHierarchySelection
{
    public static IReadOnlySet<int> ExpandToDescendantIds(
        IReadOnlyList<WorkItemEntity> allEntities,
        IReadOnlyCollection<int> rootWorkItemIds)
    {
        var includedIds = new HashSet<int>(rootWorkItemIds);

        bool changed;
        do
        {
            changed = false;
            foreach (var entity in allEntities)
            {
                if (entity.ParentTfsId.HasValue
                    && includedIds.Contains(entity.ParentTfsId.Value)
                    && includedIds.Add(entity.TfsId))
                {
                    changed = true;
                }
            }
        } while (changed);

        return includedIds;
    }
}
