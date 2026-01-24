using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Services;

/// <summary>
/// Cached pull request read provider that reads from the local database.
/// Used when DataSourceMode is Cache (after sync).
/// </summary>
public sealed class CachedPullRequestReadProvider : IPullRequestReadProvider
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<CachedPullRequestReadProvider> _logger;

    public CachedPullRequestReadProvider(
        PoToolDbContext dbContext,
        ILogger<CachedPullRequestReadProvider> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all pull requests from the local cache.
    /// </summary>
    public async Task<IEnumerable<PullRequestDto>> GetAllAsync(DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPullRequestReadProvider: Fetching all PRs from cache, fromDate: {FromDate}", fromDate);

        var query = _dbContext.PullRequests.AsNoTracking();

        if (fromDate.HasValue)
        {
            query = query.Where(pr => pr.CreatedDate >= fromDate.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        return entities.Select(MapToDto);
    }

    /// <summary>
    /// Retrieves pull requests filtered by product IDs from the cache.
    /// </summary>
    public async Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, DateTimeOffset? fromDate = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPullRequestReadProvider: Fetching PRs by product IDs: {ProductIds}, fromDate: {FromDate}", 
            productIds != null ? string.Join(", ", productIds) : "all", fromDate);

        var query = _dbContext.PullRequests.AsNoTracking();

        if (productIds != null && productIds.Count > 0)
        {
            query = query.Where(pr => pr.ProductId.HasValue && productIds.Contains(pr.ProductId.Value));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(pr => pr.CreatedDate >= fromDate.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        return entities.Select(MapToDto);
    }

    /// <summary>
    /// Retrieves a specific pull request by ID from the cache.
    /// </summary>
    public async Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPullRequestReadProvider: Fetching PR by ID: {PullRequestId}", pullRequestId);

        var entity = await _dbContext.PullRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.Id == pullRequestId, cancellationToken);

        return entity != null ? MapToDto(entity) : null;
    }

    /// <summary>
    /// Retrieves iterations for a pull request from the cache.
    /// </summary>
    public async Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPullRequestReadProvider: Fetching iterations for PR: {PullRequestId}", pullRequestId);

        var entities = await _dbContext.PullRequestIterations
            .AsNoTracking()
            .Where(i => i.PullRequestId == pullRequestId)
            .OrderBy(i => i.IterationNumber)
            .ToListAsync(cancellationToken);

        return entities.Select(MapIterationToDto);
    }

    /// <summary>
    /// Retrieves iterations for a pull request from the cache (repository name ignored for cached provider).
    /// </summary>
    public Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        // Repository name is not needed for cached lookups
        return GetIterationsAsync(pullRequestId, cancellationToken);
    }

    /// <summary>
    /// Retrieves comments for a pull request from the cache.
    /// </summary>
    public async Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPullRequestReadProvider: Fetching comments for PR: {PullRequestId}", pullRequestId);

        var entities = await _dbContext.PullRequestComments
            .AsNoTracking()
            .Where(c => c.PullRequestId == pullRequestId)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync(cancellationToken);

        return entities.Select(MapCommentToDto);
    }

    /// <summary>
    /// Retrieves comments for a pull request from the cache (repository name ignored for cached provider).
    /// </summary>
    public Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        // Repository name is not needed for cached lookups
        return GetCommentsAsync(pullRequestId, cancellationToken);
    }

    /// <summary>
    /// Retrieves file changes for a pull request from the cache.
    /// </summary>
    public async Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CachedPullRequestReadProvider: Fetching file changes for PR: {PullRequestId}", pullRequestId);

        var entities = await _dbContext.PullRequestFileChanges
            .AsNoTracking()
            .Where(fc => fc.PullRequestId == pullRequestId)
            .ToListAsync(cancellationToken);

        return entities.Select(MapFileChangeToDto);
    }

    /// <summary>
    /// Retrieves file changes for a pull request from the cache (repository name ignored for cached provider).
    /// </summary>
    public Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, string repositoryName, CancellationToken cancellationToken = default)
    {
        // Repository name is not needed for cached lookups
        return GetFileChangesAsync(pullRequestId, cancellationToken);
    }

    private static PullRequestDto MapToDto(PullRequestEntity entity)
    {
        return new PullRequestDto(
            Id: entity.Id,
            RepositoryName: entity.RepositoryName,
            Title: entity.Title,
            CreatedBy: entity.CreatedBy,
            CreatedDate: entity.CreatedDate,
            CompletedDate: entity.CompletedDate,
            Status: entity.Status,
            IterationPath: entity.IterationPath,
            SourceBranch: entity.SourceBranch,
            TargetBranch: entity.TargetBranch,
            RetrievedAt: entity.RetrievedAt,
            ProductId: entity.ProductId
        );
    }

    private static PullRequestIterationDto MapIterationToDto(PullRequestIterationEntity entity)
    {
        return new PullRequestIterationDto(
            PullRequestId: entity.PullRequestId,
            IterationNumber: entity.IterationNumber,
            CreatedDate: entity.CreatedDate,
            UpdatedDate: entity.UpdatedDate,
            CommitCount: entity.CommitCount,
            ChangeCount: entity.ChangeCount
        );
    }

    private static PullRequestCommentDto MapCommentToDto(PullRequestCommentEntity entity)
    {
        return new PullRequestCommentDto(
            Id: entity.Id,
            PullRequestId: entity.PullRequestId,
            ThreadId: entity.ThreadId,
            Author: entity.Author,
            Content: entity.Content,
            CreatedDate: entity.CreatedDate,
            UpdatedDate: entity.UpdatedDate,
            IsResolved: entity.IsResolved,
            ResolvedDate: entity.ResolvedDate,
            ResolvedBy: entity.ResolvedBy
        );
    }

    private static PullRequestFileChangeDto MapFileChangeToDto(PullRequestFileChangeEntity entity)
    {
        return new PullRequestFileChangeDto(
            PullRequestId: entity.PullRequestId,
            IterationId: entity.IterationId,
            FilePath: entity.FilePath,
            ChangeType: entity.ChangeType,
            LinesAdded: entity.LinesAdded,
            LinesDeleted: entity.LinesDeleted,
            LinesModified: entity.LinesModified
        );
    }
}
