using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Api.Services;

public sealed record PortfolioSnapshotCaptureResult(
    int ProductCount,
    int SourceCount,
    int CreatedSnapshotCount,
    int ExistingSnapshotCount);

public interface IPortfolioSnapshotCaptureOrchestrator
{
    Task<PortfolioSnapshotCaptureResult> CaptureLatestAsync(
        int productOwnerId,
        CancellationToken cancellationToken);
}

public sealed class PortfolioSnapshotCaptureOrchestrator : IPortfolioSnapshotCaptureOrchestrator
{
    private const string EmptyPortfolioSource = "Empty portfolio";
    private readonly PoToolDbContext _context;
    private readonly IPortfolioSnapshotCaptureDataService _captureDataService;
    private readonly IPortfolioSnapshotFactory _portfolioSnapshotFactory;
    private readonly IPortfolioSnapshotPersistenceService _persistenceService;
    private readonly IPortfolioSnapshotSelectionService _selectionService;

    public PortfolioSnapshotCaptureOrchestrator(
        PoToolDbContext context,
        IPortfolioSnapshotCaptureDataService captureDataService,
        IPortfolioSnapshotFactory portfolioSnapshotFactory,
        IPortfolioSnapshotPersistenceService persistenceService,
        IPortfolioSnapshotSelectionService selectionService)
    {
        _context = context;
        _captureDataService = captureDataService;
        _portfolioSnapshotFactory = portfolioSnapshotFactory;
        _persistenceService = persistenceService;
        _selectionService = selectionService;
    }

    public async Task<PortfolioSnapshotCaptureResult> CaptureLatestAsync(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        var productIds = await _context.Products
            .AsNoTracking()
            .Where(product => product.ProductOwnerId == productOwnerId)
            .OrderBy(product => product.Id)
            .Select(product => product.Id)
            .ToArrayAsync(cancellationToken);

        if (productIds.Length == 0)
        {
            return new PortfolioSnapshotCaptureResult(0, 0, 0, 0);
        }

        var sources = await _captureDataService.GetLatestSourcesAsync(productIds, cancellationToken);
        if (sources.Count == 0)
        {
            sources = [await BuildEmptyCaptureSourceAsync(cancellationToken)];
        }

        var createdCount = 0;
        var existingCount = 0;

        foreach (var source in sources
                     .OrderBy(snapshotSource => snapshotSource.EndDateUtc)
                     .ThenBy(snapshotSource => snapshotSource.SprintId))
        {
            var inputsByProduct = await _captureDataService.BuildSnapshotInputsByProductAsync(
                productOwnerId,
                source,
                cancellationToken);

            foreach (var productId in productIds)
            {
                var existing = await _persistenceService.GetBySourceAsync(
                    productId,
                    source.Source,
                    source.Timestamp,
                    cancellationToken);
                if (existing is not null)
                {
                    existingCount++;
                    continue;
                }

                var previousSnapshot = await _selectionService.GetLatestAtOrBeforeAsync(
                    productId,
                    source.Timestamp,
                    cancellationToken,
                    includeArchived: true);
                var inputs = inputsByProduct.TryGetValue(productId, out var productInputs)
                    ? productInputs
                    : Array.Empty<PortfolioSnapshotFactoryEpicInput>();

                var snapshot = _portfolioSnapshotFactory.Create(new PortfolioSnapshotFactoryRequest(
                    source.Timestamp,
                    inputs,
                    previousSnapshot?.Snapshot));

                await _persistenceService.PersistAsync(
                    productId,
                    source.Source,
                    createdBy: null,
                    snapshot,
                    cancellationToken);
                createdCount++;
            }
        }

        return new PortfolioSnapshotCaptureResult(
            productIds.Length,
            sources.Count,
            createdCount,
            existingCount);
    }

    private async Task<PortfolioSnapshotCaptureSource> BuildEmptyCaptureSourceAsync(CancellationToken cancellationToken)
    {
        var latestSprint = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.StartDateUtc != null && sprint.EndDateUtc != null)
            .OrderByDescending(sprint => sprint.EndDateUtc)
            .ThenByDescending(sprint => sprint.Id)
            .Select(sprint => new
            {
                sprint.Id,
                sprint.Name,
                StartDateUtc = sprint.StartDateUtc!.Value,
                EndDateUtc = sprint.EndDateUtc!.Value
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSprint is not null)
        {
            return new PortfolioSnapshotCaptureSource(
                latestSprint.Id,
                string.IsNullOrWhiteSpace(latestSprint.Name) ? $"Sprint {latestSprint.Id}" : latestSprint.Name.Trim(),
                DateTime.SpecifyKind(latestSprint.StartDateUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(latestSprint.EndDateUtc, DateTimeKind.Utc));
        }

        return new PortfolioSnapshotCaptureSource(
            0,
            EmptyPortfolioSource,
            DateTime.UnixEpoch,
            DateTime.UnixEpoch);
    }
}
