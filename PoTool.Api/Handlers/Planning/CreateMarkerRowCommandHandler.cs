using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Planning.Commands;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for CreateMarkerRowCommand.
/// Creates a marker row (Iteration or Release line) on the Planning Board.
/// </summary>
public sealed class CreateMarkerRowCommandHandler : ICommandHandler<CreateMarkerRowCommand, RowOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<CreateMarkerRowCommandHandler> _logger;

    public CreateMarkerRowCommandHandler(
        PoToolDbContext dbContext,
        ILogger<CreateMarkerRowCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<RowOperationResultDto> Handle(
        CreateMarkerRowCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Creating {MarkerType} marker row with label '{Label}'", 
            command.MarkerType, command.Label);

        int insertOrder;

        if (command.InsertBeforeRowId.HasValue)
        {
            var referenceRow = await _dbContext.BoardRows
                .FirstOrDefaultAsync(r => r.Id == command.InsertBeforeRowId.Value, cancellationToken);

            if (referenceRow == null)
            {
                return new RowOperationResultDto
                {
                    Success = false,
                    ErrorMessage = $"Reference row {command.InsertBeforeRowId.Value} not found"
                };
            }

            insertOrder = command.InsertBelow 
                ? referenceRow.DisplayOrder + 1 
                : referenceRow.DisplayOrder;

            // Shift existing rows
            var rowsToShift = await _dbContext.BoardRows
                .Where(r => r.DisplayOrder >= insertOrder)
                .ToListAsync(cancellationToken);

            foreach (var row in rowsToShift)
            {
                row.DisplayOrder++;
                row.LastModified = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            // Insert at end
            var maxOrder = await _dbContext.BoardRows
                .MaxAsync(r => (int?)r.DisplayOrder, cancellationToken) ?? -1;
            insertOrder = maxOrder + 1;
        }

        var newRow = new BoardRowEntity
        {
            DisplayOrder = insertOrder,
            RowType = PoTool.Api.Persistence.Entities.BoardRowType.Marker,
            MarkerRowType = (MarkerType)(int)command.MarkerType,
            MarkerLabel = command.Label,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        };

        _dbContext.BoardRows.Add(newRow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created {MarkerType} marker row with ID {RowId} at position {Position}", 
            command.MarkerType, newRow.Id, insertOrder);

        return new RowOperationResultDto
        {
            Success = true,
            RowId = newRow.Id
        };
    }
}
