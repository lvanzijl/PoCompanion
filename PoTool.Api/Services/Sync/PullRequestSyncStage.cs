using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services.Sync;

/// <summary>
/// Sync stage that fetches and upserts pull requests from TFS.
/// </summary>
public class PullRequestSyncStage : ISyncStage
{
    private const string PullRequestStatusAll = "all";
    private const string NoToDateFilter = "null";

    private readonly ITfsClient _tfsClient;
    private readonly PoToolDbContext _context;
    private readonly ILogger<PullRequestSyncStage> _logger;

    public string StageName => "SyncPullRequests";
    public int StageNumber => 7;

    public PullRequestSyncStage(
        ITfsClient tfsClient,
        PoToolDbContext context,
        ILogger<PullRequestSyncStage> logger)
    {
        _tfsClient = tfsClient;
        _context = context;
        _logger = logger;
    }

    public async Task<SyncStageResult> ExecuteAsync(
        SyncContext context,
        Action<int> progressCallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.RepositoryNames.Length == 0)
            {
                _logger.LogInformation(
                    "PR_INGEST_STAGE_SKIP: ProductOwner {ProductOwnerId} — reason: no repositories configured",
                    context.ProductOwnerId);
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0);
            }

            progressCallback(0);

            _logger.LogInformation(
                "PR_INGEST_STAGE_START: ProductOwner {ProductOwnerId}, repos={RepoCount} [{RepoNames}], " +
                "dateWindow from={FromDate} to={ToDate}, status={Status}",
                context.ProductOwnerId,
                context.RepositoryNames.Length,
                string.Join(", ", context.RepositoryNames),
                context.PullRequestWatermark?.ToString("O") ?? "null",
                NoToDateFilter,
                PullRequestStatusAll);

            var allPullRequests = new List<PullRequestDto>();
            var totalRepos = context.RepositoryNames.Length;
            var processedRepos = 0;

            // Fetch pull requests from each repository
            foreach (var repoName in context.RepositoryNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var prs = await _tfsClient.GetPullRequestsAsync(
                    repoName,
                    context.PullRequestWatermark,
                    null, // toDate
                    cancellationToken);

                allPullRequests.AddRange(prs);
                processedRepos++;

                // Map repo progress to 0-80%
                var percent = (int)((processedRepos / (double)totalRepos) * 80);
                progressCallback(percent);
            }

            _logger.LogInformation(
                "Fetched {Count} pull requests from TFS for ProductOwner {ProductOwnerId}",
                allPullRequests.Count,
                context.ProductOwnerId);

            progressCallback(80);

            if (allPullRequests.Count == 0)
            {
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0, context.PullRequestWatermark);
            }

            // Upsert pull requests to database
            var maxDate = await UpsertPullRequestsAsync(allPullRequests, progressCallback, cancellationToken);

            progressCallback(100);

            _logger.LogInformation(
                "Successfully synced {Count} pull requests for ProductOwner {ProductOwnerId}, new watermark: {Watermark}",
                allPullRequests.Count,
                context.ProductOwnerId,
                maxDate?.ToString("O") ?? "none");

            return SyncStageResult.CreateSuccess(allPullRequests.Count, maxDate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pull request sync cancelled for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pull request sync failed for ProductOwner {ProductOwnerId}", context.ProductOwnerId);
            return SyncStageResult.CreateFailure(ex.Message);
        }
    }

    private async Task<DateTimeOffset?> UpsertPullRequestsAsync(
        List<PullRequestDto> pullRequests,
        Action<int> progressCallback,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        DateTimeOffset? maxDate = null;

        var prIds = pullRequests.Select(p => p.Id).ToList();
        var existingIds = await _context.PullRequests
            .Where(p => prIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var existingSet = existingIds.ToHashSet();
        var totalBatches = (int)Math.Ceiling(pullRequests.Count / (double)batchSize);
        var processedBatches = 0;

        foreach (var batch in pullRequests.Chunk(batchSize))
        {
            // Load all existing entities for this batch in a single query
            var batchIds = batch.Where(d => existingSet.Contains(d.Id)).Select(d => d.Id).ToList();
            var existingEntities = await _context.PullRequests
                .Where(p => batchIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var dto in batch)
            {
                // Track max date for watermark (use RetrievedAt as proxy)
                if (maxDate == null || dto.RetrievedAt > maxDate)
                {
                    maxDate = dto.RetrievedAt;
                }

                if (existingEntities.TryGetValue(dto.Id, out var entity))
                {
                    // Update existing
                    UpdateEntity(entity, dto);
                }
                else
                {
                    // Insert new
                    var newEntity = MapToEntity(dto);
                    await _context.PullRequests.AddAsync(newEntity, cancellationToken);
                    existingSet.Add(dto.Id);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            processedBatches++;
            var percent = 80 + (int)((processedBatches / (double)totalBatches) * 20);
            progressCallback(Math.Min(percent, 99));
        }

        return maxDate;
    }

    private static void UpdateEntity(PullRequestEntity entity, PullRequestDto dto)
    {
        entity.RepositoryName = dto.RepositoryName;
        entity.Title = dto.Title;
        entity.CreatedBy = dto.CreatedBy;
        entity.CreatedDate = dto.CreatedDate;
        entity.CreatedDateUtc = dto.CreatedDate.UtcDateTime;
        entity.CompletedDate = dto.CompletedDate;
        entity.Status = dto.Status;
        entity.IterationPath = dto.IterationPath;
        entity.SourceBranch = dto.SourceBranch;
        entity.TargetBranch = dto.TargetBranch;
        entity.RetrievedAt = dto.RetrievedAt;
        entity.ProductId = dto.ProductId;
    }

    private static PullRequestEntity MapToEntity(PullRequestDto dto)
    {
        return new PullRequestEntity
        {
            Id = dto.Id,
            RepositoryName = dto.RepositoryName,
            Title = dto.Title,
            CreatedBy = dto.CreatedBy,
            CreatedDate = dto.CreatedDate,
            CreatedDateUtc = dto.CreatedDate.UtcDateTime,
            CompletedDate = dto.CompletedDate,
            Status = dto.Status,
            IterationPath = dto.IterationPath,
            SourceBranch = dto.SourceBranch,
            TargetBranch = dto.TargetBranch,
            RetrievedAt = dto.RetrievedAt,
            ProductId = dto.ProductId
        };
    }
}
