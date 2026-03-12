using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services.Configuration;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Commands;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for application settings.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ExportConfigurationService _exportConfigurationService;
    private readonly ImportConfigurationService _importConfigurationService;

    public SettingsController(
        IMediator mediator,
        ExportConfigurationService exportConfigurationService,
        ImportConfigurationService importConfigurationService)
    {
        _mediator = mediator;
        _exportConfigurationService = exportConfigurationService;
        _importConfigurationService = importConfigurationService;
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);

        if (settings == null)
        {
            return NotFound();
        }

        return Ok(settings);
    }

    /// <summary>
    /// Gets effort estimation settings.
    /// </summary>
    [HttpGet("effort-estimation")]
    [ProducesResponseType(typeof(EffortEstimationSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EffortEstimationSettingsDto>> GetEffortEstimationSettings(CancellationToken cancellationToken)
    {
        var settings = await _mediator.Send(new GetEffortEstimationSettingsQuery(), cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Updates effort estimation settings.
    /// </summary>
    [HttpPut("effort-estimation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateEffortEstimationSettings(
        [FromBody] EffortEstimationSettingsDto settings,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEffortEstimationSettingsCommand(settings);
        await _mediator.Send(command, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Gets work item type definitions from TFS.
    /// Returns the available work item types and their valid states.
    /// </summary>
    [HttpGet("workitem-type-definitions")]
    [ProducesResponseType(typeof(IEnumerable<WorkItemTypeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkItemTypeDefinitionDto>>> GetWorkItemTypeDefinitions(
        CancellationToken cancellationToken)
    {
        var definitions = await _mediator.Send(new GetWorkItemTypeDefinitionsQuery(), cancellationToken);
        return Ok(definitions);
    }

    /// <summary>
    /// Gets work item state classifications for the configured TFS project.
    /// </summary>
    [HttpGet("state-classifications")]
    [ProducesResponseType(typeof(GetStateClassificationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetStateClassificationsResponse>> GetStateClassifications(
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetStateClassificationsQuery(), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Gets the user-visible release notes from the canonical docs/release-notes.json source.
    /// </summary>
    [HttpGet("release-notes")]
    [ProducesResponseType(typeof(IReadOnlyList<ReleaseNoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReleaseNoteDto>>> GetReleaseNotes(CancellationToken cancellationToken)
    {
        var releaseNotes = await _mediator.Send(new GetReleaseNotesQuery(), cancellationToken);
        return Ok(releaseNotes);
    }

    [HttpGet("configuration-export")]
    [ProducesResponseType(typeof(ConfigurationExportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigurationExportDto>> ExportConfiguration(CancellationToken cancellationToken)
    {
        var export = await _exportConfigurationService.ExportAsync(cancellationToken);
        return Ok(export);
    }

    [HttpPost("configuration-import/validate")]
    [ProducesResponseType(typeof(ConfigurationImportResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigurationImportResultDto>> ValidateConfigurationImport(
        [FromBody] ConfigurationImportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _importConfigurationService.ImportAsync(request.JsonContent, validateOnly: true, cancellationToken);
        return Ok(result);
    }

    [HttpPost("configuration-import")]
    [ProducesResponseType(typeof(ConfigurationImportResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigurationImportResultDto>> ImportConfiguration(
        [FromBody] ConfigurationImportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _importConfigurationService.ImportAsync(request.JsonContent, request.ValidateOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Saves work item state classifications for the configured TFS project.
    /// </summary>
    [HttpPost("state-classifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SaveStateClassifications(
        [FromBody] SaveStateClassificationsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SaveStateClassificationsCommand(request);
        var success = await _mediator.Send(command, cancellationToken);

        if (!success)
        {
            return BadRequest("Failed to save state classifications");
        }

        return Ok();
    }
}
