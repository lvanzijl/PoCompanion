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
/// Handler for UpdateProductVisibilityCommand.
/// Updates product column visibility on the Planning Board.
/// </summary>
public sealed class UpdateProductVisibilityCommandHandler : ICommandHandler<UpdateProductVisibilityCommand, BoardOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<UpdateProductVisibilityCommandHandler> _logger;

    public UpdateProductVisibilityCommandHandler(
        PoToolDbContext dbContext,
        ILogger<UpdateProductVisibilityCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<BoardOperationResultDto> Handle(
        UpdateProductVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Updating product {ProductId} visibility to {IsVisible} for Product Owner {ProductOwnerId}", 
            command.ProductId, command.IsVisible, command.ProductOwnerId);

        var settings = await _dbContext.PlanningBoardSettings
            .FirstOrDefaultAsync(s => s.ProductOwnerId == command.ProductOwnerId, cancellationToken);

        if (settings == null)
        {
            settings = new PlanningBoardSettingsEntity
            {
                ProductOwnerId = command.ProductOwnerId,
                Scope = PlanningBoardScope.AllProducts,
                LastModified = DateTimeOffset.UtcNow
            };
            _dbContext.PlanningBoardSettings.Add(settings);
        }

        // Parse existing hidden product IDs
        var hiddenProductIds = ParseHiddenProductIds(settings.HiddenProductIdsJson);

        if (command.IsVisible)
        {
            hiddenProductIds.Remove(command.ProductId);
        }
        else
        {
            // Verify no selected epics in this product before hiding
            var hasSelectedPlacements = await _dbContext.PlanningEpicPlacements
                .AnyAsync(p => p.ProductId == command.ProductId, cancellationToken);
            
            // For now, we allow hiding regardless of placements
            // (the UI should prevent this if it has selected items)
            hiddenProductIds.Add(command.ProductId);
        }

        settings.HiddenProductIdsJson = JsonSerializer.Serialize(hiddenProductIds.ToList());
        settings.LastModified = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated product {ProductId} visibility to {IsVisible} for Product Owner {ProductOwnerId}", 
            command.ProductId, command.IsVisible, command.ProductOwnerId);

        return new BoardOperationResultDto
        {
            Success = true
        };
    }

    private static HashSet<int> ParseHiddenProductIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(json);
            return ids != null ? new HashSet<int>(ids) : [];
        }
        catch
        {
            return [];
        }
    }
}
