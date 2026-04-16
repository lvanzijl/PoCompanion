using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Configuration;

namespace PoTool.Api.Controllers;

/// <summary>
/// Controller for managing data source mode (Live vs Cache).
/// </summary>
[ApiController]
[Route("api/datasource")]
[DataSourceMode(RouteIntent.LiveAllowed)]
public sealed class DataSourceModeController : ControllerBase
{
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly ILogger<DataSourceModeController> _logger;

    public DataSourceModeController(
        IDataSourceModeProvider modeProvider,
        ILogger<DataSourceModeController> logger)
    {
        _modeProvider = modeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current data source mode for a product owner.
    /// </summary>
    /// <param name="productOwnerId">The product owner ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current data source mode.</returns>
    [HttpGet("{productOwnerId:int}")]
    [ProducesResponseType(typeof(DataSourceModeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataSourceModeResponse>> GetMode(
        int productOwnerId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting data source mode for ProductOwner {ProductOwnerId}", productOwnerId);

        var mode = await _modeProvider.GetModeAsync(productOwnerId, cancellationToken);
        
        return Ok(new DataSourceModeResponse
        {
            Mode = mode.ToString(),
            IsCache = mode == DataSourceMode.Cache
        });
    }

    /// <summary>
    /// Sets the data source mode for a product owner.
    /// </summary>
    /// <param name="productOwnerId">The product owner ID.</param>
    /// <param name="request">The mode to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{productOwnerId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetMode(
        int productOwnerId,
        [FromBody] SetDataSourceModeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting data source mode to {Mode} for ProductOwner {ProductOwnerId}", 
            request.Mode, productOwnerId);

        if (!Enum.TryParse<DataSourceMode>(request.Mode, ignoreCase: true, out var mode))
        {
            return BadRequest($"Invalid mode: {request.Mode}. Valid values are: Live, Cache");
        }

        try
        {
            await _modeProvider.SetModeAsync(productOwnerId, mode, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

/// <summary>
/// Response containing the current data source mode.
/// </summary>
public sealed class DataSourceModeResponse
{
    /// <summary>
    /// The mode as a string (Live or Cache).
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>
    /// True if the mode is Cache.
    /// </summary>
    public required bool IsCache { get; init; }
}

/// <summary>
/// Request to set the data source mode.
/// </summary>
public sealed class SetDataSourceModeRequest
{
    /// <summary>
    /// The mode to set (Live or Cache).
    /// </summary>
    public required string Mode { get; init; }
}
