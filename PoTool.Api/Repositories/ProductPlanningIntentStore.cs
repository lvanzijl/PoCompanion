using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Planning;

namespace PoTool.Api.Repositories;

public sealed class ProductPlanningIntentStore : IProductPlanningIntentStore
{
    private readonly PoToolDbContext _context;

    public ProductPlanningIntentStore(PoToolDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProductPlanningIntentRecord>> GetByProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _context.ProductPlanningIntents
            .AsNoTracking()
            .Where(intent => intent.ProductId == productId)
            .OrderBy(intent => intent.StartSprintStartDateUtc)
            .ThenBy(intent => intent.EpicId)
            .Select(intent => new ProductPlanningIntentRecord(
                intent.ProductId,
                intent.EpicId,
                intent.StartSprintStartDateUtc,
                intent.DurationInSprints,
                intent.RecoveryStatus,
                intent.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertForProductAsync(int productId, IReadOnlyList<ProductPlanningIntentRecord> intents, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ProductPlanningIntents
            .Where(intent => intent.ProductId == productId)
            .ToDictionaryAsync(intent => intent.EpicId, cancellationToken);

        foreach (var intent in intents)
        {
            if (existing.TryGetValue(intent.EpicId, out var entity))
            {
                entity.StartSprintStartDateUtc = intent.StartSprintStartDateUtc;
                entity.DurationInSprints = intent.DurationInSprints;
                entity.RecoveryStatus = intent.RecoveryStatus;
                entity.UpdatedAtUtc = intent.UpdatedAtUtc;
                continue;
            }

            await _context.ProductPlanningIntents.AddAsync(new ProductPlanningIntentEntity
            {
                ProductId = intent.ProductId,
                EpicId = intent.EpicId,
                StartSprintStartDateUtc = intent.StartSprintStartDateUtc,
                DurationInSprints = intent.DurationInSprints,
                RecoveryStatus = intent.RecoveryStatus,
                UpdatedAtUtc = intent.UpdatedAtUtc
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMissingEpicsAsync(int productId, IReadOnlyCollection<int> activeEpicIds, CancellationToken cancellationToken = default)
    {
        var staleIntents = await _context.ProductPlanningIntents
            .Where(intent => intent.ProductId == productId && !activeEpicIds.Contains(intent.EpicId))
            .ToListAsync(cancellationToken);

        if (staleIntents.Count == 0)
        {
            return;
        }

        _context.ProductPlanningIntents.RemoveRange(staleIntents);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var intents = await _context.ProductPlanningIntents
            .Where(intent => intent.ProductId == productId)
            .ToListAsync(cancellationToken);

        if (intents.Count == 0)
        {
            return;
        }

        _context.ProductPlanningIntents.RemoveRange(intents);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
