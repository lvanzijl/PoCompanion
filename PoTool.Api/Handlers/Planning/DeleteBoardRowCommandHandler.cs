using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Commands;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for DeleteBoardRowCommand.
/// Deletes a row from the Planning Board if it has no epic placements.
/// </summary>
public sealed class DeleteBoardRowCommandHandler : ICommandHandler<DeleteBoardRowCommand, RowOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<DeleteBoardRowCommandHandler> _logger;

    public DeleteBoardRowCommandHandler(
        PoToolDbContext dbContext,
        ILogger<DeleteBoardRowCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<RowOperationResultDto> Handle(
        DeleteBoardRowCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Deleting board row {RowId}", command.RowId);

        var row = await _dbContext.BoardRows
            .Include(r => r.Placements)
            .FirstOrDefaultAsync(r => r.Id == command.RowId, cancellationToken);

        if (row == null)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Row {command.RowId} not found"
            };
        }

        // Check if row has any placements
        if (row.Placements.Count > 0)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = "Cannot delete row with epic placements. Remove epics first."
            };
        }

        var deletedOrder = row.DisplayOrder;

        _dbContext.BoardRows.Remove(row);

        // Shift remaining rows
        var rowsToShift = await _dbContext.BoardRows
            .Where(r => r.DisplayOrder > deletedOrder)
            .ToListAsync(cancellationToken);

        foreach (var r in rowsToShift)
        {
            r.DisplayOrder--;
            r.LastModified = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted board row {RowId}", command.RowId);

        return new RowOperationResultDto
        {
            Success = true,
            RowId = command.RowId
        };
    }
}
