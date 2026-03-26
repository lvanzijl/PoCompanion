using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed class PortfolioSnapshotQueryService
{
    private readonly IPortfolioReadModelStateService _stateService;
    private readonly IPortfolioReadModelMapper _mapper;

    public PortfolioSnapshotQueryService(
        IPortfolioReadModelStateService stateService,
        IPortfolioReadModelMapper mapper)
    {
        _stateService = stateService;
        _mapper = mapper;
    }

    public async Task<PortfolioSnapshotDto> GetAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var state = await _stateService.GetLatestStateAsync(productOwnerId, cancellationToken);
        if (state is null)
        {
            return new PortfolioSnapshotDto
            {
                SnapshotLabel = "No data",
                Timestamp = DateTimeOffset.MinValue,
                TotalItemCount = 0,
                FilteredItemCount = 0,
                GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
                SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
                SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioSnapshotItemDto>(),
                HasData = false
            };
        }

        var mappedItems = state.CurrentSnapshot.Items
            .Select(item => _mapper.ToSnapshotItemDto(item, state.ProductNames));
        var filteredItems = PortfolioReadModelFiltering.Apply(mappedItems, options);

        return new PortfolioSnapshotDto
        {
            SnapshotLabel = state.CurrentSnapshotLabel,
            Timestamp = state.CurrentSnapshot.Timestamp,
            TotalItemCount = state.CurrentSnapshot.Items.Count,
            FilteredItemCount = filteredItems.Count,
            GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
            SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
            SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
            Items = filteredItems,
            HasData = true
        };
    }
}
