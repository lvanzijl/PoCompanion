using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Commands;
using PlanningDtos = PoTool.Shared.Planning;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for DeletePlanningEpicPlacementsCommand.
/// Deletes epic placements from the board, returning them to the unplanned list.
/// </summary>
public sealed class DeletePlanningEpicPlacementsCommandHandler : ICommandHandler<DeletePlanningEpicPlacementsCommand, PlacementOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<DeletePlanningEpicPlacementsCommandHandler> _logger;

    public DeletePlanningEpicPlacementsCommandHandler(
        PoToolDbContext dbContext,
        ILogger<DeletePlanningEpicPlacementsCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<PlacementOperationResultDto> Handle(
        DeletePlanningEpicPlacementsCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Deleting {Count} epic placements", command.PlacementIds.Count);

        if (command.PlacementIds.Count == 0)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = "No placement IDs provided"
            };
        }

        var placements = await _dbContext.PlanningEpicPlacements
            .Where(p => command.PlacementIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (placements.Count == 0)
        {
            return new PlacementOperationResultDto
            {
                Success = false,
                ErrorMessage = "No matching placements found"
            };
        }

        _dbContext.PlanningEpicPlacements.RemoveRange(placements);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} epic placements", placements.Count);

        return new PlacementOperationResultDto
        {
            Success = true
        };
    }
}
