using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Sprints;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for managing work item state classifications.
/// Provides default mappings when no custom configuration exists.
/// Implements in-memory caching to avoid repeated database queries.
/// </summary>
public class WorkItemStateClassificationService : IWorkItemStateClassificationService
{
    private readonly PoToolDbContext _dbContext;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<WorkItemStateClassificationService> _logger;
    private readonly object _cacheLock = new();
    private GetStateClassificationsResponse? _cachedResponse;
    private string? _cachedProjectName;

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
        var projectName = config?.Project ?? throw new InvalidOperationException("TFS configuration not found");

        // Check cache first
        lock (_cacheLock)
        {
            if (_cachedResponse != null && _cachedProjectName == projectName)
            {
                return _cachedResponse;
            }
        }

        _logger.LogInformation("Getting state classifications for project '{Project}'", projectName);

        // Try to get from database
        var entities = await _dbContext.WorkItemStateClassifications
            .Where(e => e.TfsProjectName == projectName)
            .ToListAsync(cancellationToken);

        GetStateClassificationsResponse response;
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

            response = new GetStateClassificationsResponse
            {
                ProjectName = projectName,
                Classifications = classifications,
                IsDefault = false
            };
        }
        else
        {
            // Return defaults
            _logger.LogInformation("No custom classifications found, returning defaults for project '{Project}'", projectName);
            var defaults = GetDefaultClassifications();

            response = new GetStateClassificationsResponse
            {
                ProjectName = projectName,
                Classifications = defaults,
                IsDefault = true
            };
        }

        // Cache the response
        lock (_cacheLock)
        {
            _cachedResponse = response;
            _cachedProjectName = projectName;
        }

        return response;
    }

    public async Task<bool> SaveClassificationsAsync(
        SaveStateClassificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetConfigEntityAsync(cancellationToken);
        var projectName = config?.Project ?? throw new InvalidOperationException("TFS configuration not found");

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

        // Invalidate cache
        lock (_cacheLock)
        {
            _cachedResponse = null;
            _cachedProjectName = null;
        }

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
    internal static List<WorkItemStateClassificationDto> GetDefaultClassifications()
    {
        return StateClassificationDefaults.Create().ToList();
    }
}
