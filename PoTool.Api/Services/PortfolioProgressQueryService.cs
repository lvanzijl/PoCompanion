using PoTool.Shared.Metrics;

namespace PoTool.Api.Services;

public sealed class PortfolioProgressQueryService
{
    private readonly IPortfolioReadModelStateService _stateService;
    private readonly IPortfolioReadModelMapper _mapper;
    private readonly PortfolioFilterResolutionService _filterResolutionService;

    public PortfolioProgressQueryService(
        IPortfolioReadModelStateService stateService,
        IPortfolioReadModelMapper mapper,
        PortfolioFilterResolutionService filterResolutionService)
    {
        _stateService = stateService;
        _mapper = mapper;
        _filterResolutionService = filterResolutionService;
    }

    public async Task<PortfolioProgressDto> GetAsync(
        int productOwnerId,
        PortfolioReadQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var resolution = await _filterResolutionService.ResolveAsync(
            productOwnerId,
            options,
            nameof(PortfolioProgressQueryService),
            cancellationToken);

        var state = await _stateService.GetLatestStateAsync(productOwnerId, resolution.EffectiveFilter, cancellationToken);
        if (state is null)
        {
            return new PortfolioProgressDto
            {
                SnapshotLabel = "No data",
                SnapshotTimestamp = DateTimeOffset.MinValue,
                PortfolioProgress = null,
                TotalWeight = 0d,
                TotalItemCount = 0,
                FilteredItemCount = 0,
                GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
                SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
                SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
                Items = Array.Empty<PortfolioSnapshotItemDto>(),
                HasData = false,
                Filter = PortfolioFilterResolutionService.ToMetadata(resolution)
            };
        }

        var mappedItems = state.CurrentSnapshot.Items
            .Select(item => _mapper.ToSnapshotItemDto(item, state.ProductNames));
        var filteredItems = PortfolioReadModelFiltering.Apply(mappedItems, resolution.EffectiveFilter, options);

        return new PortfolioProgressDto
        {
            SnapshotLabel = state.CurrentSnapshotLabel,
            SnapshotTimestamp = state.CurrentSnapshot.Timestamp,
            PortfolioProgress = state.PortfolioProgress,
            TotalWeight = state.TotalWeight,
            TotalItemCount = state.CurrentSnapshot.Items.Count,
            FilteredItemCount = filteredItems.Count,
            GroupBy = options?.GroupBy ?? PortfolioReadGroupBy.None,
            SortBy = options?.SortBy ?? PortfolioReadSortBy.Default,
            SortDirection = options?.SortDirection ?? PortfolioReadSortDirection.Desc,
            Items = filteredItems,
            HasData = true,
            Filter = PortfolioFilterResolutionService.ToMetadata(resolution)
        };
    }
}
