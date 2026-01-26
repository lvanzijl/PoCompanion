using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Commands;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for MoveRowCommand.
/// Moves a row to a new position on the Planning Board.
/// </summary>
public sealed class MoveRowCommandHandler : ICommandHandler<MoveRowCommand, RowOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<MoveRowCommandHandler> _logger;

    public MoveRowCommandHandler(
        PoToolDbContext dbContext,
        ILogger<MoveRowCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<RowOperationResultDto> Handle(
        MoveRowCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Moving row {RowId} relative to {TargetRowId}", command.RowId, command.TargetRowId);

        // Get the row to move
        var rowToMove = await _dbContext.BoardRows
            .FirstOrDefaultAsync(r => r.Id == command.RowId, cancellationToken);

        if (rowToMove == null)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Row {command.RowId} not found"
            };
        }

        // Get the target row
        var targetRow = await _dbContext.BoardRows
            .FirstOrDefaultAsync(r => r.Id == command.TargetRowId, cancellationToken);

        if (targetRow == null)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Target row {command.TargetRowId} not found"
            };
        }

        // Cannot move a row to itself
        if (command.RowId == command.TargetRowId)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = "Cannot move row to its current position"
            };
        }

        var oldOrder = rowToMove.DisplayOrder;
        var newOrder = command.InsertBelow ? targetRow.DisplayOrder + 1 : targetRow.DisplayOrder;

        // If moving within the same position, no change needed
        if (oldOrder == newOrder || (command.InsertBelow && oldOrder == targetRow.DisplayOrder + 1))
        {
            return new RowOperationResultDto
            {
                Success = true,
                RowId = rowToMove.Id
            };
        }

        // Get all rows to reorder
        var allRows = await _dbContext.BoardRows
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken);

        // Remove the row from its current position
        allRows.Remove(rowToMove);

        // Find insertion point in the reordered list
        int insertIndex;
        if (command.InsertBelow)
        {
            insertIndex = allRows.FindIndex(r => r.Id == command.TargetRowId) + 1;
        }
        else
        {
            insertIndex = allRows.FindIndex(r => r.Id == command.TargetRowId);
        }

        // Clamp insertion index
        insertIndex = Math.Max(0, Math.Min(insertIndex, allRows.Count));

        // Insert at new position
        allRows.Insert(insertIndex, rowToMove);

        // Update display orders
        for (int i = 0; i < allRows.Count; i++)
        {
            allRows[i].DisplayOrder = i;
            allRows[i].LastModified = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Moved row {RowId} from position {OldOrder} to {NewOrder}", 
            rowToMove.Id, oldOrder, rowToMove.DisplayOrder);

        return new RowOperationResultDto
        {
            Success = true,
            RowId = rowToMove.Id
        };
    }
}
