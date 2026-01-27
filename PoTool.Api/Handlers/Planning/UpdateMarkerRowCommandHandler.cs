using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Planning.Commands;
using PoTool.Shared.Planning;

namespace PoTool.Api.Handlers.Planning;

/// <summary>
/// Handler for UpdateMarkerRowCommand.
/// Updates the label of a marker row (Iteration or Release line) on the Planning Board.
/// </summary>
public sealed class UpdateMarkerRowCommandHandler : ICommandHandler<UpdateMarkerRowCommand, RowOperationResultDto>
{
    private readonly PoToolDbContext _dbContext;
    private readonly ILogger<UpdateMarkerRowCommandHandler> _logger;

    public UpdateMarkerRowCommandHandler(
        PoToolDbContext dbContext,
        ILogger<UpdateMarkerRowCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async ValueTask<RowOperationResultDto> Handle(
        UpdateMarkerRowCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Updating marker row {RowId} with label '{Label}'", 
            command.RowId, command.Label);

        var row = await _dbContext.BoardRows
            .FirstOrDefaultAsync(r => r.Id == command.RowId, cancellationToken);

        if (row == null)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Row {command.RowId} not found"
            };
        }

        if (row.RowType != Persistence.Entities.BoardRowType.Marker)
        {
            return new RowOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Row {command.RowId} is not a marker row"
            };
        }

        row.MarkerLabel = command.Label;
        row.LastModified = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated marker row {RowId} with label '{Label}'", 
            command.RowId, command.Label);

        return new RowOperationResultDto
        {
            Success = true,
            RowId = row.Id
        };
    }
}
