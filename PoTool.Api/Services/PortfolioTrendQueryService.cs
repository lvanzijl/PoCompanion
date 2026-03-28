using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed class PortfolioTrendQueryService
{
    private readonly IPortfolioReadModelStateService _stateService;
    private readonly IPortfolioTrendAnalysisService _trendAnalysisService;
    private readonly PortfolioFilterResolutionService _filterResolutionService;

    public PortfolioTrendQueryService(
        IPortfolioReadModelStateService stateService,
        IPortfolioTrendAnalysisService trendAnalysisService,
        PortfolioFilterResolutionService filterResolutionService)
    {
        _stateService = stateService;
        _trendAnalysisService = trendAnalysisService;
        _filterResolutionService = filterResolutionService;
    }

    public async Task<PortfolioTrendDto> GetAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var resolution = await _filterResolutionService.ResolveAsync(
            productOwnerId,
            options,
            nameof(PortfolioTrendQueryService),
            cancellationToken);

        var state = await _stateService.GetHistoryStateAsync(productOwnerId, resolution.EffectiveFilter, options, cancellationToken);
        if (state is null)
        {
            return new PortfolioTrendDto
            {
                Snapshots = Array.Empty<PortfolioHistoricalSnapshotDto>(),
                PortfolioProgressTrend = new PortfolioMetricTrendDto
                {
                    CurrentValue = null,
                    PreviousValue = null,
                    Delta = null,
                    Direction = null,
                    Points = Array.Empty<PortfolioTrendPointDto>()
                },
                TotalWeightTrend = new PortfolioMetricTrendDto
                {
                    CurrentValue = null,
                    PreviousValue = null,
                    Delta = null,
                    Direction = null,
                    Points = Array.Empty<PortfolioTrendPointDto>()
                },
                Projects = Array.Empty<PortfolioScopedTrendDto>(),
                WorkPackages = Array.Empty<PortfolioScopedTrendDto>(),
                SnapshotCount = options?.SnapshotCount ?? 6,
                RangeStartUtc = options?.RangeStartUtc,
                RangeEndUtc = options?.RangeEndUtc,
                IncludesArchivedSnapshots = options?.IncludeArchivedSnapshots ?? false,
                ArchivedSnapshotsExcludedByDefault = true,
                ArchivedSnapshotsExcludedNotice = false,
                HasData = false,
                Filter = PortfolioFilterResolutionService.ToMetadata(resolution)
            };
        }

        var trend = _trendAnalysisService.BuildTrend(state.Snapshots, state.ProductNames, resolution.EffectiveFilter, options);
        return trend with
        {
            ArchivedSnapshotsExcludedNotice = state.ArchivedSnapshotsExcludedNotice,
            Filter = PortfolioFilterResolutionService.ToMetadata(resolution)
        };
    }
}
