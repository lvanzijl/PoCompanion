using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for sprint persistence.
/// </summary>
public class SprintRepository : ISprintRepository
{
    private readonly PoToolDbContext _context;

    public SprintRepository(PoToolDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SprintDto>> GetAllSprintsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Sprints
            .OrderBy(s => s.StartDateUtc == null)
            .ThenBy(s => s.StartDateUtc)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SprintDto>> GetSprintsForTeamAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Sprints
            .Where(s => s.TeamId == teamId)
            .OrderBy(s => s.StartDateUtc == null)
            .ThenBy(s => s.StartDateUtc)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<SprintDto?> GetCurrentSprintForTeamAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        // Prefer timeFrame="current"
        var currentByTimeFrame = await _context.Sprints
            .Where(s => s.TeamId == teamId && s.TimeFrame == "current")
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentByTimeFrame != null)
        {
            return MapToDto(currentByTimeFrame);
        }

        // Fallback: find sprint by date range (start <= now < end)
        var currentByDate = await _context.Sprints
                .Where(s => s.TeamId == teamId
                && s.StartDateUtc.HasValue
                && s.EndDateUtc.HasValue
                && s.StartDateUtc <= nowUtc
                && s.EndDateUtc > nowUtc)
                .OrderBy(s => s.StartDateUtc)
                .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return currentByDate == null ? null : MapToDto(currentByDate);
    }

    /// <inheritdoc />
    public async Task<SprintDto?> GetNextSprintForTeamAsync(int teamId, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        // Prefer earliest timeFrame="future" by start date
        var futureByTimeFrame = await _context.Sprints
            .Where(s => s.TeamId == teamId && s.TimeFrame == "future" && s.StartDateUtc.HasValue)
            .OrderBy(s => s.StartDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (futureByTimeFrame != null)
        {
            return MapToDto(futureByTimeFrame);
        }

        // Fallback: find earliest sprint with start date in the future
        var futureByDate = await _context.Sprints
            .Where(s => s.TeamId == teamId && s.StartDateUtc.HasValue && s.StartDateUtc > nowUtc)
            .OrderBy(s => s.StartDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return futureByDate == null ? null : MapToDto(futureByDate);
    }

    /// <inheritdoc />
    public async Task UpsertSprintsForTeamAsync(
        int teamId,
        IEnumerable<TeamIterationDto> iterations,
        CancellationToken cancellationToken = default)
    {
        var iterationsList = iterations.ToList();
        if (!iterationsList.Any())
        {
            return;
        }

        var syncTime = DateTimeOffset.UtcNow;
        var syncTimeUtc = syncTime.UtcDateTime;

        // Get all existing sprints for this team
        var existingSprints = await _context.Sprints
            .Where(s => s.TeamId == teamId)
            .ToListAsync(cancellationToken);

        // Build a map of existing sprints by path for efficient lookup
        var existingByPath = existingSprints.ToDictionary(s => s.Path, s => s);

        foreach (var iteration in iterationsList)
        {
            if (existingByPath.TryGetValue(iteration.Path, out var existing))
            {
                // Update existing sprint
                existing.TfsIterationId = iteration.Id;
                existing.Name = iteration.Name;
                existing.StartUtc = iteration.StartDate;
                existing.EndUtc = iteration.FinishDate;
                existing.StartDateUtc = iteration.StartDate?.UtcDateTime;
                existing.EndDateUtc = iteration.FinishDate?.UtcDateTime;
                existing.TimeFrame = iteration.TimeFrame;
                existing.LastSyncedUtc = syncTime;
                existing.LastSyncedDateUtc = syncTimeUtc;
            }
            else
            {
                // Insert new sprint
                var newSprint = new SprintEntity
                {
                    TeamId = teamId,
                    TfsIterationId = iteration.Id,
                    Path = iteration.Path,
                    Name = iteration.Name,
                    StartUtc = iteration.StartDate,
                    EndUtc = iteration.FinishDate,
                    StartDateUtc = iteration.StartDate?.UtcDateTime,
                    EndDateUtc = iteration.FinishDate?.UtcDateTime,
                    TimeFrame = iteration.TimeFrame,
                    LastSyncedUtc = syncTime,
                    LastSyncedDateUtc = syncTimeUtc
                };
                _context.Sprints.Add(newSprint);
            }
        }

        // Remove sprints that were not in the sync response (stale/deleted in TFS)
        var syncedPaths = new HashSet<string>(iterationsList.Select(i => i.Path));
        var staleSprints = existingSprints.Where(s => !syncedPaths.Contains(s.Path)).ToList();

        if (staleSprints.Any())
        {
            _context.Sprints.RemoveRange(staleSprints);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveStaleSprintsAsync(
        int teamId,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default)
    {
        var staleSprints = await _context.Sprints
            .Where(s => s.TeamId == teamId && s.LastSyncedDateUtc < olderThan.UtcDateTime)
            .ToListAsync(cancellationToken);

        if (staleSprints.Any())
        {
            _context.Sprints.RemoveRange(staleSprints);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static SprintDto MapToDto(SprintEntity entity)
    {
        return new SprintDto(
            entity.Id,
            entity.TeamId,
            entity.TfsIterationId,
            entity.Path,
            entity.Name,
            entity.StartUtc,
            entity.EndUtc,
            entity.TimeFrame,
            entity.LastSyncedUtc
        );
    }
}
