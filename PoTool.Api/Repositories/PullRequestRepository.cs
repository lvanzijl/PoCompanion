using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Api.Repositories;

/// <summary>
/// Repository implementation for pull request persistence.
/// </summary>
public class PullRequestRepository : IPullRequestRepository
{
    private readonly PoToolDbContext _context;

    public PullRequestRepository(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<PullRequestDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.PullRequests
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    public async Task<IEnumerable<PullRequestDto>> GetByProductIdsAsync(List<int>? productIds, CancellationToken cancellationToken = default)
    {
        var query = _context.PullRequests.AsNoTracking();

        if (productIds != null && productIds.Count > 0)
        {
            query = query.Where(pr => pr.ProductId != null && productIds.Contains(pr.ProductId.Value));
        }

        var entities = await query.ToListAsync(cancellationToken);

        return entities.Select(MapToDto);
    }

    public async Task<PullRequestDto?> GetByIdAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.PullRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.Id == pullRequestId, cancellationToken);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task SaveAsync(IEnumerable<PullRequestDto> pullRequests, CancellationToken cancellationToken = default)
    {
        var prList = pullRequests.ToList();
        var prIds = prList.Select(pr => pr.Id).ToList();

        // Get existing PRs
        var existingPrs = await _context.PullRequests
            .Where(pr => prIds.Contains(pr.Id))
            .ToListAsync(cancellationToken);

        var existingPrIds = existingPrs.Select(pr => pr.Id).ToHashSet();

        // Update existing PRs
        foreach (var existingPr in existingPrs)
        {
            var updatedPr = prList.First(pr => pr.Id == existingPr.Id);
            UpdateEntity(existingPr, updatedPr);
        }

        // Add new PRs
        var newPrs = prList.Where(pr => !existingPrIds.Contains(pr.Id)).Select(MapToEntity);
        await _context.PullRequests.AddRangeAsync(newPrs, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveIterationsAsync(IEnumerable<PullRequestIterationDto> iterations, CancellationToken cancellationToken = default)
    {
        var iterationsList = iterations.ToList();

        foreach (var iteration in iterationsList)
        {
            var existing = await _context.PullRequestIterations
                .FirstOrDefaultAsync(
                    i => i.PullRequestId == iteration.PullRequestId &&
                         i.IterationNumber == iteration.IterationNumber,
                    cancellationToken);

            if (existing != null)
            {
                UpdateIterationEntity(existing, iteration);
            }
            else
            {
                await _context.PullRequestIterations.AddAsync(MapToIterationEntity(iteration), cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<PullRequestIterationDto>> GetIterationsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.PullRequestIterations
            .AsNoTracking()
            .Where(i => i.PullRequestId == pullRequestId)
            .OrderBy(i => i.IterationNumber)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToIterationDto);
    }

    public async Task SaveCommentsAsync(IEnumerable<PullRequestCommentDto> comments, CancellationToken cancellationToken = default)
    {
        var commentsList = comments.ToList();
        var commentIds = commentsList.Select(c => c.Id).ToList();

        var existingComments = await _context.PullRequestComments
            .Where(c => commentIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var existingCommentIds = existingComments.Select(c => c.Id).ToHashSet();

        foreach (var existingComment in existingComments)
        {
            var updatedComment = commentsList.First(c => c.Id == existingComment.Id);
            UpdateCommentEntity(existingComment, updatedComment);
        }

        var newComments = commentsList.Where(c => !existingCommentIds.Contains(c.Id)).Select(MapToCommentEntity);
        await _context.PullRequestComments.AddRangeAsync(newComments, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<PullRequestCommentDto>> GetCommentsAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.PullRequestComments
            .AsNoTracking()
            .Where(c => c.PullRequestId == pullRequestId)
            .OrderBy(c => c.CreatedDate.Ticks)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToCommentDto);
    }

    public async Task SaveFileChangesAsync(IEnumerable<PullRequestFileChangeDto> fileChanges, CancellationToken cancellationToken = default)
    {
        var fileChangesList = fileChanges.ToList();

        // For file changes, we'll just replace all for a given PR+Iteration
        if (fileChangesList.Count > 0)
        {
            var prId = fileChangesList.First().PullRequestId;
            var iterationId = fileChangesList.First().IterationId;

            var existing = await _context.PullRequestFileChanges
                .Where(fc => fc.PullRequestId == prId && fc.IterationId == iterationId)
                .ToListAsync(cancellationToken);

            _context.PullRequestFileChanges.RemoveRange(existing);

            var entities = fileChangesList.Select(MapToFileChangeEntity);
            await _context.PullRequestFileChanges.AddRangeAsync(entities, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<PullRequestFileChangeDto>> GetFileChangesAsync(int pullRequestId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.PullRequestFileChanges
            .AsNoTracking()
            .Where(fc => fc.PullRequestId == pullRequestId)
            .OrderBy(fc => fc.IterationId)
            .ThenBy(fc => fc.FilePath)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToFileChangeDto);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        if (isInMemory)
        {
            var fileChanges = await _context.PullRequestFileChanges.ToListAsync(cancellationToken);
            var comments = await _context.PullRequestComments.ToListAsync(cancellationToken);
            var iterations = await _context.PullRequestIterations.ToListAsync(cancellationToken);
            var prs = await _context.PullRequests.ToListAsync(cancellationToken);

            _context.PullRequestFileChanges.RemoveRange(fileChanges);
            _context.PullRequestComments.RemoveRange(comments);
            _context.PullRequestIterations.RemoveRange(iterations);
            _context.PullRequests.RemoveRange(prs);

            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await _context.PullRequestFileChanges.ExecuteDeleteAsync(cancellationToken);
            await _context.PullRequestComments.ExecuteDeleteAsync(cancellationToken);
            await _context.PullRequestIterations.ExecuteDeleteAsync(cancellationToken);
            await _context.PullRequests.ExecuteDeleteAsync(cancellationToken);
        }
    }

    // Mapping methods
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
            RetrievedAt: entity.RetrievedAt
        );
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
            CompletedDate = dto.CompletedDate,
            Status = dto.Status,
            IterationPath = dto.IterationPath,
            SourceBranch = dto.SourceBranch,
            TargetBranch = dto.TargetBranch,
            RetrievedAt = dto.RetrievedAt
        };
    }

    private static void UpdateEntity(PullRequestEntity entity, PullRequestDto dto)
    {
        entity.RepositoryName = dto.RepositoryName;
        entity.Title = dto.Title;
        entity.CreatedBy = dto.CreatedBy;
        entity.CreatedDate = dto.CreatedDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.Status = dto.Status;
        entity.IterationPath = dto.IterationPath;
        entity.SourceBranch = dto.SourceBranch;
        entity.TargetBranch = dto.TargetBranch;
        entity.RetrievedAt = dto.RetrievedAt;
    }

    private static PullRequestIterationDto MapToIterationDto(PullRequestIterationEntity entity)
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

    private static PullRequestIterationEntity MapToIterationEntity(PullRequestIterationDto dto)
    {
        return new PullRequestIterationEntity
        {
            PullRequestId = dto.PullRequestId,
            IterationNumber = dto.IterationNumber,
            CreatedDate = dto.CreatedDate,
            UpdatedDate = dto.UpdatedDate,
            CommitCount = dto.CommitCount,
            ChangeCount = dto.ChangeCount
        };
    }

    private static void UpdateIterationEntity(PullRequestIterationEntity entity, PullRequestIterationDto dto)
    {
        entity.UpdatedDate = dto.UpdatedDate;
        entity.CommitCount = dto.CommitCount;
        entity.ChangeCount = dto.ChangeCount;
    }

    private static PullRequestCommentDto MapToCommentDto(PullRequestCommentEntity entity)
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

    private static PullRequestCommentEntity MapToCommentEntity(PullRequestCommentDto dto)
    {
        return new PullRequestCommentEntity
        {
            Id = dto.Id,
            PullRequestId = dto.PullRequestId,
            ThreadId = dto.ThreadId,
            Author = dto.Author,
            Content = dto.Content,
            CreatedDate = dto.CreatedDate,
            UpdatedDate = dto.UpdatedDate,
            IsResolved = dto.IsResolved,
            ResolvedDate = dto.ResolvedDate,
            ResolvedBy = dto.ResolvedBy
        };
    }

    private static void UpdateCommentEntity(PullRequestCommentEntity entity, PullRequestCommentDto dto)
    {
        entity.Content = dto.Content;
        entity.UpdatedDate = dto.UpdatedDate;
        entity.IsResolved = dto.IsResolved;
        entity.ResolvedDate = dto.ResolvedDate;
        entity.ResolvedBy = dto.ResolvedBy;
    }

    private static PullRequestFileChangeDto MapToFileChangeDto(PullRequestFileChangeEntity entity)
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

    private static PullRequestFileChangeEntity MapToFileChangeEntity(PullRequestFileChangeDto dto)
    {
        return new PullRequestFileChangeEntity
        {
            PullRequestId = dto.PullRequestId,
            IterationId = dto.IterationId,
            FilePath = dto.FilePath,
            ChangeType = dto.ChangeType,
            LinesAdded = dto.LinesAdded,
            LinesDeleted = dto.LinesDeleted,
            LinesModified = dto.LinesModified
        };
    }
}
