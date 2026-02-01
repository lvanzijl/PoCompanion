using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.BugTriage;

namespace PoTool.Api.Services;

/// <summary>
/// Service for managing bug triage state locally.
/// Tracks whether bugs have been triaged based on user actions in the Bugs Triage UI.
/// </summary>
public class BugTriageStateService
{
    private readonly PoToolDbContext _db;
    private readonly ILogger<BugTriageStateService> _logger;

    public BugTriageStateService(
        PoToolDbContext db,
        ILogger<BugTriageStateService> logger)
    {
        _db = db;
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
        string currentCriticality,
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
            FirstObservedCriticality = currentCriticality,
            IsTriaged = false,
            LastTriageActionAt = null
        };

        _db.BugTriageStates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded first seen for bug {BugId} with criticality {Criticality}", bugId, currentCriticality);
    }

    /// <summary>
    /// Marks a bug as triaged due to a user action (criticality change or tag toggle).
    /// Logs an Info message describing what would be saved to TFS.
    /// </summary>
    public async Task<UpdateBugTriageStateResponse> MarkAsTriagedAsync(
        UpdateBugTriageStateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.BugTriageStates
            .FirstOrDefaultAsync(s => s.BugId == request.BugId, cancellationToken);

        if (entity == null)
        {
            // Bug not yet seen - create initial record
            entity = new BugTriageStateEntity
            {
                BugId = request.BugId,
                FirstSeenAt = DateTimeOffset.UtcNow,
                FirstObservedCriticality = request.NewCriticality ?? "Unknown",
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

        // Log what would be saved to TFS
        LogTriageAction(request);

        return new UpdateBugTriageStateResponse(true, "Bug marked as triaged (local state only)");
    }

    private void LogTriageAction(UpdateBugTriageStateRequest request)
    {
        if (request.NewCriticality != null)
        {
            _logger.LogInformation(
                "Would update TFS bug {BugId}: Criticality changed to {NewCriticality}",
                request.BugId,
                request.NewCriticality);
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

    private static BugTriageStateDto MapToDto(BugTriageStateEntity entity)
    {
        return new BugTriageStateDto(
            entity.BugId,
            entity.FirstSeenAt,
            entity.FirstObservedCriticality,
            entity.IsTriaged,
            entity.LastTriageActionAt
        );
    }
}
