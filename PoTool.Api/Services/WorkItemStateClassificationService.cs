using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for managing work item state classifications.
/// Provides default mappings when no custom configuration exists.
/// </summary>
public class WorkItemStateClassificationService : IWorkItemStateClassificationService
{
    private readonly PoToolDbContext _dbContext;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<WorkItemStateClassificationService> _logger;

    public WorkItemStateClassificationService(
        PoToolDbContext dbContext,
        TfsConfigurationService configService,
        ILogger<WorkItemStateClassificationService> logger)
    {
        _dbContext = dbContext;
        _configService = configService;
        _logger = logger;
    }

    public async Task<GetStateClassificationsResponse> GetClassificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetConfigEntityAsync(cancellationToken);
        var projectName = config?.Project ?? "DefaultProject";

        _logger.LogInformation("Getting state classifications for project '{Project}'", projectName);

        // Try to get from database
        var entities = await _dbContext.WorkItemStateClassifications
            .Where(e => e.TfsProjectName == projectName)
            .ToListAsync(cancellationToken);

        if (entities.Count > 0)
        {
            var classifications = entities.Select(e => new WorkItemStateClassificationDto
            {
                WorkItemType = e.WorkItemType,
                StateName = e.StateName,
                Classification = (StateClassification)e.Classification
            }).ToList();

            _logger.LogInformation(
                "Found {Count} custom state classifications for project '{Project}'",
                classifications.Count, projectName);

            return new GetStateClassificationsResponse
            {
                ProjectName = projectName,
                Classifications = classifications,
                IsDefault = false
            };
        }

        // Return defaults
        _logger.LogInformation("No custom classifications found, returning defaults for project '{Project}'", projectName);
        var defaults = GetDefaultClassifications();

        return new GetStateClassificationsResponse
        {
            ProjectName = projectName,
            Classifications = defaults,
            IsDefault = true
        };
    }

    public async Task<bool> SaveClassificationsAsync(
        SaveStateClassificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetConfigEntityAsync(cancellationToken);
        var projectName = config?.Project ?? "DefaultProject";

        _logger.LogInformation(
            "Saving {Count} state classifications for project '{Project}'",
            request.Classifications.Count, projectName);

        // Validate: ensure the project name matches
        if (request.ProjectName != projectName)
        {
            _logger.LogWarning(
                "Project name mismatch: request has '{RequestProject}' but config has '{ConfigProject}'",
                request.ProjectName, projectName);
            return false;
        }

        // Delete existing classifications for this project
        var existing = await _dbContext.WorkItemStateClassifications
            .Where(e => e.TfsProjectName == projectName)
            .ToListAsync(cancellationToken);

        _dbContext.WorkItemStateClassifications.RemoveRange(existing);

        // Add new classifications
        var now = DateTimeOffset.UtcNow;
        var newEntities = request.Classifications.Select(c => new WorkItemStateClassificationEntity
        {
            TfsProjectName = projectName,
            WorkItemType = c.WorkItemType,
            StateName = c.StateName,
            Classification = (int)c.Classification,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await _dbContext.WorkItemStateClassifications.AddRangeAsync(newEntities, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully saved {Count} state classifications for project '{Project}'",
            newEntities.Count, projectName);

        return true;
    }

    public async Task<bool> IsDoneStateAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var classification = await GetClassificationAsync(workItemType, state, cancellationToken);
        return classification == StateClassification.Done;
    }

    public async Task<bool> IsInProgressStateAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var classification = await GetClassificationAsync(workItemType, state, cancellationToken);
        return classification == StateClassification.InProgress;
    }

    public async Task<bool> IsNewStateAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var classification = await GetClassificationAsync(workItemType, state, cancellationToken);
        return classification == StateClassification.New;
    }

    public async Task<StateClassification> GetClassificationAsync(
        string workItemType,
        string state,
        CancellationToken cancellationToken = default)
    {
        var response = await GetClassificationsAsync(cancellationToken);

        var match = response.Classifications.FirstOrDefault(c =>
            c.WorkItemType.Equals(workItemType, StringComparison.OrdinalIgnoreCase) &&
            c.StateName.Equals(state, StringComparison.OrdinalIgnoreCase));

        return match?.Classification ?? StateClassification.New; // Default to New if not found
    }

    /// <summary>
    /// Returns default state classifications based on common TFS patterns.
    /// </summary>
    private static List<WorkItemStateClassificationDto> GetDefaultClassifications()
    {
        var defaults = new List<WorkItemStateClassificationDto>();

        // Goal
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Proposed", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Active", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Completed", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "goal", StateName = "Removed", Classification = StateClassification.Done });

        // Objective
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Proposed", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Active", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Completed", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Objective", StateName = "Removed", Classification = StateClassification.Done });

        // Epic
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "New", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Active", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Resolved", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Closed", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Removed", Classification = StateClassification.Done });

        // Feature
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "New", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Active", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Resolved", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Closed", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Feature", StateName = "Removed", Classification = StateClassification.Done });

        // Product Backlog Item
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "New", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Approved", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Committed", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Done", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Removed", Classification = StateClassification.Done });

        // Bug
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "New", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Approved", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Committed", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Done", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Bug", StateName = "Removed", Classification = StateClassification.Done });

        // Task
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "To Do", Classification = StateClassification.New });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "In Progress", Classification = StateClassification.InProgress });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "Done", Classification = StateClassification.Done });
        defaults.Add(new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "Removed", Classification = StateClassification.Done });

        return defaults;
    }
}
