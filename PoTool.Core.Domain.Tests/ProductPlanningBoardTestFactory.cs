using PoTool.Core.Contracts;
using PoTool.Core.Planning;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Domain.Tests;

internal static class ProductPlanningBoardTestFactory
{
    internal static ProductPlanningBoardService CreateService(params WorkItemDto[] workItems)
    {
        return CreateService(new InMemoryProductPlanningSessionStore(), workItems);
    }

    internal static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        params WorkItemDto[] workItems)
    {
        return new ProductPlanningBoardService(
            new FakeProductRepository(CreateProduct(7, "Roadmap Product", 100)),
            new FakeWorkItemReadProvider(workItems),
            sessionStore);
    }

    internal static ProductPlanningBoardService CreateService(
        InMemoryProductPlanningSessionStore sessionStore,
        IReadOnlyList<ProductDto> products,
        IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>> workItemsByRootId)
    {
        return new ProductPlanningBoardService(
            new FakeProductRepository(products.ToArray()),
            new FakeWorkItemReadProvider(workItemsByRootId),
            sessionStore);
    }

    internal static ProductDto CreateProduct(int productId, string name, params int[] rootIds)
    {
        return new ProductDto(
            productId,
            null,
            name,
            rootIds.ToList(),
            0,
            ProductPictureType.Default,
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            [],
            []);
    }

    internal static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string title,
        int? parentId,
        double? backlogPriority,
        string? tags)
    {
        return new WorkItemDto(
            tfsId,
            type,
            title,
            parentId,
            "Area",
            "Iteration",
            "Active",
            DateTimeOffset.UtcNow,
            null,
            null,
            BacklogPriority: backlogPriority,
            Tags: tags);
    }

    internal sealed class FakeProductRepository : IProductRepository
    {
        private readonly Dictionary<int, ProductDto> _products;

        public FakeProductRepository(params ProductDto[] products)
        {
            _products = products.ToDictionary(static product => product.Id);
        }

        public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_products.GetValueOrDefault(id));
        }

        public Task<IEnumerable<ProductDto>> GetProductsByOwnerAsync(int productOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> CreateProductAsync(int? productOwnerId, string name, List<int> backlogRootWorkItemIds, ProductPictureType pictureType, int defaultPictureId, string? customPicturePath, EstimationMode estimationMode = EstimationMode.StoryPoints, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> UpdateProductAsync(int id, string name, List<int> backlogRootWorkItemIds, ProductPictureType? pictureType, int? defaultPictureId, string? customPicturePath, EstimationMode? estimationMode = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDto> ChangeProductOwnerAsync(int productId, int? newProductOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> GetSelectableProductsAsync(int productOwnerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<ProductDto>> ReorderProductsAsync(int productOwnerId, List<int> productIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> LinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UnlinkTeamAsync(int productId, int teamId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FakeWorkItemReadProvider : IWorkItemReadProvider
    {
        private readonly IReadOnlyList<WorkItemDto>? _workItems;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>>? _workItemsByRootId;

        public FakeWorkItemReadProvider(params WorkItemDto[] workItems)
        {
            _workItems = workItems;
        }

        public FakeWorkItemReadProvider(IReadOnlyDictionary<int, IReadOnlyList<WorkItemDto>> workItemsByRootId)
        {
            _workItemsByRootId = workItemsByRootId;
        }

        public Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
        {
            if (_workItemsByRootId is not null)
            {
                var workItems = rootWorkItemIds
                    .Distinct()
                    .OrderBy(static rootId => rootId)
                    .SelectMany(rootId => _workItemsByRootId.GetValueOrDefault(rootId, Array.Empty<WorkItemDto>()))
                    .ToArray();

                return Task.FromResult<IEnumerable<WorkItemDto>>(workItems);
            }

            return Task.FromResult<IEnumerable<WorkItemDto>>(_workItems ?? Array.Empty<WorkItemDto>());
        }

        public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
