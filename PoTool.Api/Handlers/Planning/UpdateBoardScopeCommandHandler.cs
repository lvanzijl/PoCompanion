using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Planning.Commands;
using PlanningDtos = PoTool.Shared.Planning;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for UpdateBoardScopeCommand.
/// Updates the Planning Board scope settings.
/// </summary>
public sealed class UpdateBoardScopeCommandHandler : ICommandHandler<UpdateBoardScopeCommand, BoardOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<UpdateBoardScopeCommandHandler> _logger;

    public UpdateBoardScopeCommandHandler(
        PoToolDbContext dbContext,
        ILogger<UpdateBoardScopeCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<BoardOperationResultDto> Handle(
        UpdateBoardScopeCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Updating board scope for Product Owner {ProductOwnerId} to {Scope}", 
            command.ProductOwnerId, command.Scope);

        var settings = await _dbContext.PlanningBoardSettings
            .FirstOrDefaultAsync(s => s.ProductOwnerId == command.ProductOwnerId, cancellationToken);

        if (settings == null)
        {
            settings = new PlanningBoardSettingsEntity
            {
                ProductOwnerId = command.ProductOwnerId,
                Scope = (PlanningBoardScope)(int)command.Scope,
                SelectedProductId = command.SelectedProductId,
                LastModified = DateTimeOffset.UtcNow
            };
            _dbContext.PlanningBoardSettings.Add(settings);
        }
        else
        {
            settings.Scope = (PlanningBoardScope)(int)command.Scope;
            settings.SelectedProductId = command.SelectedProductId;
            settings.LastModified = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated board scope for Product Owner {ProductOwnerId} to {Scope}", 
            command.ProductOwnerId, command.Scope);

        return new BoardOperationResultDto
        {
            Success = true
        };
    }
}
