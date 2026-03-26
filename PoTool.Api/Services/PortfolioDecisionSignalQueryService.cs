using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed class PortfolioDecisionSignalQueryService
{
    private readonly IPortfolioReadModelStateService _stateService;
    private readonly IPortfolioTrendAnalysisService _trendAnalysisService;
    private readonly IPortfolioDecisionSignalService _decisionSignalService;
    private readonly IPortfolioSnapshotComparisonService _comparisonService;
    private readonly IPortfolioReadModelMapper _mapper;

    public PortfolioDecisionSignalQueryService(
        IPortfolioReadModelStateService stateService,
        IPortfolioTrendAnalysisService trendAnalysisService,
        IPortfolioDecisionSignalService decisionSignalService,
        IPortfolioSnapshotComparisonService comparisonService,
        IPortfolioReadModelMapper mapper)
    {
        _stateService = stateService;
        _trendAnalysisService = trendAnalysisService;
        _decisionSignalService = decisionSignalService;
        _comparisonService = comparisonService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<PortfolioDecisionSignalDto>> GetAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var historyState = await _stateService.GetHistoryStateAsync(productOwnerId, options, cancellationToken);
        var comparisonState = await _stateService.GetComparisonStateAsync(productOwnerId, options, cancellationToken);
        if (historyState is null || comparisonState is null)
        {
            return Array.Empty<PortfolioDecisionSignalDto>();
        }

        var trend = _trendAnalysisService.BuildTrend(historyState.Snapshots, historyState.ProductNames, options) with
        {
            ArchivedSnapshotsExcludedNotice = historyState.ArchivedSnapshotsExcludedNotice
        };

        var comparisonResult = _comparisonService.Compare(new(
            comparisonState.ComparisonSnapshot?.Snapshot,
            comparisonState.CurrentSnapshot.Snapshot));
        var comparisonDto = new PortfolioComparisonDto
        {
            PreviousSnapshotLabel = comparisonState.ComparisonSnapshot?.Source,
            CurrentSnapshotLabel = comparisonState.CurrentSnapshot.Source,
            PreviousTimestamp = comparisonState.ComparisonSnapshot?.Snapshot.Timestamp,
            CurrentTimestamp = comparisonState.CurrentSnapshot.Snapshot.Timestamp,
            TotalItemCount = comparisonResult.Items.Count,
            FilteredItemCount = comparisonResult.Items.Count,
            GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
            SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
            SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
            Items = comparisonResult.Items
                .Select(item => _mapper.ToComparisonItemDto(item, comparisonState.ProductNames))
                .ToArray(),
            HasData = true
        };

        return PortfolioReadModelFiltering.Apply(
            _decisionSignalService.BuildSignals(trend, comparisonDto),
            options);
    }
}
