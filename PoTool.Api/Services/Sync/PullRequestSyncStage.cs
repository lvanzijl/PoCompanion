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

    /// <summary>
    /// Maximum number of concurrent TFS API calls when fetching PR detail data
    /// (iterations, comments, file changes). Kept low to avoid rate-limiting.
    /// </summary>
    private const int MaxConcurrentPrDetailFetches = 3;

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

            var repositoryProductMap = await BuildRepositoryProductMapAsync(context, cancellationToken);
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

                allPullRequests.AddRange(ApplyProductScope(prs, repoName, repositoryProductMap));
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
            await BackfillMissingProductIdsAsync(repositoryProductMap, cancellationToken);

            if (allPullRequests.Count == 0)
            {
                progressCallback(100);
                return SyncStageResult.CreateSuccess(0, context.PullRequestWatermark);
            }

            // Upsert pull requests to database
            var maxDate = await UpsertPullRequestsAsync(allPullRequests, progressCallback, cancellationToken);

            // Fetch and save related detail data (iterations, comments, file changes)
            // that are used by PR Insights to compute review cycles, comment counts, and file change counts.
            await UpsertPullRequestDetailDataAsync(allPullRequests, cancellationToken);

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

    private async Task<Dictionary<string, int>> BuildRepositoryProductMapAsync(
        SyncContext context,
        CancellationToken cancellationToken)
    {
        var repositoryNames = context.RepositoryNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (repositoryNames.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var repositoryRows = await _context.Repositories
            .AsNoTracking()
            .Where(r => repositoryNames.Contains(r.Name) && r.Product.ProductOwnerId == context.ProductOwnerId)
            .Select(r => new { r.Name, r.ProductId })
            .ToListAsync(cancellationToken);

        var repositoryProductMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in repositoryRows.GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase))
        {
            var productIds = group
                .Select(row => row.ProductId)
                .Distinct()
                .ToList();

            if (productIds.Count == 1)
            {
                repositoryProductMap[group.Key] = productIds[0];
            }
            else
            {
                _logger.LogWarning(
                    "PR_PRODUCT_SCOPE_AMBIGUOUS: ProductOwner {ProductOwnerId}, repository {RepositoryName} maps to multiple products [{ProductIds}]",
                    context.ProductOwnerId,
                    group.Key,
                    string.Join(", ", productIds));
            }
        }

        foreach (var repositoryName in repositoryNames.Where(name => !repositoryProductMap.ContainsKey(name)))
        {
            _logger.LogWarning(
                "PR_PRODUCT_SCOPE_MISSING: ProductOwner {ProductOwnerId}, repository {RepositoryName} has no unique product mapping",
                context.ProductOwnerId,
                repositoryName);
        }

        return repositoryProductMap;
    }

    private List<PullRequestDto> ApplyProductScope(
        IEnumerable<PullRequestDto> pullRequests,
        string repositoryName,
        IReadOnlyDictionary<string, int> repositoryProductMap)
    {
        if (!repositoryProductMap.TryGetValue(repositoryName, out var productId))
        {
            return pullRequests.ToList();
        }

        return pullRequests
            .Select(pr => pr with { ProductId = productId })
            .ToList();
    }

    private async Task BackfillMissingProductIdsAsync(
        IReadOnlyDictionary<string, int> repositoryProductMap,
        CancellationToken cancellationToken)
    {
        if (repositoryProductMap.Count == 0)
        {
            return;
        }

        var repositoryNames = repositoryProductMap.Keys.ToList();
        var existingPullRequests = await _context.PullRequests
            .Where(pr => !pr.ProductId.HasValue && repositoryNames.Contains(pr.RepositoryName))
            .ToListAsync(cancellationToken);

        var updatedCount = 0;

        foreach (var pullRequest in existingPullRequests)
        {
            if (!repositoryProductMap.TryGetValue(pullRequest.RepositoryName, out var productId))
            {
                continue;
            }

            pullRequest.ProductId = productId;
            updatedCount++;
        }

        if (updatedCount == 0)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PR_PRODUCT_SCOPE_BACKFILL: Updated ProductId for {Count} cached pull requests",
            updatedCount);
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

    /// <summary>
    /// Fetches and upserts iterations, comments, file changes, and explicit PR → work item links for each PR.
    /// These are required by PR Insights to compute review cycles, comment counts,
    /// files-changed counts, and PR Delivery Insights work item classification. The
    /// stage only ingests direct links returned by the PR work item endpoint; reverse
    /// or UI-inferred associations are not available in the local cache through this flow.
    ///
    /// A semaphore limits concurrent TFS API calls to avoid rate-limiting.
    /// Per-PR failures are logged as warnings and do not abort the stage.
    /// </summary>
    private async Task UpsertPullRequestDetailDataAsync(
        List<PullRequestDto> pullRequests,
        CancellationToken cancellationToken)
    {
        var allIterations  = new System.Collections.Concurrent.ConcurrentBag<PullRequestIterationDto>();
        var allComments    = new System.Collections.Concurrent.ConcurrentBag<PullRequestCommentDto>();
        var allFileChanges = new System.Collections.Concurrent.ConcurrentBag<PullRequestFileChangeDto>();
        // (PullRequestId, WorkItemId) pairs
        var allWorkItemLinks = new System.Collections.Concurrent.ConcurrentBag<(int PullRequestId, int WorkItemId)>();

        await Parallel.ForEachAsync(
            pullRequests,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentPrDetailFetches,
                CancellationToken      = cancellationToken
            },
            async (pr, ct) =>
            {
                try
                {
                    // Fetch iterations (used for review-cycle count)
                    var iterations = (await _tfsClient.GetPullRequestIterationsAsync(
                        pr.Id, pr.RepositoryName, ct)).ToList();

                    foreach (var it in iterations)
                        allIterations.Add(it);

                    // Fetch comments (used for comment count)
                    var comments = await _tfsClient.GetPullRequestCommentsAsync(
                        pr.Id, pr.RepositoryName, ct);

                    foreach (var c in comments)
                        allComments.Add(c);

                    // Fetch file changes for the last iteration only.
                    // Azure DevOps returns cumulative changes per iteration, so the last
                    // iteration contains all unique files changed across the whole PR.
                    if (iterations.Count > 0)
                    {
                        var lastIteration = iterations.OrderByDescending(i => i.IterationNumber).First();
                        var fileChanges = await _tfsClient.GetPullRequestFileChangesAsync(
                            pr.Id, pr.RepositoryName, lastIteration.IterationNumber, ct);

                        foreach (var fc in fileChanges)
                            allFileChanges.Add(fc);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "PR_DETAIL_SKIP: Could not fetch iteration/comment/file detail data for PR {PrId} in repo {Repo}",
                        pr.Id, pr.RepositoryName);
                }

                try
                {
                    // Keep work item link ingestion isolated from other PR detail fetch failures.
                    // A failure in comments or file changes must not suppress explicit PR → work item
                    // links, because PR Delivery Insights depends on those links for classification.
                    var workItemIds = (await _tfsClient.GetPullRequestWorkItemLinksAsync(
                        pr.Id, pr.RepositoryName, ct)).Distinct().ToList();

                    foreach (var wiId in workItemIds)
                        allWorkItemLinks.Add((pr.Id, wiId));

                    _logger.LogDebug(
                        "PR_WORK_ITEM_LINK_INGEST: PR {PrId} in repo {Repo} fetched {LinkCount} explicit work item link(s) [{WorkItemIds}]",
                        pr.Id,
                        pr.RepositoryName,
                        workItemIds.Count,
                        workItemIds.Count > 0 ? string.Join(", ", workItemIds) : "none");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "PR_WORK_ITEM_LINK_SKIP: Could not fetch explicit work item links for PR {PrId} in repo {Repo}",
                        pr.Id, pr.RepositoryName);
                }
            });

        var iterationsList  = allIterations.ToList();
        var commentsList    = allComments.ToList();
        var fileChangesList = allFileChanges.ToList();
        var workItemLinksList = allWorkItemLinks.ToList();

        _logger.LogInformation(
            "PR_DETAIL_INGEST: {IterCount} iterations, {CommentCount} comments, {FileCount} file-change rows, {LinkCount} work-item links fetched",
            iterationsList.Count, commentsList.Count, fileChangesList.Count, workItemLinksList.Count);

        if (iterationsList.Count > 0)
            await UpsertIterationsAsync(iterationsList, cancellationToken);

        if (commentsList.Count > 0)
            await UpsertCommentsAsync(commentsList, cancellationToken);

        if (fileChangesList.Count > 0)
            await UpsertFileChangesAsync(fileChangesList, cancellationToken);

        if (workItemLinksList.Count > 0)
            await UpsertWorkItemLinksAsync(workItemLinksList, cancellationToken);
    }

    private async Task UpsertIterationsAsync(
        List<PullRequestIterationDto> iterations,
        CancellationToken cancellationToken)
    {
        var prIds = iterations.Select(i => i.PullRequestId).Distinct().ToList();
        var existing = await _context.PullRequestIterations
            .Where(i => prIds.Contains(i.PullRequestId))
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(i => new { i.PullRequestId, i.IterationNumber })
            .ToHashSet();

        var toInsert = new List<PullRequestIterationEntity>();

        foreach (var dto in iterations)
        {
            var key = new { dto.PullRequestId, dto.IterationNumber };
            var existingEntity = existing.FirstOrDefault(
                i => i.PullRequestId == dto.PullRequestId &&
                     i.IterationNumber == dto.IterationNumber);

            if (existingEntity != null)
            {
                existingEntity.UpdatedDate = dto.UpdatedDate;
                existingEntity.CommitCount = dto.CommitCount;
                existingEntity.ChangeCount = dto.ChangeCount;
            }
            else if (!existingKeys.Contains(key))
            {
                toInsert.Add(new PullRequestIterationEntity
                {
                    PullRequestId   = dto.PullRequestId,
                    IterationNumber = dto.IterationNumber,
                    CreatedDate     = dto.CreatedDate,
                    UpdatedDate     = dto.UpdatedDate,
                    CommitCount     = dto.CommitCount,
                    ChangeCount     = dto.ChangeCount
                });
                existingKeys.Add(key);
            }
        }

        if (toInsert.Count > 0)
            await _context.PullRequestIterations.AddRangeAsync(toInsert, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertCommentsAsync(
        List<PullRequestCommentDto> comments,
        CancellationToken cancellationToken)
    {
        // TFS comment Id is only unique within a PR; deduplicate by composite key (PullRequestId, Id) first.
        var deduplicated = comments
            .GroupBy(c => (c.PullRequestId, c.Id))
            .Select(g => g.Last())
            .ToList();

        var prIds      = deduplicated.Select(c => c.PullRequestId).Distinct().ToList();
        var commentIds = deduplicated.Select(c => c.Id).Distinct().ToList();

        var existing = await _context.PullRequestComments
            .Where(c => prIds.Contains(c.PullRequestId) && commentIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        // Build dictionary for O(1) lookups when updating existing comments
        var commentsByKeyLookup = deduplicated.ToDictionary(c => (c.PullRequestId, c.Id));
        var existingKeys = existing
            .Select(c => (c.PullRequestId, c.Id))
            .ToHashSet();

        foreach (var existingComment in existing)
        {
            var key = (existingComment.PullRequestId, existingComment.Id);
            if (commentsByKeyLookup.TryGetValue(key, out var updated))
            {
                existingComment.Content      = updated.Content;
                existingComment.UpdatedDate  = updated.UpdatedDate;
                existingComment.IsResolved   = updated.IsResolved;
                existingComment.ResolvedDate = updated.ResolvedDate;
                existingComment.ResolvedBy   = updated.ResolvedBy;
            }
        }

        var toInsert = deduplicated
            .Where(c => !existingKeys.Contains((c.PullRequestId, c.Id)))
            .Select(c => new PullRequestCommentEntity
            {
                Id            = c.Id,
                PullRequestId = c.PullRequestId,
                ThreadId      = c.ThreadId,
                Author        = c.Author,
                Content       = c.Content,
                CreatedDate   = c.CreatedDate,
                CreatedDateUtc = c.CreatedDate.UtcDateTime,
                UpdatedDate   = c.UpdatedDate,
                IsResolved    = c.IsResolved,
                ResolvedDate  = c.ResolvedDate,
                ResolvedBy    = c.ResolvedBy
            });

        await _context.PullRequestComments.AddRangeAsync(toInsert, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertFileChangesAsync(
        List<PullRequestFileChangeDto> fileChanges,
        CancellationToken cancellationToken)
    {
        // Group by PR + iteration and replace all rows for each group
        var groups = fileChanges
            .GroupBy(fc => new { fc.PullRequestId, fc.IterationId })
            .ToList();

        var prIds        = groups.Select(g => g.Key.PullRequestId).Distinct().ToList();
        var iterationIds = groups.Select(g => g.Key.IterationId).Distinct().ToList();

        var existing = await _context.PullRequestFileChanges
            .Where(fc => prIds.Contains(fc.PullRequestId) && iterationIds.Contains(fc.IterationId))
            .ToListAsync(cancellationToken);

        var groupKeys = groups
            .Select(g => new { g.Key.PullRequestId, g.Key.IterationId })
            .ToHashSet();

        var toRemove = existing
            .Where(fc => groupKeys.Contains(new { fc.PullRequestId, fc.IterationId }))
            .ToList();

        _context.PullRequestFileChanges.RemoveRange(toRemove);

        var toInsert = groups.SelectMany(g => g.Select(fc => new PullRequestFileChangeEntity
        {
            PullRequestId = fc.PullRequestId,
            IterationId   = fc.IterationId,
            FilePath      = fc.FilePath,
            ChangeType    = fc.ChangeType,
            LinesAdded    = fc.LinesAdded,
            LinesDeleted  = fc.LinesDeleted,
            LinesModified = fc.LinesModified
        }));

        await _context.PullRequestFileChanges.AddRangeAsync(toInsert, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertWorkItemLinksAsync(
        List<(int PullRequestId, int WorkItemId)> links,
        CancellationToken cancellationToken)
    {
        var prIds = links.Select(l => l.PullRequestId).Distinct().ToList();

        var existing = await _context.PullRequestWorkItemLinks
            .Where(l => prIds.Contains(l.PullRequestId))
            .Select(l => new { l.PullRequestId, l.WorkItemId })
            .ToListAsync(cancellationToken);

        var existingSet = existing
            .Select(l => (l.PullRequestId, l.WorkItemId))
            .ToHashSet();

        var toInsert = links
            .Where(l => !existingSet.Contains((l.PullRequestId, l.WorkItemId)))
            .Select(l => new PullRequestWorkItemLinkEntity
            {
                PullRequestId = l.PullRequestId,
                WorkItemId    = l.WorkItemId
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            await _context.PullRequestWorkItemLinks.AddRangeAsync(toInsert, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
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
