using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Filters;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;
using Mediator;

namespace PoTool.Api.Services;

public sealed class SprintScopedWorkItemLoader
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly IMediator _mediator;

    public SprintScopedWorkItemLoader(
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        IMediator mediator)
    {
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _mediator = mediator;
    }

    public async Task<IReadOnlyList<WorkItemDto>> LoadAsync(
        SprintEffectiveFilter effectiveFilter,
        CancellationToken cancellationToken)
    {
        IEnumerable<WorkItemDto> workItems;

        if (!effectiveFilter.Context.ProductIds.IsAll)
        {
            var products = new List<ProductDto>();
            foreach (var productId in effectiveFilter.Context.ProductIds.Values.Distinct())
            {
                var product = await _productRepository.GetProductByIdAsync(productId, cancellationToken);
                if (product != null)
                {
                    products.Add(product);
                }
            }

            var rootIds = products
                .SelectMany(product => product.BacklogRootWorkItemIds)
                .Distinct()
                .ToArray();

            workItems = rootIds.Length == 0
                ? Array.Empty<WorkItemDto>()
                : await _mediator.Send(new GetWorkItemsByRootIdsQuery(rootIds), cancellationToken);
        }
        else if (!effectiveFilter.Context.AreaPaths.IsAll)
        {
            workItems = await _workItemReadProvider.GetByAreaPathsAsync(effectiveFilter.Context.AreaPaths.Values.ToList(), cancellationToken);
        }
        else
        {
            var products = (await _productRepository.GetAllProductsAsync(cancellationToken)).ToList();
            var rootIds = products
                .SelectMany(product => product.BacklogRootWorkItemIds)
                .Distinct()
                .ToArray();

            workItems = rootIds.Length == 0
                ? await _workItemReadProvider.GetAllAsync(cancellationToken)
                : await _mediator.Send(new GetWorkItemsByRootIdsQuery(rootIds), cancellationToken);
        }

        if (!effectiveFilter.Context.AreaPaths.IsAll)
        {
            var areaPaths = effectiveFilter.Context.AreaPaths.Values;
            workItems = workItems
                .Where(workItem => areaPaths.Any(areaPath =>
                    workItem.AreaPath.StartsWith(areaPath, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return workItems.ToList();
    }
}

internal sealed class RepositoryBackedWorkItemReadProvider : IWorkItemReadProvider
{
    private readonly IWorkItemRepository _repository;

    public RepositoryBackedWorkItemReadProvider(IWorkItemRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
        => _repository.GetFilteredAsync(filter, cancellationToken);

    public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
        => _repository.GetByAreaPathsAsync(areaPaths, cancellationToken);

    public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
        => _repository.GetByTfsIdAsync(tfsId, cancellationToken);

    public Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
        => GetByRootIdsInternalAsync(rootWorkItemIds, cancellationToken);

    private async Task<IEnumerable<WorkItemDto>> GetByRootIdsInternalAsync(int[] rootWorkItemIds, CancellationToken cancellationToken)
    {
        var allWorkItems = (await _repository.GetAllAsync(cancellationToken)).ToList();
        var includedIds = new HashSet<int>(rootWorkItemIds ?? Array.Empty<int>());

        bool changed;
        do
        {
            changed = false;
            foreach (var workItem in allWorkItems)
            {
                if (workItem.ParentTfsId.HasValue
                    && includedIds.Contains(workItem.ParentTfsId.Value)
                    && includedIds.Add(workItem.TfsId))
                {
                    changed = true;
                }
            }
        } while (changed);

        return allWorkItems.Where(workItem => includedIds.Contains(workItem.TfsId)).ToList();
    }
}
