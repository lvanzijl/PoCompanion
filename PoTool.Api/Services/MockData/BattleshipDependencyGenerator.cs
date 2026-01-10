using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Generates dependency links between work items following mock-data-rules.md requirements.
/// Creates 15,000-20,000 links with 30-40% cross-team dependencies.
/// </summary>
public class BattleshipDependencyGenerator
{
    private readonly Random _random;
    private const int Seed = 43; // Different seed from work items for variety

    public BattleshipDependencyGenerator()
    {
        _random = new Random(Seed);
    }

    /// <summary>
    /// Generates dependency links for the given work items
    /// </summary>
    public List<DependencyLink> GenerateDependencies(List<WorkItemDto> workItems)
    {
        var dependencies = new List<DependencyLink>();
        var linkIdCounter = 1;

        // Extract work items by type for efficient lookup
        var epics = workItems.Where(w => w.Type == WorkItemType.Epic).ToList();
        var features = workItems.Where(w => w.Type == WorkItemType.Feature).ToList();
        var pbis = workItems.Where(w => w.Type == WorkItemType.Pbi).ToList();
        var bugs = workItems.Where(w => w.Type == WorkItemType.Bug).ToList();

        // Generate dependencies at different levels
        // Epics: 30-40% have dependencies
        dependencies.AddRange(GenerateDependenciesForLevel(epics, workItems, 0.35, linkIdCounter, "Epic"));
        linkIdCounter = dependencies.Count + 1;

        // Features: 20-30% have dependencies
        dependencies.AddRange(GenerateDependenciesForLevel(features, workItems, 0.25, linkIdCounter, "Feature"));
        linkIdCounter = dependencies.Count + 1;

        // PBIs: 10-15% have dependencies
        dependencies.AddRange(GenerateDependenciesForLevel(pbis, workItems, 0.12, linkIdCounter, "PBI"));
        linkIdCounter = dependencies.Count + 1;

        // Bugs: 5-10% have dependencies
        dependencies.AddRange(GenerateDependenciesForLevel(bugs, workItems, 0.07, linkIdCounter, "Bug"));

        return dependencies;
    }

    private List<DependencyLink> GenerateDependenciesForLevel(
        List<WorkItemDto> sourceItems,
        List<WorkItemDto> allItems,
        double percentage,
        int startId,
        string level)
    {
        var dependencies = new List<DependencyLink>();
        var linkId = startId;

        // Determine how many items should have dependencies
        var itemsWithDependencies = (int)(sourceItems.Count * percentage);

        // Shuffle source items for randomness
        var shuffled = sourceItems.OrderBy(x => _random.Next()).Take(itemsWithDependencies).ToList();

        foreach (var sourceItem in shuffled)
        {
            // Each item can have 1-3 dependencies
            var dependencyCount = _random.Next(1, 4);

            for (var i = 0; i < dependencyCount; i++)
            {
                var linkType = GetRandomLinkType();
                var targetItem = GetRandomTargetItem(sourceItem, allItems);

                if (targetItem != null)
                {
                    dependencies.Add(new DependencyLink
                    {
                        Id = linkId++,
                        SourceWorkItemId = sourceItem.TfsId,
                        TargetWorkItemId = targetItem.TfsId,
                        LinkType = linkType,
                        SourceAreaPath = sourceItem.AreaPath,
                        TargetAreaPath = targetItem.AreaPath,
                        IsCrossTeam = IsCrossTeam(sourceItem.AreaPath, targetItem.AreaPath)
                    });
                }
            }
        }

        return dependencies;
    }

    private string GetRandomLinkType()
    {
        var linkTypes = new[] { "Predecessor", "Successor", "Related", "Duplicate" };
        var weights = new[] { 0.40, 0.40, 0.15, 0.05 };

        var rand = _random.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative)
                return linkTypes[i];
        }
        return linkTypes[0];
    }

    private WorkItemDto? GetRandomTargetItem(WorkItemDto sourceItem, List<WorkItemDto> allItems)
    {
        // Get items of the same type (mostly) or similar types
        var candidateTypes = sourceItem.Type switch
        {
            WorkItemType.Epic => new[] { WorkItemType.Epic },
            WorkItemType.Feature => new[] { WorkItemType.Feature, WorkItemType.Epic },
            WorkItemType.Pbi => new[] { WorkItemType.Pbi, WorkItemType.Feature },
            WorkItemType.Bug => new[] { WorkItemType.Bug, WorkItemType.Pbi },
            _ => new[] { sourceItem.Type }
        };

        var candidates = allItems
            .Where(w => candidateTypes.Contains(w.Type) && w.TfsId != sourceItem.TfsId)
            .ToList();

        if (!candidates.Any())
            return null;

        // Decide if this should be a cross-team dependency (target 30-40%)
        var shouldBeCrossTeam = _random.NextDouble() < 0.35;

        if (shouldBeCrossTeam)
        {
            // Find items from different teams
            var crossTeamCandidates = candidates
                .Where(c => IsCrossTeam(sourceItem.AreaPath, c.AreaPath))
                .ToList();

            if (crossTeamCandidates.Any())
                return crossTeamCandidates[_random.Next(crossTeamCandidates.Count)];
        }

        // Return a random candidate (same team or any team if cross-team not available)
        return candidates[_random.Next(candidates.Count)];
    }

    private bool IsCrossTeam(string areaPath1, string areaPath2)
    {
        // Extract team name (last segment of area path)
        var team1 = areaPath1.Split('\\').LastOrDefault() ?? "";
        var team2 = areaPath2.Split('\\').LastOrDefault() ?? "";
        return !string.Equals(team1, team2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates intentionally invalid dependencies for testing (5-10% of total)
    /// </summary>
    public List<DependencyLink> GenerateInvalidDependencies(List<WorkItemDto> workItems, int existingLinkCount)
    {
        var invalidDependencies = new List<DependencyLink>();
        var linkId = existingLinkCount + 1;

        // Calculate 5-10% of existing dependencies
        var invalidCount = (int)(existingLinkCount * 0.07);

        for (var i = 0; i < invalidCount; i++)
        {
            var invalidType = _random.Next(0, 4);

            switch (invalidType)
            {
                case 0: // Circular dependency (will be created in sets of 3)
                    if (i + 2 < invalidCount)
                    {
                        var circularItems = workItems
                            .Where(w => w.Type == WorkItemType.Feature)
                            .OrderBy(x => _random.Next())
                            .Take(3)
                            .ToList();

                        if (circularItems.Count == 3)
                        {
                            invalidDependencies.Add(new DependencyLink
                            {
                                Id = linkId++,
                                SourceWorkItemId = circularItems[0].TfsId,
                                TargetWorkItemId = circularItems[1].TfsId,
                                LinkType = "Predecessor",
                                SourceAreaPath = circularItems[0].AreaPath,
                                TargetAreaPath = circularItems[1].AreaPath,
                                IsCrossTeam = IsCrossTeam(circularItems[0].AreaPath, circularItems[1].AreaPath)
                            });

                            invalidDependencies.Add(new DependencyLink
                            {
                                Id = linkId++,
                                SourceWorkItemId = circularItems[1].TfsId,
                                TargetWorkItemId = circularItems[2].TfsId,
                                LinkType = "Predecessor",
                                SourceAreaPath = circularItems[1].AreaPath,
                                TargetAreaPath = circularItems[2].AreaPath,
                                IsCrossTeam = IsCrossTeam(circularItems[1].AreaPath, circularItems[2].AreaPath)
                            });

                            invalidDependencies.Add(new DependencyLink
                            {
                                Id = linkId++,
                                SourceWorkItemId = circularItems[2].TfsId,
                                TargetWorkItemId = circularItems[0].TfsId,
                                LinkType = "Predecessor",
                                SourceAreaPath = circularItems[2].AreaPath,
                                TargetAreaPath = circularItems[0].AreaPath,
                                IsCrossTeam = IsCrossTeam(circularItems[2].AreaPath, circularItems[0].AreaPath)
                            });
                            i += 2; // Skip next 2 iterations since we added 3 links
                        }
                    }
                    break;

                case 1: // Orphaned dependency (link to non-existent work item)
                    var sourceItem = workItems[_random.Next(workItems.Count)];
                    invalidDependencies.Add(new DependencyLink
                    {
                        Id = linkId++,
                        SourceWorkItemId = sourceItem.TfsId,
                        TargetWorkItemId = 999999, // Non-existent ID
                        LinkType = "Predecessor",
                        SourceAreaPath = sourceItem.AreaPath,
                        TargetAreaPath = "\\Battleship Systems\\Unknown",
                        IsCrossTeam = true
                    });
                    break;

                case 2: // Self-dependency
                    var selfItem = workItems[_random.Next(workItems.Count)];
                    invalidDependencies.Add(new DependencyLink
                    {
                        Id = linkId++,
                        SourceWorkItemId = selfItem.TfsId,
                        TargetWorkItemId = selfItem.TfsId,
                        LinkType = "Predecessor",
                        SourceAreaPath = selfItem.AreaPath,
                        TargetAreaPath = selfItem.AreaPath,
                        IsCrossTeam = false
                    });
                    break;

                case 3: // Temporal violation (current sprint depends on future sprint)
                    var currentSprintItems = workItems
                        .Where(w => w.IterationPath.Contains("Sprint 2") || w.IterationPath.Contains("Sprint 3"))
                        .ToList();
                    var futureSprintItems = workItems
                        .Where(w => w.IterationPath.Contains("Sprint 8") || w.IterationPath.Contains("Sprint 9"))
                        .ToList();

                    if (currentSprintItems.Any() && futureSprintItems.Any())
                    {
                        var current = currentSprintItems[_random.Next(currentSprintItems.Count)];
                        var future = futureSprintItems[_random.Next(futureSprintItems.Count)];

                        invalidDependencies.Add(new DependencyLink
                        {
                            Id = linkId++,
                            SourceWorkItemId = current.TfsId,
                            TargetWorkItemId = future.TfsId,
                            LinkType = "Predecessor",
                            SourceAreaPath = current.AreaPath,
                            TargetAreaPath = future.AreaPath,
                            IsCrossTeam = IsCrossTeam(current.AreaPath, future.AreaPath)
                        });
                    }
                    break;
            }
        }

        return invalidDependencies;
    }
}

/// <summary>
/// Represents a dependency link between work items
/// </summary>
public class DependencyLink
{
    public int Id { get; set; }
    public int SourceWorkItemId { get; set; }
    public int TargetWorkItemId { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public string SourceAreaPath { get; set; } = string.Empty;
    public string TargetAreaPath { get; set; } = string.Empty;
    public bool IsCrossTeam { get; set; }
}
