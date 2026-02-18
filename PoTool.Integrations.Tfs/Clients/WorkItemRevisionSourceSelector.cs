using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

public sealed class WorkItemRevisionSourceSelector : IWorkItemRevisionSourceSelector
{
    private readonly ITfsConfigurationService _configService;
    private readonly IProductOwnerRevisionSourceOverrideProvider _overrideProvider;
    private readonly IEnumerable<IWorkItemRevisionSource> _sources;
    private readonly ILogger<WorkItemRevisionSourceSelector> _logger;

    public WorkItemRevisionSourceSelector(
        ITfsConfigurationService configService,
        IProductOwnerRevisionSourceOverrideProvider overrideProvider,
        IEnumerable<IWorkItemRevisionSource> sources,
        ILogger<WorkItemRevisionSourceSelector> logger)
    {
        _configService = configService;
        _overrideProvider = overrideProvider;
        _sources = sources;
        _logger = logger;
    }

    public async Task<IWorkItemRevisionSource> GetSourceAsync(
        int? productOwnerId = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetConfigEntityAsync(cancellationToken);
        var configuredSource = config?.RevisionSource ?? RevisionSource.RestReportingRevisions;
        if (productOwnerId.HasValue)
        {
            var overrideSource = await _overrideProvider.GetOverrideAsync(productOwnerId.Value, cancellationToken);
            configuredSource = overrideSource ?? configuredSource;
        }

        var source = _sources.FirstOrDefault(candidate => candidate.SourceType == configuredSource)
                     ?? _sources.First(candidate => candidate.SourceType == RevisionSource.RestReportingRevisions);

        _logger.LogInformation(
            "Using revision source {RevisionSource} for ProductOwnerId={ProductOwnerId}",
            source.SourceType,
            productOwnerId);
        return source;
    }
}
