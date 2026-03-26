using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed class PortfolioComparisonQueryService
{
    private readonly IPortfolioReadModelStateService _stateService;
    private readonly IPortfolioReadModelMapper _mapper;
    private readonly IPortfolioSnapshotComparisonService _comparisonService;

    public PortfolioComparisonQueryService(
        IPortfolioReadModelStateService stateService,
        IPortfolioReadModelMapper mapper,
        IPortfolioSnapshotComparisonService comparisonService)
    {
        _stateService = stateService;
        _mapper = mapper;
        _comparisonService = comparisonService;
    }

    public async Task<PortfolioComparisonDto> GetAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var state = await _stateService.GetComparisonStateAsync(productOwnerId, options, cancellationToken);
        if (state is null)
        {
            return new PortfolioComparisonDto
            {
                PreviousSnapshotLabel = null,
                CurrentSnapshotLabel = "No data",
                PreviousTimestamp = null,
                CurrentTimestamp = DateTimeOffset.MinValue,
                TotalItemCount = 0,
                FilteredItemCount = 0,
                GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
                SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
                SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioComparisonItemDto>(),
                HasData = false
            };
        }

        var comparison = _comparisonService.Compare(new PortfolioSnapshotComparisonRequest(
            state.ComparisonSnapshot?.Snapshot,
            state.CurrentSnapshot.Snapshot));

        var mappedItems = comparison.Items
            .Select(item => _mapper.ToComparisonItemDto(item, state.ProductNames));
        var filteredItems = PortfolioReadModelFiltering.Apply(mappedItems, options);

        return new PortfolioComparisonDto
        {
            PreviousSnapshotLabel = state.ComparisonSnapshot?.Source,
            CurrentSnapshotLabel = state.CurrentSnapshot.Source,
            PreviousTimestamp = state.ComparisonSnapshot?.Snapshot.Timestamp,
            CurrentTimestamp = state.CurrentSnapshot.Snapshot.Timestamp,
            TotalItemCount = comparison.Items.Count,
            FilteredItemCount = filteredItems.Count,
            GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
            SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
            SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
            Items = filteredItems,
            HasData = true
        };
    }
}
