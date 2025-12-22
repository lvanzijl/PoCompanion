using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintCapacityPlanQuery.
/// Calculates sprint capacity utilization and identifies overcommitments.
/// </summary>
public sealed class GetSprintCapacityPlanQueryHandler 
    : IQueryHandler<GetSprintCapacityPlanQuery, SprintCapacityPlanDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetSprintCapacityPlanQueryHandler> _logger;

    public GetSprintCapacityPlanQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetSprintCapacityPlanQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<SprintCapacityPlanDto?> Handle(
        GetSprintCapacityPlanQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetSprintCapacityPlanQuery for iteration: {IterationPath}", query.IterationPath);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        var iterationWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!iterationWorkItems.Any())
        {
            _logger.LogDebug("No work items found for iteration: {IterationPath}", query.IterationPath);
            return null;
        }

        var sprintName = ExtractSprintName(query.IterationPath);
        var totalPlannedEffort = iterationWorkItems.Sum(wi => wi.Effort ?? 0);

        // Calculate team member capacities (simplified - using assigned to field)
        var teamCapacities = CalculateTeamCapacities(iterationWorkItems, query.DefaultCapacityPerPerson);
        
        var totalCapacity = teamCapacities.Sum(tc => tc.AvailableCapacity);
        var utilizationPercentage = totalCapacity > 0 
            ? (double)totalPlannedEffort / totalCapacity * 100 
            : 0;

        var status = DetermineCapacityStatus(totalPlannedEffort, totalCapacity);
        var warnings = GenerateWarnings(totalPlannedEffort, totalCapacity, teamCapacities);

        return new SprintCapacityPlanDto(
            IterationPath: query.IterationPath,
            SprintName: sprintName,
            StartDate: null, // Could be extracted from JSON payload
            EndDate: null,
            TotalPlannedEffort: totalPlannedEffort,
            TotalCapacity: totalCapacity,
            UtilizationPercentage: utilizationPercentage,
            Status: status,
            TeamCapacities: teamCapacities,
            Warnings: warnings,
            AnalysisTimestamp: DateTimeOffset.UtcNow
        );
    }

    private static List<TeamMemberCapacity> CalculateTeamCapacities(
        List<WorkItemDto> workItems,
        int? defaultCapacity)
    {
        // Group by creator as proxy for assignee (simplified)
        var byMember = workItems
            .GroupBy(wi => "Team Member") // Simplified - would need AssignedTo field
            .Select(group =>
            {
                var assignedEffort = group.Sum(wi => wi.Effort ?? 0);
                var capacity = defaultCapacity ?? 40; // Default 40 hours per sprint
                var utilization = capacity > 0 ? (double)assignedEffort / capacity * 100 : 0;
                var status = DetermineCapacityStatus(assignedEffort, capacity);

                return new TeamMemberCapacity(
                    MemberName: group.Key,
                    AssignedEffort: assignedEffort,
                    AvailableCapacity: capacity,
                    UtilizationPercentage: utilization,
                    Status: status
                );
            })
            .ToList();

        return byMember;
    }

    private static CapacityStatus DetermineCapacityStatus(int effort, int capacity)
    {
        if (capacity == 0)
        {
            return CapacityStatus.Unknown;
        }

        var utilizationPercentage = (double)effort / capacity * 100;

        return utilizationPercentage switch
        {
            < 50 => CapacityStatus.Underutilized,
            >= 50 and < 85 => CapacityStatus.Normal,
            >= 85 and < 100 => CapacityStatus.NearCapacity,
            _ => CapacityStatus.OverCapacity
        };
    }

    private static List<CapacityWarning> GenerateWarnings(
        int totalEffort,
        int totalCapacity,
        List<TeamMemberCapacity> teamCapacities)
    {
        var warnings = new List<CapacityWarning>();

        // Check for overall overcommitment
        if (totalEffort > totalCapacity * 1.1)
        {
            warnings.Add(new CapacityWarning(
                Level: WarningLevel.Critical,
                Message: $"Sprint is overcommitted by {totalEffort - totalCapacity} points ({(double)(totalEffort - totalCapacity) / totalCapacity * 100:F1}%)",
                AffectedMembers: new List<string> { "Team" }
            ));
        }
        else if (totalEffort > totalCapacity)
        {
            warnings.Add(new CapacityWarning(
                Level: WarningLevel.Warning,
                Message: $"Sprint is at or over capacity",
                AffectedMembers: new List<string> { "Team" }
            ));
        }

        // Check for individual overcommitments
        var overcommittedMembers = teamCapacities
            .Where(tc => tc.Status == CapacityStatus.OverCapacity)
            .ToList();

        if (overcommittedMembers.Any())
        {
            warnings.Add(new CapacityWarning(
                Level: WarningLevel.Warning,
                Message: $"{overcommittedMembers.Count} team member(s) overcommitted",
                AffectedMembers: overcommittedMembers.Select(m => m.MemberName).ToList()
            ));
        }

        // Check for underutilization
        if (totalEffort < totalCapacity * 0.5)
        {
            warnings.Add(new CapacityWarning(
                Level: WarningLevel.Info,
                Message: "Sprint may be underutilized - consider adding more work",
                AffectedMembers: new List<string> { "Team" }
            ));
        }

        return warnings;
    }

    private static string ExtractSprintName(string iterationPath)
    {
        var parts = iterationPath.Split('\\', '/');
        return parts.Length > 0 ? parts[^1] : iterationPath;
    }
}
