using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
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
    private readonly ILogger<BugTriageStateService> _logger;

    public BugTriageStateService(
        PoToolDbContext db,
        ITfsClient tfsClient,
        ILogger<BugTriageStateService> logger)
    {
        _db = db;
        _tfsClient = tfsClient;
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
                
                // Update TFS with Severity field and get the updated work item from the response
                var refreshedWorkItem = await _tfsClient.UpdateWorkItemSeverityAndReturnAsync(
                    request.BugId, 
                    tfsSeverity, 
                    cancellationToken);
                
                if (refreshedWorkItem == null)
                {
                    _logger.LogError("Failed to update TFS bug {BugId} severity", request.BugId);
                    return new UpdateBugTriageStateResponse(
                        false, 
                        $"Failed to update bug {request.BugId} in TFS. Please try again.");
                }
                
                _logger.LogInformation("Successfully updated TFS bug {BugId} severity to '{Severity}'", 
                    request.BugId, tfsSeverity);
                
                // Update the work item in cache with the response from TFS
                await UpdateCachedWorkItemAsync(refreshedWorkItem, request.BugId, cancellationToken);
            }
            
            // Handle tags - now persist to TFS
            if ((request.TagsAdded != null && request.TagsAdded.Count > 0) ||
                (request.TagsRemoved != null && request.TagsRemoved.Count > 0))
            {
                // Get current tags from the work item
                var currentWorkItem = await _db.WorkItems
                    .FirstOrDefaultAsync(wi => wi.TfsId == request.BugId, cancellationToken);
                
                if (currentWorkItem != null)
                {
                    // Extract current tags from the denormalized Tags field
                    // Tags are stored as semicolon-separated string (e.g., "Tag1; Tag2; Tag3")
                    var currentTags = ExtractTagsFromString(currentWorkItem.Tags);
                    
                    // Use case-insensitive comparison to match TFS behavior where tags are case-insensitive
                    // This ensures that 'Bug' and 'bug' are treated as the same tag, preventing duplicates
                    var tagSet = new HashSet<string>(currentTags, StringComparer.OrdinalIgnoreCase);
                    
                    // Apply tag additions
                    if (request.TagsAdded != null)
                    {
                        foreach (var tag in request.TagsAdded)
                        {
                            tagSet.Add(tag);
                            _logger.LogInformation("Adding tag '{Tag}' to bug {BugId}", tag, request.BugId);
                        }
                    }
                    
                    // Apply tag removals
                    if (request.TagsRemoved != null)
                    {
                        foreach (var tag in request.TagsRemoved)
                        {
                            tagSet.Remove(tag);
                            _logger.LogInformation("Removing tag '{Tag}' from bug {BugId}", tag, request.BugId);
                        }
                    }
                    
                    // Update TFS with new tag list and get the updated work item from the response
                    var newTagsList = tagSet.ToList();
                    _logger.LogInformation("Updating TFS bug {BugId} with tags: {Tags}", 
                        request.BugId, string.Join("; ", newTagsList));
                    
                    var refreshedWorkItem = await _tfsClient.UpdateWorkItemTagsAndReturnAsync(
                        request.BugId,
                        newTagsList,
                        cancellationToken);
                    
                    if (refreshedWorkItem == null)
                    {
                        _logger.LogError("Failed to update TFS bug {BugId} tags", request.BugId);
                        return new UpdateBugTriageStateResponse(
                            false,
                            $"Failed to update tags for bug {request.BugId} in TFS. Please try again.");
                    }
                    
                    _logger.LogInformation("Successfully updated TFS bug {BugId} tags", request.BugId);
                    
                    // Update the work item in cache with the response from TFS
                    await UpdateCachedWorkItemAsync(refreshedWorkItem, request.BugId, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Bug {BugId} not found in cache for tag update", request.BugId);
                    return new UpdateBugTriageStateResponse(
                        false,
                        $"Bug {request.BugId} not found in cache. Please try again.");
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
    /// Maps UI severity values to TFS Severity field format.
    /// Handles both old format ("Critical") and new TFS format ("1 - Critical").
    /// TFS expects values like "1 - Critical", "2 - High", "3 - Medium", "4 - Low".
    /// </summary>
    private string MapSeverityToTfsFormat(string severity)
    {
        // If already in TFS format (contains " - "), return as-is
        if (!string.IsNullOrWhiteSpace(severity) && severity.Contains(" - "))
        {
            // Validate it's a known TFS format
            var validTfsFormats = new[] { "1 - Critical", "2 - High", "3 - Medium", "4 - Low" };
            if (validTfsFormats.Contains(severity, StringComparer.OrdinalIgnoreCase))
            {
                return severity;
            }
        }

        // Map old UI severity constants to TFS format (for backwards compatibility)
        var mapped = severity switch
        {
            "Critical" => "1 - Critical",
            "High" => "2 - High",
            "Medium" => "3 - Medium",
            "Low" => "4 - Low",
            // Also handle TFS format without exact match (case-insensitive)
            var s when s.Equals("1 - Critical", StringComparison.OrdinalIgnoreCase) => "1 - Critical",
            var s when s.Equals("2 - High", StringComparison.OrdinalIgnoreCase) => "2 - High",
            var s when s.Equals("3 - Medium", StringComparison.OrdinalIgnoreCase) => "3 - Medium",
            var s when s.Equals("4 - Low", StringComparison.OrdinalIgnoreCase) => "4 - Low",
            _ => null
        };

        if (mapped == null)
        {
            _logger.LogWarning("Unknown severity value '{Severity}' encountered. Expected values: '1 - Critical', '2 - High', '3 - Medium', '4 - Low' or 'Critical', 'High', 'Medium', 'Low'. Defaulting to '3 - Medium'", severity);
            return "3 - Medium";
        }

        return mapped;
    }

    /// <summary>
    /// Extracts tags from a semicolon-separated string.
    /// Tags in TFS are stored as a semicolon-separated string (e.g., "Tag1; Tag2; Tag3").
    /// Returns an empty list if the input is null or empty.
    /// </summary>
    private List<string> ExtractTagsFromString(string? tagsString)
    {
        if (string.IsNullOrWhiteSpace(tagsString))
        {
            return new List<string>();
        }

        // TFS tags are semicolon-separated
        return tagsString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
    }

    /// <summary>
    /// Updates the cached work item entity with fresh data from TFS.
    /// This helper method reduces duplication in severity and tag update flows.
    /// </summary>
    private async Task UpdateCachedWorkItemAsync(WorkItemDto refreshedWorkItem, int bugId, CancellationToken cancellationToken)
    {
        var cachedEntity = await _db.WorkItems
            .FirstOrDefaultAsync(wi => wi.TfsId == bugId, cancellationToken);
        
        if (cachedEntity != null)
        {
            // Update cached work item with fresh data from TFS PATCH response
            cachedEntity.JsonPayload = refreshedWorkItem.JsonPayload;
            cachedEntity.State = refreshedWorkItem.State;
            cachedEntity.Title = refreshedWorkItem.Title;
            cachedEntity.AreaPath = refreshedWorkItem.AreaPath;
            cachedEntity.IterationPath = refreshedWorkItem.IterationPath;
            cachedEntity.Effort = refreshedWorkItem.Effort;
            cachedEntity.Description = refreshedWorkItem.Description;
            cachedEntity.RetrievedAt = refreshedWorkItem.RetrievedAt;
            cachedEntity.Severity = refreshedWorkItem.Severity;
            cachedEntity.Tags = refreshedWorkItem.Tags;
            
            // Explicitly mark entity as modified to ensure EF Core tracks the change
            _db.Entry(cachedEntity).State = EntityState.Modified;
            
            _logger.LogInformation("Updated cache for bug {BugId} with data from TFS PATCH response. Severity: {Severity}, Tags: {Tags}", 
                bugId, cachedEntity.Severity ?? "null", cachedEntity.Tags ?? "null");
        }
        else
        {
            _logger.LogWarning("Bug {BugId} not found in cache, cannot refresh", bugId);
        }
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
