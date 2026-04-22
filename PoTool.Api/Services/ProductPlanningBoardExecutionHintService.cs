using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;
using PoTool.Shared.Planning;

namespace PoTool.Api.Services;

public interface IProductPlanningBoardExecutionHintService
{
    Task<ProductPlanningBoardDto> ApplyExecutionHintAsync(
        ProductPlanningBoardDto board,
        int? requestedTeamId,
        int? requestedSprintId,
        CancellationToken cancellationToken = default);
}

public sealed class ProductPlanningBoardExecutionHintService : IProductPlanningBoardExecutionHintService
{
    private readonly IProductRepository _productRepository;
    private readonly ISprintRepository _sprintRepository;
    private readonly IExecutionRealityCheckInterpretationLayerService _interpretationLayerService;

    public ProductPlanningBoardExecutionHintService(
        IProductRepository productRepository,
        ISprintRepository sprintRepository,
        IExecutionRealityCheckInterpretationLayerService interpretationLayerService)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _sprintRepository = sprintRepository ?? throw new ArgumentNullException(nameof(sprintRepository));
        _interpretationLayerService = interpretationLayerService ?? throw new ArgumentNullException(nameof(interpretationLayerService));
    }

    public async Task<ProductPlanningBoardDto> ApplyExecutionHintAsync(
        ProductPlanningBoardDto board,
        int? requestedTeamId,
        int? requestedSprintId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);

        var product = await _productRepository.GetProductByIdAsync(board.ProductId, cancellationToken);
        if (product is null || !product.ProductOwnerId.HasValue || product.TeamIds.Count == 0)
        {
            return board with { ExecutionHint = null };
        }

        var productOwnerId = product.ProductOwnerId.Value;

        var teamId = ResolveTeamId(product.TeamIds, requestedTeamId);
        if (!teamId.HasValue)
        {
            return board with { ExecutionHint = null };
        }

        var sprintId = await ResolveSprintIdAsync(teamId.Value, requestedSprintId, cancellationToken);
        if (!sprintId.HasValue)
        {
            return board with { ExecutionHint = null };
        }

        var interpretation = await _interpretationLayerService.BuildAsync(
            productOwnerId,
            sprintId.Value,
            [board.ProductId],
            cancellationToken);

        return board with
        {
            ExecutionHint = BuildHint(interpretation, teamId.Value, sprintId.Value)
        };
    }

    private static int? ResolveTeamId(IReadOnlyList<int> teamIds, int? requestedTeamId)
    {
        if (requestedTeamId.HasValue && teamIds.Contains(requestedTeamId.Value))
        {
            return requestedTeamId.Value;
        }

        return teamIds.Count > 0 ? teamIds[0] : null;
    }

    private async Task<int?> ResolveSprintIdAsync(int teamId, int? requestedSprintId, CancellationToken cancellationToken)
    {
        var teamSprints = (await _sprintRepository.GetSprintsForTeamAsync(teamId, cancellationToken))
            .OrderBy(static sprint => sprint.StartUtc?.UtcDateTime ?? DateTime.MinValue)
            .ThenBy(static sprint => sprint.Id)
            .ToArray();

        if (requestedSprintId.HasValue && teamSprints.Any(sprint => sprint.Id == requestedSprintId.Value))
        {
            return requestedSprintId.Value;
        }

        var currentSprint = await _sprintRepository.GetCurrentSprintForTeamAsync(teamId, cancellationToken);
        if (currentSprint is not null)
        {
            return currentSprint.Id;
        }

        return teamSprints.LastOrDefault()?.Id;
    }

    private static ProductPlanningExecutionHintDto? BuildHint(
        ExecutionRealityCheckInterpretation interpretation,
        int teamId,
        int sprintId)
    {
        if (interpretation.OverallState != ExecutionRealityCheckOverallState.Watch
            && interpretation.OverallState != ExecutionRealityCheckOverallState.Investigate)
        {
            return null;
        }

        var anomaly = interpretation.Anomalies
            .Where(static candidate =>
                candidate.Status == ExecutionRealityCheckAnomalyStatus.Weak
                || candidate.Status == ExecutionRealityCheckAnomalyStatus.Strong)
            .OrderByDescending(candidate => GetStatusPriority(candidate.Status))
            .ThenBy(candidate => GetAnomalyPriority(candidate.AnomalyKey))
            .FirstOrDefault();

        if (anomaly is null)
        {
            return null;
        }

        return anomaly.AnomalyKey switch
        {
            ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey => new ProductPlanningExecutionHintDto(
                anomaly.AnomalyKey,
                "Execution signal: committed work was not delivered as expected (recent sprint)",
                "Recent sprint delivered less than committed. Open Sprint Execution to see unfinished work.",
                teamId,
                sprintId),
            ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey => new ProductPlanningExecutionHintDto(
                anomaly.AnomalyKey,
                "Execution signal: delivery was less steady than expected (recent sprints)",
                "Recent sprints delivered unevenly. Open Delivery Trends to see when this changed.",
                teamId,
                sprintId),
            ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey => new ProductPlanningExecutionHintDto(
                anomaly.AnomalyKey,
                "Execution signal: committed work kept carrying into the next sprint (recent sprint)",
                "Recent sprint carried more committed work forward. Open Sprint Execution to see unfinished work.",
                teamId,
                sprintId),
            _ => null
        };
    }

    private static int GetStatusPriority(ExecutionRealityCheckAnomalyStatus status)
    {
        return status switch
        {
            ExecutionRealityCheckAnomalyStatus.Strong => 2,
            ExecutionRealityCheckAnomalyStatus.Weak => 1,
            _ => 0
        };
    }

    private static int GetAnomalyPriority(string anomalyKey)
    {
        return anomalyKey switch
        {
            ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey => 0,
            ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey => 1,
            ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey => 2,
            _ => int.MaxValue
        };
    }
}
