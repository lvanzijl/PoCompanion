using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Repositories;

/// <summary>
/// Development-only in-memory repository that returns a generated hierarchy of dummy work items.
/// Used to develop UI without a TFS connection.
/// </summary>
public class DevWorkItemRepository : IWorkItemRepository
{
    private readonly List<WorkItemDto> _items;

    public DevWorkItemRepository()
    {
        _items = GenerateDummyWorkItems();
    }

    public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_items.AsEnumerable());
    }

    public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return Task.FromResult(_items.AsEnumerable());

        var f = filter.Trim();
        var result = _items.Where(w => w.Title.Contains(f, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
    }

    public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(w => w.TfsId == tfsId);
        return Task.FromResult(item);
    }

    public Task ReplaceAllAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default)
    {
        // For dev repository, just replace the in-memory list
        _items.Clear();
        _items.AddRange(workItems);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a fresh hierarchy of dummy work items (spaceship backlog) including some orphaned items.
    /// </summary>
    public List<WorkItemDto> GenerateDummyWorkItems()
    {
        var list = new List<WorkItemDto>();
        var id = 1000;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Helper to create JsonPayload with empty description
        string MakePayload(string type)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { Description = string.Empty, Type = type });
        }

        // Create 7 goals
        for (int g = 1; g <= 7; g++)
        {
            var goalId = id++;
            var goalTitle = g switch
            {
                1 => "Goal 1: Define Mission Requirements",
                2 => "Goal 2: Design Propulsion System",
                3 => "Goal 3: Build Structural Frame",
                4 => "Goal 4: Implement Avionics",
                5 => "Goal 5: Integrate Life Support",
                6 => "Goal 6: Prepare Launch Facilities",
                7 => "Goal 7: Validate Mission Operations",
                _ => $"Goal {g}"
            };

            list.Add(new WorkItemDto(
                TfsId: goalId,
                Type: "Goal",
                Title: goalTitle,
                ParentTfsId: null,
                AreaPath: "Spaceship",
                IterationPath: "Backlog",
                State: "Active",
                JsonPayload: MakePayload("Goal"),
                RetrievedAt: now
            ));

            // 2 objectives per goal
            for (int o = 1; o <= 2; o++)
            {
                var objId = id++;
                var objectiveTitle = $"{goalTitle} - Objective {o}";
                list.Add(new WorkItemDto(
                    TfsId: objId,
                    Type: "Objective",
                    Title: objectiveTitle,
                    ParentTfsId: goalId,
                    AreaPath: "Spaceship",
                    IterationPath: "Backlog",
                    State: "Active",
                    JsonPayload: MakePayload("Objective"),
                    RetrievedAt: now
                ));

                // 2 epics per objective
                for (int e = 1; e <= 2; e++)
                {
                    var epicId = id++;
                    var epicTitle = $"{objectiveTitle} - Epic {e}";
                    list.Add(new WorkItemDto(
                        TfsId: epicId,
                        Type: "Epic",
                        Title: epicTitle,
                        ParentTfsId: objId,
                        AreaPath: "Spaceship",
                        IterationPath: "Backlog",
                        State: "Active",
                        JsonPayload: MakePayload("Epic"),
                        RetrievedAt: now
                    ));

                    // 2 features per epic
                    for (int f = 1; f <= 2; f++)
                    {
                        var featureId = id++;
                        var featureTitle = $"{epicTitle} - Feature {f}";
                        list.Add(new WorkItemDto(
                            TfsId: featureId,
                            Type: "Feature",
                            Title: featureTitle,
                            ParentTfsId: epicId,
                            AreaPath: "Spaceship",
                            IterationPath: "Backlog",
                            State: "Active",
                            JsonPayload: MakePayload("Feature"),
                            RetrievedAt: now
                        ));

                        // 2 PBIs per feature
                        for (int p = 1; p <= 2; p++)
                        {
                            var pbiId = id++;
                            var pbiTitle = $"{featureTitle} - PBI {p}";
                            list.Add(new WorkItemDto(
                                TfsId: pbiId,
                                Type: "PBI",
                                Title: pbiTitle,
                                ParentTfsId: featureId,
                                AreaPath: "Spaceship",
                                IterationPath: "Sprint 1",
                                State: "Active",
                                JsonPayload: MakePayload("PBI"),
                                RetrievedAt: now
                            ));

                            // 2 tasks per PBI
                            for (int t = 1; t <= 2; t++)
                            {
                                var taskId = id++;
                                var taskTitle = $"{pbiTitle} - Task {t}";
                                list.Add(new WorkItemDto(
                                    TfsId: taskId,
                                    Type: "Task",
                                    Title: taskTitle,
                                    ParentTfsId: pbiId,
                                    AreaPath: "Spaceship",
                                    IterationPath: "Sprint 1",
                                    State: "Active",
                                    JsonPayload: MakePayload("Task"),
                                    RetrievedAt: now
                                ));
                            }
                        }
                    }
                }
            }
        }

        // Add orphaned items at various levels
        var orphanBase = id + 100;
        // Orphan goal-level (no parent -> still root but mark as orphan title)
        var orphanGoalId = orphanBase++;
        list.Add(new WorkItemDto(
            TfsId: orphanGoalId,
            Type: "Goal",
            Title: "Orphan Goal: Experimental Module",
            ParentTfsId: null,
            AreaPath: "Spaceship",
            IterationPath: "Backlog",
            State: "Active",
            JsonPayload: MakePayload("Goal"),
            RetrievedAt: now
        ));

        // Orphan objective with missing parent (parent id points to a non-existent goal)
        var missingGoalId = 99999;
        var orphanObjectiveId = orphanBase++;
        list.Add(new WorkItemDto(
            TfsId: orphanObjectiveId,
            Type: "Objective",
            Title: "Orphan Objective: Unlinked Objective",
            ParentTfsId: missingGoalId,
            AreaPath: "Spaceship",
            IterationPath: "Backlog",
            State: "Active",
            JsonPayload: MakePayload("Objective"),
            RetrievedAt: now
        ));

        // Orphan epic with missing parent
        var missingObjectiveId = 99998;
        var orphanEpicId = orphanBase++;
        list.Add(new WorkItemDto(
            TfsId: orphanEpicId,
            Type: "Epic",
            Title: "Orphan Epic: Ghost Epic",
            ParentTfsId: missingObjectiveId,
            AreaPath: "Spaceship",
            IterationPath: "Backlog",
            State: "Active",
            JsonPayload: MakePayload("Epic"),
            RetrievedAt: now
        ));

        // Orphan feature with missing parent
        var missingEpicId = 99997;
        var orphanFeatureId = orphanBase++;
        list.Add(new WorkItemDto(
            TfsId: orphanFeatureId,
            Type: "Feature",
            Title: "Orphan Feature: Phantom Feature",
            ParentTfsId: missingEpicId,
            AreaPath: "Spaceship",
            IterationPath: "Backlog",
            State: "Active",
            JsonPayload: MakePayload("Feature"),
            RetrievedAt: now
        ));

        // Orphan PBI with missing parent
        var missingFeatureId = 99996;
        var orphanPbiId = orphanBase++;
        list.Add(new WorkItemDto(
            TfsId: orphanPbiId,
            Type: "PBI",
            Title: "Orphan PBI: Lost Requirement",
            ParentTfsId: missingFeatureId,
            AreaPath: "Spaceship",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: MakePayload("PBI"),
            RetrievedAt: now
        ));

        // Orphan Task with missing parent
        var missingPbiId = 99995;
        var orphanTaskId = orphanBase++;
        list.Add(new WorkItemDto(
            TfsId: orphanTaskId,
            Type: "Task",
            Title: "Orphan Task: Stray Task",
            ParentTfsId: missingPbiId,
            AreaPath: "Spaceship",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: MakePayload("Task"),
            RetrievedAt: now
        ));

        return list;
    }
}
