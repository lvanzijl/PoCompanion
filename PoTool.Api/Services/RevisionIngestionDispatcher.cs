using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core.Configuration;

namespace PoTool.Api.Services;

/// <summary>
/// Dispatches revision ingestion to V1 or V2 based on the <see cref="RevisionIngestionV2Options.RevisionIngestionMode"/> configuration.
/// </summary>
public sealed class RevisionIngestionDispatcher : IRevisionIngestionService
{
    private readonly RevisionIngestionService _v1;
    private readonly RevisionIngestionServiceV2 _v2;
    private readonly IOptionsMonitor<RevisionIngestionV2Options> _options;
    private readonly ILogger<RevisionIngestionDispatcher> _logger;

    public RevisionIngestionDispatcher(
        RevisionIngestionService v1,
        RevisionIngestionServiceV2 v2,
        IOptionsMonitor<RevisionIngestionV2Options> options,
        ILogger<RevisionIngestionDispatcher> logger)
    {
        _v1 = v1;
        _v2 = v2;
        _options = options;
        _logger = logger;
    }

    public Task<RevisionIngestionResult> IngestRevisionsAsync(
        int productOwnerId,
        Action<RevisionIngestionProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var config = _options.CurrentValue;

        if (config.IsV2)
        {
            _logger.LogInformation(
                "REV_INGEST_DISPATCH mode=V2 productOwnerId={ProductOwnerId}",
                productOwnerId);
            return _v2.IngestRevisionsAsync(productOwnerId, progressCallback, cancellationToken);
        }

        _logger.LogInformation(
            "REV_INGEST_DISPATCH mode=V1 productOwnerId={ProductOwnerId}",
            productOwnerId);
        return _v1.IngestRevisionsAsync(productOwnerId, progressCallback, cancellationToken);
    }
}
