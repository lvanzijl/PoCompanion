using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;
using static PoTool.Core.Domain.Tests.ProductPlanningBoardTestFactory;

namespace PoTool.Api.Tests;

[TestClass]
public sealed class ProductPlanningBoardExecutionHintServiceTests
{
    [TestMethod]
    public async Task ApplyExecutionHintAsync_DoesNotSurfaceHint_WhenOverallStateIsStable()
    {
        var service = CreateService(
            CreateExecutionProduct(),
            CreateDefaultSprintsByTeam([10]),
            new ExecutionRealityCheckInterpretation(
                [],
                ExecutionRealityCheckOverallState.Stable,
                TotalSeverity: 0));

        var result = await service.ApplyExecutionHintAsync(CreateBoard(), requestedTeamId: 10, requestedSprintId: 1, CancellationToken.None);

        Assert.IsNull(result.ExecutionHint);
    }

    [TestMethod]
    public async Task ApplyExecutionHintAsync_PrefersStrongAnomalyOverWeakAndMapsExactText()
    {
        var service = CreateService(
            CreateExecutionProduct(),
            CreateDefaultSprintsByTeam([10]),
            new ExecutionRealityCheckInterpretation(
                [
                    new ExecutionRealityCheckAnomalyInterpretation(
                        ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey,
                        ExecutionRealityCheckAnomalyStatus.Weak,
                        3),
                    new ExecutionRealityCheckAnomalyInterpretation(
                        ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey,
                        ExecutionRealityCheckAnomalyStatus.Strong,
                        4)
                ],
                ExecutionRealityCheckOverallState.Investigate,
                TotalSeverity: 3));

        var result = await service.ApplyExecutionHintAsync(CreateBoard(), requestedTeamId: 10, requestedSprintId: 1, CancellationToken.None);

        Assert.IsNotNull(result.ExecutionHint);
        Assert.AreEqual("completion-below-typical", result.ExecutionHint.AnomalyKey);
        Assert.AreEqual("Execution signal: committed delivery below typical range", result.ExecutionHint.Message);
        Assert.AreEqual(10, result.ExecutionHint.TeamId);
        Assert.AreEqual(1, result.ExecutionHint.SprintId);
    }

    [TestMethod]
    public async Task ApplyExecutionHintAsync_UsesPhase26TieBreakOrderForEqualSeverity()
    {
        var service = CreateService(
            CreateExecutionProduct(),
            CreateDefaultSprintsByTeam([10]),
            new ExecutionRealityCheckInterpretation(
                [
                    new ExecutionRealityCheckAnomalyInterpretation(
                        ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey,
                        ExecutionRealityCheckAnomalyStatus.Weak,
                        3),
                    new ExecutionRealityCheckAnomalyInterpretation(
                        ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey,
                        ExecutionRealityCheckAnomalyStatus.Weak,
                        3),
                    new ExecutionRealityCheckAnomalyInterpretation(
                        ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey,
                        ExecutionRealityCheckAnomalyStatus.Weak,
                        3)
                ],
                ExecutionRealityCheckOverallState.Investigate,
                TotalSeverity: 3));

        var result = await service.ApplyExecutionHintAsync(CreateBoard(), requestedTeamId: 10, requestedSprintId: 1, CancellationToken.None);

        Assert.IsNotNull(result.ExecutionHint);
        Assert.AreEqual("spillover-increase", result.ExecutionHint.AnomalyKey);
        Assert.AreEqual("Execution signal: direct spillover increasing", result.ExecutionHint.Message);
    }

    [TestMethod]
    public async Task ApplyExecutionHintAsync_FallsBackToProductTeamAndCurrentSprint_WhenRequestedContextIsInvalid()
    {
        var sprintsByTeam = CreateDefaultSprintsByTeam([10]);
        var service = CreateService(
            CreateExecutionProduct(),
            sprintsByTeam,
            new ExecutionRealityCheckInterpretation(
                [
                    new ExecutionRealityCheckAnomalyInterpretation(
                        ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey,
                        ExecutionRealityCheckAnomalyStatus.Weak,
                        3)
                ],
                ExecutionRealityCheckOverallState.Watch,
                TotalSeverity: 1));

        var result = await service.ApplyExecutionHintAsync(CreateBoard(), requestedTeamId: 999, requestedSprintId: 999, CancellationToken.None);

        Assert.IsNotNull(result.ExecutionHint);
        Assert.AreEqual(10, result.ExecutionHint.TeamId);
        Assert.AreEqual(sprintsByTeam[10][0].Id, result.ExecutionHint.SprintId);
    }

    private static ProductPlanningBoardExecutionHintService CreateService(
        ProductDto product,
        IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> sprintsByTeam,
        ExecutionRealityCheckInterpretation interpretation)
    {
        return new ProductPlanningBoardExecutionHintService(
            new StubProductRepository(product),
            new StubSprintRepository(sprintsByTeam),
            new StubInterpretationLayerService(interpretation));
    }

    private static ProductDto CreateExecutionProduct()
        => new(
            7,
            1,
            "Roadmap Product",
            [100],
            0,
            ProductPictureType.Default,
            0,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            [10],
            []);

    private static ProductPlanningBoardDto CreateBoard()
        => new(
            7,
            "Roadmap Product",
            [],
            [],
            [],
            [],
            []);

    private sealed class StubProductRepository : IProductRepository
    {
        private readonly ProductDto _product;

        public StubProductRepository(ProductDto product)
        {
            _product = product;
        }

        public Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

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

    private sealed class StubSprintRepository : ISprintRepository
    {
        private readonly IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> _sprintsByTeam;

        public StubSprintRepository(IReadOnlyDictionary<int, IReadOnlyList<SprintDto>> sprintsByTeam)
        {
            _sprintsByTeam = sprintsByTeam;
        }

        public Task<IEnumerable<SprintDto>> GetAllSprintsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SprintDto>>(_sprintsByTeam.Values.SelectMany(static sprints => sprints).ToArray());

        public Task<IEnumerable<SprintDto>> GetSprintsForTeamAsync(int teamId, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<SprintDto>>(_sprintsByTeam.GetValueOrDefault(teamId, Array.Empty<SprintDto>()));

        public Task<SprintDto?> GetCurrentSprintForTeamAsync(int teamId, CancellationToken cancellationToken = default)
            => Task.FromResult(_sprintsByTeam.GetValueOrDefault(teamId, Array.Empty<SprintDto>()).FirstOrDefault());

        public Task UpsertSprintsForTeamAsync(int teamId, IEnumerable<TeamIterationDto> iterations, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveStaleSprintsAsync(int teamId, DateTimeOffset olderThan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubInterpretationLayerService : IExecutionRealityCheckInterpretationLayerService
    {
        private readonly ExecutionRealityCheckInterpretation _interpretation;

        public StubInterpretationLayerService(ExecutionRealityCheckInterpretation interpretation)
        {
            _interpretation = interpretation;
        }

        public Task<ExecutionRealityCheckInterpretation> BuildAsync(
            int productOwnerId,
            int anchorSprintId,
            IReadOnlyList<int>? effectiveProductIds = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_interpretation);
    }
}
