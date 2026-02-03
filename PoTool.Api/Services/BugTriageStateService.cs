using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Client.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.BugTriage;
using PoTool.Shared.WorkItems;
using System.Text.Json;

namespace PoTool.Api.Services;

/// <summary>
/// Service for managing bug triage state and updating TFS.
/// Tracks whether bugs have been triaged based on user actions in the Bugs Triage UI.
/// </summary>
public class BugTriageStateService
{
    private readonly PoToolDbContext _db;
    private readonly ITfsClient _tfsClient;
    private readonly TfsFieldParserService _fieldParser;
    private readonly ILogger<BugTriageStateService> _logger;

    public BugTriageStateService(
        PoToolDbContext db,
        ITfsClient tfsClient,
        TfsFieldParserService fieldParser,
        ILogger<BugTriageStateService> logger)
    {
        _db = db;
        _tfsClient = tfsClient;
        _fieldParser = fieldParser;
        _logger = logger;
    }

    /// <summary>
    /// Gets triage state for a specific bug.
    /// Returns null if the bug has not been seen in triage UI yet.
    /// </summary>
    public async Task<BugTriageStateDto?> GetTriageStateAsync(int bugId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.BugTriageStates
            .FirstOrDefaultAsync(s => s.BugId == bugId, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>
    /// Gets triage states for multiple bugs.
    /// </summary>
    public async Task<List<BugTriageStateDto>> GetTriageStatesAsync(
        List<int> bugIds,
        CancellationToken cancellationToken = default)
    {
        var entities = await _db.BugTriageStates
            .Where(s => bugIds.Contains(s.BugId))
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets all untriaged bugs (IsTriaged = false or no record exists).
    /// </summary>
    public async Task<HashSet<int>> GetUntriagedBugIdsAsync(
        List<int> allBugIds,
        CancellationToken cancellationToken = default)
    {
        var triagedBugIds = await _db.BugTriageStates
            .Where(s => s.IsTriaged && allBugIds.Contains(s.BugId))
            .Select(s => s.BugId)
            .ToListAsync(cancellationToken);

        var triagedSet = new HashSet<int>(triagedBugIds);
        return new HashSet<int>(allBugIds.Where(id => !triagedSet.Contains(id)));
    }

    /// <summary>
    /// Records that a bug was first seen in the triage UI.
    /// If the bug already has a record, this is a no-op.
    /// </summary>
    public async Task RecordFirstSeenAsync(
        int bugId,
        string currentSeverity,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.BugTriageStates
            .FirstOrDefaultAsync(s => s.BugId == bugId, cancellationToken);

        if (existing != null)
        {
            // Already recorded
            return;
        }

        var entity = new BugTriageStateEntity
        {
            BugId = bugId,
            FirstSeenAt = DateTimeOffset.UtcNow,
            FirstObservedSeverity = currentSeverity,
            IsTriaged = false,
            LastTriageActionAt = null
        };

        _db.BugTriageStates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded first seen for bug {BugId} with severity {Severity}", bugId, currentSeverity);
    }

    /// <summary>
    /// Marks a bug as triaged due to a user action (severity change or tag toggle).
    /// Updates TFS with the new severity value and refreshes the work item from TFS.
    /// </summary>
    public async Task<UpdateBugTriageStateResponse> MarkAsTriagedAsync(
        UpdateBugTriageStateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If severity changed, update TFS
            if (request.NewSeverity != null)
            {
                _logger.LogInformation("Updating TFS bug {BugId}: Severity to '{NewSeverity}'", 
                    request.BugId, request.NewSeverity);
                
                // Map severity display name to TFS Severity format (e.g., "Critical" -> "1 - Critical")
                var tfsSeverity = MapSeverityToTfsFormat(request.NewSeverity);
                
                // Update TFS with Severity field
                var updateSuccess = await _tfsClient.UpdateWorkItemSeverityAsync(
                    request.BugId, 
                    tfsSeverity, 
                    cancellationToken);
                
                if (!updateSuccess)
                {
                    _logger.LogError("Failed to update TFS bug {BugId} severity", request.BugId);
                    return new UpdateBugTriageStateResponse(
                        false, 
                        $"Failed to update bug {request.BugId} in TFS. Please try again.");
                }
                
                _logger.LogInformation("Successfully updated TFS bug {BugId} severity to '{Severity}'", 
                    request.BugId, tfsSeverity);
                
                // Refresh work item from TFS and update cache
                var refreshedWorkItem = await _tfsClient.GetWorkItemByIdAsync(request.BugId, cancellationToken);
                if (refreshedWorkItem != null)
                {
                    // Update the work item in cache
                    var cachedEntity = await _db.WorkItems
                        .FirstOrDefaultAsync(wi => wi.TfsId == request.BugId, cancellationToken);
                    
                    if (cachedEntity != null)
                    {
                        // Parse severity from the refreshed JsonPayload to log the actual value
                        string? newSeverityValue = null;
                        try
                        {
                            using var doc = JsonDocument.Parse(refreshedWorkItem.JsonPayload);
                            if (doc.RootElement.TryGetProperty("Microsoft.VSTS.Common.Severity", out var severity))
                            {
                                newSeverityValue = severity.GetString();
                            }
                        }
                        catch (Exception parseEx)
                        {
                            _logger.LogWarning(parseEx, "Failed to parse severity from refreshed work item {BugId}", request.BugId);
                        }
                        
                        // Update cached work item with fresh data from TFS
                        cachedEntity.JsonPayload = refreshedWorkItem.JsonPayload;
                        cachedEntity.State = refreshedWorkItem.State;
                        cachedEntity.Title = refreshedWorkItem.Title;
                        cachedEntity.AreaPath = refreshedWorkItem.AreaPath;
                        cachedEntity.IterationPath = refreshedWorkItem.IterationPath;
                        cachedEntity.Effort = refreshedWorkItem.Effort;
                        cachedEntity.Description = refreshedWorkItem.Description;
                        cachedEntity.RetrievedAt = refreshedWorkItem.RetrievedAt;
                        
                        // Explicitly mark entity as modified to ensure EF Core tracks the change
                        _db.Entry(cachedEntity).State = EntityState.Modified;
                        
                        _logger.LogInformation("Updated cache for bug {BugId} with refreshed data from TFS. New severity in JsonPayload: {Severity}", 
                            request.BugId, newSeverityValue ?? "null");
                    }
                    else
                    {
                        _logger.LogWarning("Bug {BugId} not found in cache, cannot refresh", request.BugId);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve refreshed work item {BugId} from TFS after update", 
                        request.BugId);
                }
            }
            
            // Handle tags (currently just logged - future enhancement for TFS tags update)
            if (request.TagsAdded != null && request.TagsAdded.Count > 0)
            {
                foreach (var tag in request.TagsAdded)
                {
                    _logger.LogInformation("Tag {Tag} added to bug {BugId} (not persisted to TFS yet)", 
                        tag, request.BugId);
                }
            }
            
            if (request.TagsRemoved != null && request.TagsRemoved.Count > 0)
            {
                foreach (var tag in request.TagsRemoved)
                {
                    _logger.LogInformation("Tag {Tag} removed from bug {BugId} (not persisted to TFS yet)", 
                        tag, request.BugId);
                }
            }
            
            // Update or create triage state entity
            var entity = await _db.BugTriageStates
                .FirstOrDefaultAsync(s => s.BugId == request.BugId, cancellationToken);
            
            if (entity == null)
            {
                // Bug not yet seen - create initial record
                entity = new BugTriageStateEntity
                {
                    BugId = request.BugId,
                    FirstSeenAt = DateTimeOffset.UtcNow,
                    FirstObservedSeverity = request.NewSeverity ?? "Unknown",
                    IsTriaged = true,
                    LastTriageActionAt = DateTimeOffset.UtcNow
                };
                _db.BugTriageStates.Add(entity);
            }
            else
            {
                // Update existing record
                entity.IsTriaged = true;
                entity.LastTriageActionAt = DateTimeOffset.UtcNow;
            }
            
            await _db.SaveChangesAsync(cancellationToken);
            
            return new UpdateBugTriageStateResponse(
                true, 
                $"Bug {request.BugId} updated successfully in TFS and marked as triaged");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bug {BugId} triage state", request.BugId);
            return new UpdateBugTriageStateResponse(
                false, 
                $"Error updating bug {request.BugId}: {ex.Message}");
        }
    }

    private void LogTriageAction(UpdateBugTriageStateRequest request)
    {
        if (request.NewSeverity != null)
        {
            _logger.LogInformation(
                "Would update TFS bug {BugId}: Severity changed to {NewSeverity}",
                request.BugId,
                request.NewSeverity);
        }

        if (request.TagsAdded != null && request.TagsAdded.Count > 0)
        {
            foreach (var tag in request.TagsAdded)
            {
                _logger.LogInformation(
                    "Would update TFS bug {BugId}: Tag {Tag} added",
                    request.BugId,
                    tag);
            }
        }

        if (request.TagsRemoved != null && request.TagsRemoved.Count > 0)
        {
            foreach (var tag in request.TagsRemoved)
            {
                _logger.LogInformation(
                    "Would update TFS bug {BugId}: Tag {Tag} removed",
                    request.BugId,
                    tag);
            }
        }
    }

    /// <summary>
    /// Maps UI severity display names to TFS Severity field format.
    /// TFS expects values like "1 - Critical", "2 - High", "3 - Medium", "4 - Low".
    /// </summary>
    private string MapSeverityToTfsFormat(string severity)
    {
        // Map UI severity constants to TFS format
        var mapped = severity switch
        {
            "Critical" => "1 - Critical",
            "High" => "2 - High",
            "Medium" => "3 - Medium",
            "Low" => "4 - Low",
            _ => null
        };

        if (mapped == null)
        {
            _logger.LogWarning("Unknown severity value '{Severity}' encountered. Expected values: Critical, High, Medium, or Low. Defaulting to '3 - Medium'", severity);
            return "3 - Medium";
        }

        return mapped;
    }

    private static BugTriageStateDto MapToDto(BugTriageStateEntity entity)
    {
        return new BugTriageStateDto(
            entity.BugId,
            entity.FirstSeenAt,
            entity.FirstObservedSeverity,
            entity.IsTriaged,
            entity.LastTriageActionAt
        );
    }
}
