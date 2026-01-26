using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that fetches and upserts team sprints (iterations) from TFS.
/// </summary>
public class TeamSprintSyncStage : ISyncStage
{
    private readonly ITfsClient _tfsClient;
    private readonly PoToolDbContext _context;
    private readonly ISprintRepository _sprintRepository;
    private readonly ILogger<TeamSprintSyncStage> _logger;

    public string StageName => "SyncTeamSprints";
    public int StageNumber => 2;

    public TeamSprintSyncStage(
        ITfsClient tfsClient,
        PoToolDbContext context,
        ISprintRepository sprintRepository,
        ILogger<TeamSprintSyncStage> logger)
    {
        _tfsClient = tfsClient;
        _context = context;
        _sprintRepository = sprintRepository;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progressCallback(0);

            // Get all products for this ProductOwner
            var products = await _context.Products
                .Where(p => p.ProductOwnerId == context.ProductOwnerId)
                .Include(p => p.ProductTeamLinks)
                .ThenInclude(ptl => ptl.Team)
                .ToListAsync(cancellationToken);

            if (!products.Any())
            {
                _logger.LogInformation(
                    "No products found for ProductOwner {ProductOwnerId}",
                    context.ProductOwnerId);
                return SyncStageResult.CreateSuccess(0);
            }

            // Get all unique teams linked to these products
            // Query teams directly to ensure proper change tracking
            var teamIds = products
                .SelectMany(p => p.ProductTeamLinks)
                .Select(ptl => ptl.TeamId)
                .Distinct()
                .ToList();

            // Load teams with tracking enabled (default behavior)
            var teams = await _context.Teams
                .Where(t => teamIds.Contains(t.Id) 
                    && t.ProjectName != null 
                    && t.TfsTeamName != null)
                .ToListAsync(cancellationToken);

            if (!teams.Any())
            {
                _logger.LogInformation(
                    "No teams with TFS configuration found for ProductOwner {ProductOwnerId}",
                    context.ProductOwnerId);
                return SyncStageResult.CreateSuccess(0);
            }

            _logger.LogInformation(
                "Starting sprint sync for ProductOwner {ProductOwnerId} with {TeamCount} teams",
                context.ProductOwnerId,
                teams.Count);

            int totalSprintsSynced = 0;
            int teamsSynced = 0;
            var syncTime = DateTimeOffset.UtcNow;

            // Track which teams need to be updated
            var teamsToUpdate = new List<TeamEntity>();

            for (int i = 0; i < teams.Count; i++)
            {
                var team = teams[i];
                
                try
                {
                    _logger.LogDebug(
                        "Syncing sprints for Team {TeamId} ({TeamName}) - Project: {ProjectName}, TfsTeam: {TfsTeamName}",
                        team.Id,
                        team.Name,
                        team.ProjectName,
                        team.TfsTeamName);

                    // Fetch team iterations from TFS
                    var iterations = await _tfsClient.GetTeamIterationsAsync(
                        team.ProjectName!,
                        team.TfsTeamName!,
                        cancellationToken);

                    var iterationsList = iterations.ToList();

                    // Upsert sprints for this team
                    await _sprintRepository.UpsertSprintsForTeamAsync(
                        team.Id,
                        iterationsList,
                        cancellationToken);

                    // Update the team's last synced timestamp (will be saved in batch)
                    team.LastSyncedIterationsUtc = syncTime;
                    teamsToUpdate.Add(team);

                    totalSprintsSynced += iterationsList.Count;
                    teamsSynced++;

                    _logger.LogDebug(
                        "Synced {SprintCount} sprints for Team {TeamId} ({TeamName})",
                        iterationsList.Count,
                        team.Id,
                        team.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to sync sprints for Team {TeamId} ({TeamName}): {Message}",
                        team.Id,
                        team.Name,
                        ex.Message);
                    // Continue with other teams even if one fails
                }

                // Update progress
                var percent = (int)((i + 1) / (double)teams.Count * 100);
                progressCallback(percent);
            }

            // Save all team updates in a single batch
            if (teamsToUpdate.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            progressCallback(100);

            _logger.LogInformation(
                "Successfully synced {SprintCount} sprints across {TeamCount} teams for ProductOwner {ProductOwnerId}",
                totalSprintsSynced,
                teamsSynced,
                context.ProductOwnerId);

            return SyncStageResult.CreateSuccess(totalSprintsSynced);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sprint sync cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sprint sync failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }
}
