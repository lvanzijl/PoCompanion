using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Computes a summary of work-item and sprint changes that occurred between the
/// previous and the latest successful sync. Powers the "What's New Since Last Sync"
/// section on the Home page.
/// </summary>
public sealed class SyncChangesSummaryService
{
    // Bug states that are considered "done/closed"
    private static readonly HashSet<string> ClosedStates = new(
        ["Closed", "Done", "Resolved", "Completed", "Removed"],
        StringComparer.OrdinalIgnoreCase);

    private const int MaxItemsPerCategory = 20;

    private readonly PoToolDbContext _context;
    private readonly IWorkItemValidator _validator;
    private readonly ISprintRepository _sprintRepository;

    public SyncChangesSummaryService(
        PoToolDbContext context,
        IWorkItemValidator validator,
        ISprintRepository sprintRepository)
    {
        _context = context;
        _validator = validator;
        _sprintRepository = sprintRepository;
    }

    /// <summary>
    /// Returns the change summary for the given product owner.
    /// When no previous sync exists, returns an empty summary with <see cref="SyncChangesSummaryDto.HasData"/> = false.
    /// </summary>
    public async Task<SyncChangesSummaryDto> GetChangesSummaryAsync(
        int productOwnerId,
        CancellationToken cancellationToken = default)
    {
        var cacheState = await _context.ProductOwnerCacheStates
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync(e => e.ProductOwnerId == productOwnerId, cancellationToken);

        var sinceTime = cacheState?.PreviousSuccessfulSync;
        var lastSyncTime = cacheState?.LastSuccessfulSync;

        if (sinceTime == null)
        {
            // First sync ever, or cache was reset — no change window available
            return new SyncChangesSummaryDto { LastSyncTime = lastSyncTime };
        }

        var sinceUtc = sinceTime.Value.UtcDateTime;
        var untilUtc = (lastSyncTime ?? DateTimeOffset.UtcNow).UtcDateTime;

        var (bugsOpened, bugsClosed) = await GetBugStateChangesAsync(
            productOwnerId, sinceUtc, cancellationToken);

        var (validationCount, validationItems) = await GetValidationIssueItemsChangedSinceAsync(
            sinceUtc, cancellationToken);

        var completedSprints = await GetCompletedSprintsAsync(sinceUtc, untilUtc, cancellationToken);

        return new SyncChangesSummaryDto
        {
            SinceTime = sinceTime,
            LastSyncTime = lastSyncTime,
            BugsOpenedCount = bugsOpened.Count,
            BugsOpened = bugsOpened,
            BugsClosedCount = bugsClosed.Count,
            BugsClosed = bugsClosed,
            ValidationIssueWorkItemCount = validationCount,
            ValidationIssueItems = validationItems,
            SprintsCompletedCount = completedSprints.Count,
            SprintsCompleted = completedSprints
        };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds Bug work items whose <c>System.State</c> changed in the
    /// ActivityEventLedger since <paramref name="sinceUtc"/>, then classifies
    /// each as "opened" (last new-state is not a done-state) or "closed" (it is).
    /// </summary>
    private async Task<(List<WorkItemChangeSummary> Opened, List<WorkItemChangeSummary> Closed)>
        GetBugStateChangesAsync(
            int productOwnerId,
            DateTime sinceUtc,
            CancellationToken cancellationToken)
    {
        // Get TFS IDs for all cached bugs
        var bugIds = await _context.WorkItems
            .AsNoTracking()
            .Where(w => w.Type == "Bug")
            .Select(w => w.TfsId)
            .ToListAsync(cancellationToken);

        if (bugIds.Count == 0)
        {
            return ([], []);
        }

        // State-change events for bugs since the previous sync
        var stateChanges = await _context.ActivityEventLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.ProductOwnerId == productOwnerId
                && bugIds.Contains(e.WorkItemId)
                && e.EventTimestampUtc >= sinceUtc
                && e.FieldRefName == "System.State")
            .Select(e => new
            {
                e.WorkItemId,
                e.OldValue,
                e.NewValue,
                e.EventTimestamp
            })
            .ToListAsync(cancellationToken);

        if (stateChanges.Count == 0)
        {
            return ([], []);
        }

        // Keep only the latest state change per work item
        var latestByWorkItem = stateChanges
            .GroupBy(e => e.WorkItemId)
            .Select(g => g.OrderByDescending(e => e.EventTimestamp).First())
            .ToList();

        // Fetch titles for the affected work items
        var affectedIds = latestByWorkItem.Select(e => e.WorkItemId).ToList();
        var titleLookup = await _context.WorkItems
            .AsNoTracking()
            .Where(w => affectedIds.Contains(w.TfsId))
            .Select(w => new { w.TfsId, w.Title })
            .ToDictionaryAsync(w => w.TfsId, w => w.Title, cancellationToken);

        var opened = new List<WorkItemChangeSummary>();
        var closed = new List<WorkItemChangeSummary>();

        foreach (var change in latestByWorkItem)
        {
            var summary = new WorkItemChangeSummary
            {
                TfsId = change.WorkItemId,
                Title = titleLookup.GetValueOrDefault(change.WorkItemId, $"Work Item {change.WorkItemId}"),
                Type = "Bug",
                OldState = change.OldValue,
                NewState = change.NewValue,
                ChangedAt = change.EventTimestamp
            };

            if (ClosedStates.Contains(change.NewValue ?? string.Empty))
            {
                closed.Add(summary);
            }
            else
            {
                opened.Add(summary);
            }
        }

        return (opened.Take(MaxItemsPerCategory).ToList(),
                closed.Take(MaxItemsPerCategory).ToList());
    }

    /// <summary>
    /// Returns work items whose TFS ChangedDate >= <paramref name="sinceUtc"/>
    /// and that currently have at least one validation issue.
    /// </summary>
    private async Task<(int TotalCount, List<WorkItemChangeSummary> Items)>
        GetValidationIssueItemsChangedSinceAsync(
            DateTime sinceUtc,
            CancellationToken cancellationToken)
    {
        var changedEntities = await _context.WorkItems
            .AsNoTracking()
            .Where(w => w.TfsChangedDateUtc >= sinceUtc)
            .ToListAsync(cancellationToken);

        if (changedEntities.Count == 0)
        {
            return (0, []);
        }

        var dtos = changedEntities
            .Select(MapEntityToWorkItemDto)
            .ToList();

        var validationResults = _validator.ValidateWorkItems(dtos);

        var withIssues = dtos
            .Where(w => validationResults.TryGetValue(w.TfsId, out var issues) && issues.Count > 0)
            .ToList();

        var totalCount = withIssues.Count;
        var items = withIssues
            .Take(MaxItemsPerCategory)
            .Select(w => new WorkItemChangeSummary
            {
                TfsId = w.TfsId,
                Title = w.Title,
                Type = w.Type,
                NewState = w.State,
                ChangedAt = w.ChangedDate
            })
            .ToList();

        return (totalCount, items);
    }

    /// <summary>
    /// Returns sprints whose end date falls within [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>].
    /// </summary>
    private async Task<List<SprintChangeSummary>> GetCompletedSprintsAsync(
        DateTime sinceUtc,
        DateTime untilUtc,
        CancellationToken cancellationToken)
    {
        var allSprints = await _sprintRepository.GetAllSprintsAsync(cancellationToken);

        return allSprints
            .Where(s =>
                s.EndUtc.HasValue
                && s.EndUtc.Value.UtcDateTime >= sinceUtc
                && s.EndUtc.Value.UtcDateTime <= untilUtc)
            .Select(s => new SprintChangeSummary
            {
                SprintName = s.Name,
                IterationPath = s.Path,
                EndDate = s.EndUtc
            })
            .OrderByDescending(s => s.EndDate)
            .ToList();
    }

    /// <summary>
    /// Maps a <see cref="Persistence.Entities.WorkItemEntity"/> to a
    /// <see cref="WorkItemDto"/> for use in validation. Only fields required
    /// by the validator are populated.
    /// </summary>
    private static WorkItemDto MapEntityToWorkItemDto(Persistence.Entities.WorkItemEntity entity)
    {
        return new WorkItemDto(
            TfsId: entity.TfsId,
            Type: entity.Type,
            Title: entity.Title,
            ParentTfsId: entity.ParentTfsId,
            AreaPath: entity.AreaPath,
            IterationPath: entity.IterationPath,
            State: entity.State,
            RetrievedAt: entity.RetrievedAt,
            Effort: entity.Effort,
            Description: entity.Description,
            CreatedDate: entity.CreatedDate,
            ClosedDate: entity.ClosedDate,
            Severity: entity.Severity,
            Tags: entity.Tags,
            IsBlocked: entity.IsBlocked,
            ChangedDate: entity.TfsChangedDate,
            BusinessValue: entity.BusinessValue,
            BacklogPriority: entity.BacklogPriority,
            StoryPoints: entity.StoryPoints,
            TimeCriticality: entity.TimeCriticality,
            ProjectNumber: entity.ProjectNumber,
            ProjectElement: entity.ProjectElement
        );
    }
}
