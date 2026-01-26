using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Planning.Commands;
using PlanningDtos = PoTool.Shared.Planning;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for InitializeDefaultBoardCommand.
/// Creates the default board layout: 3 rows + Iteration line + 3 rows + Release line + 3 rows.
/// </summary>
public sealed class InitializeDefaultBoardCommandHandler : ICommandHandler<InitializeDefaultBoardCommand, BoardOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<InitializeDefaultBoardCommandHandler> _logger;

    public InitializeDefaultBoardCommandHandler(
        PoToolDbContext dbContext,
        ILogger<InitializeDefaultBoardCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<BoardOperationResultDto> Handle(
        InitializeDefaultBoardCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing default board for Product Owner {ProductOwnerId}", command.ProductOwnerId);

        // Check if board already has rows
        var existingRowCount = await _dbContext.BoardRows.CountAsync(cancellationToken);
        if (existingRowCount > 0)
        {
            return new BoardOperationResultDto
            {
                Success = false,
                ErrorMessage = "Board already initialized"
            };
        }

        // Create default layout: 3 rows + Iteration + 3 rows + Release + 3 rows
        var rows = new List<BoardRowEntity>
        {
            // First 3 normal rows
            new() { DisplayOrder = 0, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            new() { DisplayOrder = 1, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            new() { DisplayOrder = 2, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            
            // Iteration line
            new() { DisplayOrder = 3, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Marker, MarkerRowType = MarkerType.Iteration, MarkerLabel = "Iteration Line", CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            
            // Middle 3 normal rows
            new() { DisplayOrder = 4, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            new() { DisplayOrder = 5, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            new() { DisplayOrder = 6, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            
            // Release line
            new() { DisplayOrder = 7, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Marker, MarkerRowType = MarkerType.Release, MarkerLabel = "Release Line", CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            
            // Final 3 normal rows
            new() { DisplayOrder = 8, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            new() { DisplayOrder = 9, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow },
            new() { DisplayOrder = 10, RowType = PoTool.Api.Persistence.Entities.BoardRowType.Normal, CreatedAt = DateTimeOffset.UtcNow, LastModified = DateTimeOffset.UtcNow }
        };

        _dbContext.BoardRows.AddRange(rows);

        // Create default settings
        var settings = new PlanningBoardSettingsEntity
        {
            ProductOwnerId = command.ProductOwnerId,
            Scope = PlanningBoardScope.AllProducts,
            LastModified = DateTimeOffset.UtcNow
        };

        _dbContext.PlanningBoardSettings.Add(settings);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Initialized default board with {RowCount} rows for Product Owner {ProductOwnerId}", 
            rows.Count, command.ProductOwnerId);

        return new BoardOperationResultDto
        {
            Success = true
        };
    }
}
