using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Integrations.Tfs.Diagnostics;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation for work item revisions.
/// Uses the reporting work item revisions API for efficient bulk retrieval.
/// This client is separate from RealTfsClient to maintain strict separation of concerns.
/// Registered as scoped and not thread-safe for concurrent pagination; GetReportingRevisionsAsync serializes access.
/// Implements <see cref="IDisposable"/> to release the pagination semaphore.
/// </summary>
public class RealRevisionTfsClient : IRevisionTfsClient, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITfsConfigurationService _configService;
    private readonly ILogger<RealRevisionTfsClient> _logger;
    private readonly TfsRequestThrottler _throttler;
    private readonly TfsRequestSender _requestSender;
    private readonly RevisionIngestionDiagnostics? _diagnostics;
    private readonly IOptionsMonitor<RevisionIngestionPaginationOptions> _paginationOptions;

    // Token history is bounded by MaxTotalPages for a single pagination session.
    private readonly HashSet<string> _observedContinuationTokens = new(StringComparer.Ordinal);
    private int _totalPagesFetched;
    private int _emptyPages;
    private int _progressWithoutDataPages;
    private bool _paginationCompleted;
    // Guards pagination state to ensure GetReportingRevisionsAsync is single-threaded per client instance.
    private readonly SemaphoreSlim _paginationGate = new(1, 1);

    private const int MaxRetries = 3;
    private const int MaxPayloadLogLength = 2000; // Limit error logs to a readable size without losing essential context.
    private const string TruncationSuffix = "... (truncated)";
    private const int DefaultMaxParseWarningsPerPage = 10;
    private const int MaxValuePreviewLength = 64;
    private const string TokenHashHexFormat = "X8";

    /// <summary>
    /// Field whitelist for revision API — delegates to the single source of truth.
    /// </summary>
    private static readonly IReadOnlyList<string> FieldWhitelist = RevisionFieldWhitelist.Fields;

    public RealRevisionTfsClient(
        IHttpClientFactory httpClientFactory,
        ITfsConfigurationService configService,
        ILogger<RealRevisionTfsClient> logger,
        TfsRequestThrottler throttler,
        TfsRequestSender requestSender,
        IOptionsMonitor<RevisionIngestionPaginationOptions> paginationOptions,
        RevisionIngestionDiagnostics? diagnostics = null)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
        _throttler = throttler;
        _requestSender = requestSender;
        _paginationOptions = paginationOptions;
        _diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public async Task<ReportingRevisionsResult> GetReportingRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default)
    {
        await _paginationGate.WaitAsync(cancellationToken);
        try
        {
            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            ValidateTfsConfiguration(entity);
            var config = entity!;

            var httpClient = GetAuthenticatedHttpClient();

            var runContext = default(RevisionIngestionRunContext);
            var hasRunContext = _diagnostics?.TryGetCurrentRun(out runContext) == true;
            var logPerPageSummary = hasRunContext && runContext.LogPerPageSummary;

            var maxTokenHistory = Math.Max(1, _paginationOptions.CurrentValue.MaxTotalPages);
            var normalizedContinuationToken = NormalizeContinuationToken(continuationToken);
            EnsurePaginationState(normalizedContinuationToken, maxTokenHistory);

            var pagePayload = await FetchReportingRevisionsPageAsync(
                httpClient,
                config,
                startDateTime,
                normalizedContinuationToken,
                expandMode,
                logPerPageSummary,
                cancellationToken);

            _totalPagesFetched++;
            var pageIndex = _totalPagesFetched;
            LogEffortParseSummary(pagePayload.EffortParseSummary, pageIndex);

            if (pagePayload.PayloadError != null)
            {
                return CreateTerminationResult(
                    new ReportingRevisionsTermination(
                        ReportingRevisionsTerminationReason.MalformedPayload,
                        $"Reporting revisions payload malformed on page {pageIndex}: {pagePayload.PayloadError}"),
                    pagePayload,
                    pageIndex,
                    continuationToken,
                    tokenAdvanced: false,
                    skipReason: null,
                    logPerPageSummary,
                    runContext);
            }

            var result = new ReportingRevisionsResult(
                pagePayload.Revisions,
                pagePayload.ContinuationToken,
                termination: null,
                pagePayload.HttpStatusCode,
                pagePayload.HttpDurationMs,
                pagePayload.ParseDurationMs,
                pagePayload.TransformDurationMs);

            if (result.IsComplete)
            {
                return MarkPaginationComplete(result);
            }

            return result;
        }
        finally
        {
            _paginationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync("Relations", async () =>
        {
            // Per-item revisions endpoint: /_apis/wit/workItems/{id}/revisions
            // Request only fields (no relations) to avoid relation-derived ingestion.
            var url = BuildWorkItemRevisionsUrl(config, workItemId);

            _logger.LogDebug("Calling per-item revisions API for work item {WorkItemId}", workItemId);

            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var revisions = new List<WorkItemRevision>();

            if (!doc.RootElement.TryGetProperty("value", out var revisionsArray))
            {
                return revisions;
            }

            // Store previous revision fields for delta calculation
            Dictionary<string, string?>? previousFields = null;

            foreach (var revision in revisionsArray.EnumerateArray())
            {
                var workItemRevision = ParseWorkItemRevisionFromPerItem(revision, workItemId, previousFields);
                if (workItemRevision != null)
                {
                    revisions.Add(workItemRevision.Value.Revision);
                    previousFields = workItemRevision.Value.CurrentFields;
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} revisions for work item {WorkItemId}",
                revisions.Count, workItemId);

            return revisions;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                return false;
            }

            var httpClient = GetAuthenticatedHttpClient();
            var url = $"{entity.Url.TrimEnd('/')}/_apis/projects?api-version={entity.ApiVersion}&$top=1";

            var response = await SendGetAsync(httpClient, entity, url, cancellationToken, handleErrors: false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection validation failed");
            return false;
        }
    }

    private string BuildReportingRevisionsUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        string? continuationToken,
        ReportingExpandMode expandMode)
    {
        const string reportingEndpointPath = "/_apis/wit/reporting/workitemrevisions";

        if (expandMode != ReportingExpandMode.None && expandMode != ReportingExpandMode.Fields)
        {
            _logger.LogError(
                "Invalid reporting revisions expand mode {ExpandMode} for endpoint {EndpointPath}. Only None/Fields are allowed. Relations is not supported.",
                expandMode,
                reportingEndpointPath);

            throw new InvalidOperationException(
                $"Reporting endpoint {reportingEndpointPath} does not support expand mode '{expandMode}'. Relations is not supported. Only None/Fields are allowed.");
        }

        // Build URL: {collection}/_apis/wit/reporting/workitemrevisions
        var baseUrl = $"{config.Url.TrimEnd('/')}{reportingEndpointPath}";

        var queryParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["api-version"] = config.ApiVersion,
            ["fields"] = string.Join(",", FieldWhitelist)
        };

        // Parameter conflict validation: prefer continuation token over startDateTime
        // When a continuation token is present, the API ignores startDateTime
        if (!string.IsNullOrEmpty(continuationToken))
        {
            // Add continuation token for paging
            queryParams["continuationToken"] = continuationToken;
            // Do NOT add startDateTime when continuation token is present
        }
        else if (startDateTime.HasValue)
        {
            // Add startDateTime for incremental sync (only when no continuation token)
            // Uses invariant ISO 8601 UTC format, e.g. "2024-01-15T10:30:00.0000000Z"
            var serializedStartDateTime = ToTfsQueryDateTimeUtc(startDateTime.Value);
            ValidateSerializedStartDateTime(serializedStartDateTime);
            queryParams["startDateTime"] = serializedStartDateTime;
        }

        // Add expand parameter if requested
        // IMPORTANT: The reporting endpoint does NOT support $expand=relations
        // Only $expand=fields is allowed (for long text fields)
        if (expandMode == ReportingExpandMode.Fields)
        {
            queryParams["$expand"] = "fields";
        }

        var query = string.Join("&", queryParams.Select(param =>
            $"{param.Key}={Uri.EscapeDataString(param.Value)}"));

        return $"{baseUrl}?{query}";
    }

    private string BuildWorkItemRevisionsUrl(TfsConfigEntity config, int workItemId)
    {
        return $"{config.Url.TrimEnd('/')}/_apis/wit/workItems/{workItemId}/revisions?api-version={config.ApiVersion}&$expand=fields";
    }

    private async Task<ReportingRevisionsPagePayload> FetchReportingRevisionsPageAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        string? continuationToken,
        ReportingExpandMode expandMode,
        bool captureTimings,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithRetryAsync("Reporting", async () =>
        {
            string? serializedStartDateTime = null;
            if (startDateTime.HasValue && string.IsNullOrEmpty(continuationToken))
            {
                serializedStartDateTime = ToTfsQueryDateTimeUtc(startDateTime.Value);
                _logger.LogInformation(
                    "Reporting revisions startDateTime raw {StartDateTimeRaw} serialized {StartDateTimeSerialized}",
                    startDateTime.Value,
                    serializedStartDateTime);
            }

            var url = BuildReportingRevisionsUrl(config, startDateTime, continuationToken, expandMode);
            var logUrl = SanitizeUrlForLogging(url);
            var requestPathAndQuery = new Uri(url).PathAndQuery;

            _logger.LogDebug(
                "Calling reporting revisions API: {Url}. startDateTime={StartDateTimeSerialized}",
                logUrl,
                serializedStartDateTime ?? "<none>");
            _logger.LogDebug("Reporting revisions request path+query: {PathAndQuery}", requestPathAndQuery);

            var httpStart = captureTimings ? Stopwatch.GetTimestamp() : 0;
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
            await HandleHttpErrorsAsync(response, cancellationToken);

            long? httpDurationMs = captureTimings
                ? RevisionIngestionDiagnostics.GetElapsedMilliseconds(httpStart)
                : null;
            int? httpStatusCode = captureTimings ? (int)response.StatusCode : null;

            var parseStart = captureTimings ? Stopwatch.GetTimestamp() : 0;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            long? parseDurationMs = captureTimings
                ? RevisionIngestionDiagnostics.GetElapsedMilliseconds(parseStart)
                : null;

            var nextContinuationToken = ExtractContinuationToken(response);

            if (nextContinuationToken == null)
            {
                nextContinuationToken = ExtractContinuationTokenFromPayload(doc.RootElement);
            }

            var transformStart = captureTimings ? Stopwatch.GetTimestamp() : 0;
            var payloadError = (string?)null;
            IReadOnlyList<WorkItemRevision> revisions;
            EffortParseSummary effortParseSummary;

            try
            {
                var parseResult = ParseReportingRevisionsPayload(doc);
                revisions = parseResult.Revisions;
                effortParseSummary = parseResult.EffortParseSummary;
            }
            catch (TfsException ex) when (ex.StatusCode is null)
            {
                payloadError = ex.Message;
                _logger.LogError(ex, "Reporting revisions payload parsing failed");
                revisions = Array.Empty<WorkItemRevision>();
                effortParseSummary = EffortParseSummary.Empty;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                payloadError = ex.Message;
                _logger.LogError(ex, "Reporting revisions payload parsing failed with unexpected error");
                revisions = Array.Empty<WorkItemRevision>();
                effortParseSummary = EffortParseSummary.Empty;
            }

            long? transformDurationMs = captureTimings
                ? RevisionIngestionDiagnostics.GetElapsedMilliseconds(transformStart)
                : null;

            var hasMoreResults = nextContinuationToken is not null;
            _logger.LogInformation(
                "Retrieved {Count} revisions from reporting API. HasMoreResults: {HasMore}",
                revisions.Count,
                hasMoreResults);

            return new ReportingRevisionsPagePayload(
                revisions,
                nextContinuationToken,
                httpStatusCode,
                httpDurationMs,
                parseDurationMs,
                transformDurationMs,
                payloadError,
                effortParseSummary);
        }, cancellationToken);
    }

    private static string? ExtractContinuationToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("x-ms-continuationtoken", out var headerTokens))
        {
            return null;
        }

        return NormalizeContinuationToken(headerTokens.FirstOrDefault());
    }

    private static string? ExtractContinuationTokenFromPayload(JsonElement rootElement)
    {
        if (TryGetPropertyCaseInsensitive(rootElement, "continuationToken", out var tokenElement))
        {
            var token = tokenElement.ValueKind switch
            {
                JsonValueKind.String => tokenElement.GetString(),
                JsonValueKind.Number => tokenElement.GetRawText(),
                _ => null
            };

            var normalizedToken = NormalizeContinuationToken(token);
            if (normalizedToken != null)
            {
                return normalizedToken;
            }
        }

        if (TryGetPropertyCaseInsensitive(rootElement, "nextLink", out var nextLinkElement) &&
            nextLinkElement.ValueKind == JsonValueKind.String)
        {
            return ExtractContinuationTokenFromNextLink(nextLinkElement.GetString());
        }

        return null;
    }

    private static string? ExtractContinuationTokenFromNextLink(string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
        {
            return null;
        }

        string queryText;
        if (Uri.TryCreate(nextLink, UriKind.RelativeOrAbsolute, out var nextUri))
        {
            queryText = nextUri.IsAbsoluteUri
                ? nextUri.Query
                : ExtractQueryFromRelativeNextLink(nextLink);
        }
        else
        {
            queryText = ExtractQueryFromRelativeNextLink(nextLink);
        }

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return null;
        }

        var query = queryText.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in query)
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 &&
                pair[0].Equals("continuationToken", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeContinuationToken(Uri.UnescapeDataString(pair[1]));
            }
        }

        return null;
    }

    private static string ExtractQueryFromRelativeNextLink(string nextLink)
    {
        var queryIndex = nextLink.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0 || queryIndex + 1 >= nextLink.Length)
        {
            return string.Empty;
        }

        var fragmentIndex = nextLink.IndexOf('#', queryIndex + 1);
        if (fragmentIndex == queryIndex + 1)
        {
            return string.Empty;
        }

        return fragmentIndex < 0
            ? nextLink[(queryIndex + 1)..]
            : nextLink[(queryIndex + 1)..fragmentIndex];
    }

    private static bool TryGetPropertyCaseInsensitive(
        JsonElement element,
        string propertyName,
        out JsonElement propertyValue)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }
        }

        propertyValue = default;
        return false;
    }

    private static string? NormalizeContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        var normalized = continuationToken.Trim();
        if (normalized.Length >= 2 &&
            normalized[0] == '"' &&
            normalized[^1] == '"')
        {
            normalized = normalized[1..^1];
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string RedactContinuationToken(string url)
    {
        const string tokenParam = "continuationToken=";
        var tokenIndex = url.IndexOf(tokenParam, StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
        {
            return url;
        }

        var tokenStart = tokenIndex + tokenParam.Length;
        var tokenEnd = url.IndexOf('&', tokenStart);
        if (tokenEnd < 0)
        {
            return $"{url[..tokenStart]}<redacted>";
        }

        return $"{url[..tokenStart]}<redacted>{url[tokenEnd..]}";
    }

    private static string SanitizeUrlForLogging(string url)
    {
        var uri = new Uri(url);
        var sanitizedBuilder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty
        };

        return RedactContinuationToken(sanitizedBuilder.Uri.ToString());
    }

    private static string ToTfsQueryDateTimeUtc(DateTimeOffset dateTime)
    {
        return dateTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    private static void ValidateSerializedStartDateTime(string serializedStartDateTime)
    {
        if (string.IsNullOrWhiteSpace(serializedStartDateTime))
        {
            throw new InvalidOperationException("Serialized startDateTime is empty.");
        }

        if (serializedStartDateTime.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Serialized startDateTime contains whitespace: '{serializedStartDateTime}'.");
        }

        if (!serializedStartDateTime.EndsWith("Z", StringComparison.Ordinal) &&
            !serializedStartDateTime.EndsWith("+00:00", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Serialized startDateTime must be UTC (Z or +00:00): '{serializedStartDateTime}'.");
        }
    }

    private static bool IsNonRetryableReportingError(string stage, TfsException exception)
    {
        if (!string.Equals(stage, "Reporting", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (exception.StatusCode != (int)HttpStatusCode.BadRequest)
        {
            return false;
        }

        var errorContent = exception.ErrorContent ?? string.Empty;
        return errorContent.Contains("not valid for Nullable`1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonRetryableReportingClientError(string stage, InvalidOperationException exception)
    {
        if (!string.Equals(stage, "Reporting", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return exception.Message.Contains("startDateTime", StringComparison.OrdinalIgnoreCase);
    }

    // Caller must hold _paginationGate.
    private void EnsurePaginationState(string? continuationToken, int maxTokenHistory)
    {
        if (_paginationCompleted)
        {
            _observedContinuationTokens.Clear();
            _totalPagesFetched = 0;
            _emptyPages = 0;
            _progressWithoutDataPages = 0;
            _paginationCompleted = false;
        }

        TrackContinuationToken(continuationToken, maxTokenHistory);
    }

    private void TrackContinuationToken(string? continuationToken, int maxTokenHistory)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return;
        }

        if (_observedContinuationTokens.Count >= maxTokenHistory)
        {
            return;
        }

        _observedContinuationTokens.Add(continuationToken);
    }

    private ReportingRevisionsResult MarkPaginationComplete(ReportingRevisionsResult result)
    {
        if (result.IsComplete)
        {
            _paginationCompleted = true;
        }

        return result;
    }

    private ReportingRevisionsResult CreateTerminationResult(
        ReportingRevisionsTermination termination,
        ReportingRevisionsPagePayload pagePayload,
        int pageIndex,
        string? continuationToken,
        bool tokenAdvanced,
        string? skipReason,
        bool logPerPageSummary,
        RevisionIngestionRunContext runContext)
    {
        LogTermination(termination, pageIndex, continuationToken, pagePayload.Revisions.Count);

        if (logPerPageSummary)
        {
            LogPaginationSummary(
                runContext,
                pageIndex,
                pagePayload.Revisions.Count,
                continuationToken,
                tokenAdvanced,
                skipReason,
                logPerPageSummary,
                termination: termination);
        }

        var result = new ReportingRevisionsResult(
            pagePayload.Revisions,
            continuationToken: null,
            termination,
            pagePayload.HttpStatusCode,
            pagePayload.HttpDurationMs,
            pagePayload.ParseDurationMs,
            pagePayload.TransformDurationMs);

        _paginationCompleted = true;
        return result;
    }

    private void LogPaginationSummary(
        RevisionIngestionRunContext runContext,
        int pageIndex,
        int revisionCount,
        string? continuationToken,
        bool tokenAdvanced,
        string? skipReason,
        bool logPerPageSummary,
        ReportingRevisionsTermination? termination)
    {
        if (!logPerPageSummary || !runContext.IsEnabled || !_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var tokenHash = HashContinuationToken(continuationToken);
        _logger.LogInformation(
            "Reporting revisions pagination. PageIndex={PageIndex} RevisionCount={RevisionCount} ContinuationTokenPresent={ContinuationTokenPresent} ContinuationTokenHash={ContinuationTokenHash} TokenAdvanced={TokenAdvanced} EmptyPages={EmptyPages} ProgressWithoutDataPages={ProgressWithoutDataPages} TotalPages={TotalPages} SkipReason={SkipReason} TerminationReason={TerminationReason} TerminationMessage={TerminationMessage}",
            pageIndex,
            revisionCount,
            continuationToken != null,
            tokenHash,
            tokenAdvanced,
            _emptyPages,
            _progressWithoutDataPages,
            _totalPagesFetched,
            skipReason,
            termination?.Reason,
            termination?.Message);
    }

    private void LogTermination(
        ReportingRevisionsTermination termination,
        int pageIndex,
        string? continuationToken,
        int revisionCount)
    {
        if (!_logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        var tokenHash = HashContinuationToken(continuationToken);
        _logger.LogWarning(
            "Reporting revisions pagination terminated. PageIndex={PageIndex} RevisionCount={RevisionCount} ContinuationTokenHash={ContinuationTokenHash} Reason={Reason} Message={Message}",
            pageIndex,
            revisionCount,
            tokenHash,
            termination.Reason,
            termination.Message);
    }

    private static string? HashContinuationToken(string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return null;
        }

        var hash = StringComparer.Ordinal.GetHashCode(continuationToken);
        return unchecked((uint)hash).ToString(TokenHashHexFormat, CultureInfo.InvariantCulture);
    }

    private void LogEffortParseSummary(EffortParseSummary summary, int pageIndex)
    {
        if (summary.FailedCount <= 0 || !_logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        var sampleValues = summary.Samples.Count > 0
            ? string.Join(" | ", summary.Samples)
            : "<none>";

        _logger.LogWarning(
            "Effort parse failures on reporting revisions page. PageIndex={PageIndex} FailedEffortParseCount={FailedEffortParseCount} Samples={Samples}",
            pageIndex,
            summary.FailedCount,
            sampleValues);
    }

    private RevisionParseResult ParseReportingRevisionsPayload(JsonDocument doc)
    {
        var revisions = new List<WorkItemRevision>();
        var maxParseWarnings = _diagnostics?.GetMaxParseWarningsPerPage() ?? DefaultMaxParseWarningsPerPage;
        var warningLimiter = new ParseWarningLimiter(maxParseWarnings);
        var effortParseTracker = new EffortParseTracker();

        // Parse revisions from response (some API versions return "value" vs legacy "values")
        if (doc.RootElement.TryGetProperty("value", out var valuesArray) ||
            doc.RootElement.TryGetProperty("values", out valuesArray))
        {
            foreach (var revision in valuesArray.EnumerateArray())
            {
                var workItemRevision = ParseWorkItemRevision(revision, warningLimiter, effortParseTracker);
                if (workItemRevision != null)
                {
                    revisions.Add(workItemRevision);
                }
            }
        }
        else
        {
            // Error-only path: capture the raw payload for diagnostics.
            var rawPayload = doc.RootElement.GetRawText();
            var truncatedPayload = TruncatePayloadForLogging(rawPayload);

            _logger.LogError(
                "Reporting revisions response missing expected 'value' or 'values' array. Payload (truncated): {Payload}",
                truncatedPayload);

            throw new TfsException(
                "Reporting revisions response missing expected 'value' or 'values' array.",
                truncatedPayload);
        }

        if (warningLimiter.SuppressedCount > 0)
        {
            _logger.LogWarning(
                "Suppressed {SuppressedCount} additional revision parse warnings for this page.",
                warningLimiter.SuppressedCount);
        }

        return new RevisionParseResult(revisions, effortParseTracker.ToSummary());
    }

    private WorkItemRevision? ParseWorkItemRevision(
        JsonElement revision,
        ParseWarningLimiter warningLimiter,
        EffortParseTracker? effortParseTracker)
    {
        try
        {
            var hasWorkItemId = TryGetIntProperty(
                revision,
                "id",
                out var idElement,
                out var workItemId,
                out var idFailureReason);
            var hasRevisionNumber = TryGetIntProperty(
                revision,
                "rev",
                out var revElement,
                out var revisionNumber,
                out var revFailureReason);
            var warningContext = new ParseWarningContext(hasWorkItemId ? workItemId : null, hasRevisionNumber ? revisionNumber : null);

            if (!hasWorkItemId)
            {
                LogParseWarning(
                    warningLimiter,
                    "id",
                    idElement,
                    idFailureReason == "missing",
                    warningContext,
                    idFailureReason ?? "Invalid work item id value");
                return null;
            }

            if (!hasRevisionNumber)
            {
                LogParseWarning(
                    warningLimiter,
                    "rev",
                    revElement,
                    revFailureReason == "missing",
                    warningContext,
                    revFailureReason ?? "Invalid revision number value");
                return null;
            }

            // Parse fields
            if (!revision.TryGetProperty("fields", out var fields))
            {
                LogParseWarning(
                    warningLimiter,
                    "fields",
                    default,
                    isMissing: true,
                    warningContext,
                    "Missing fields object");
                return null;
            }

            var workItemType = GetStringField(fields, "System.WorkItemType") ?? "Unknown";
            var title = GetStringField(fields, "System.Title") ?? "";
            var state = GetStringField(fields, "System.State") ?? "Unknown";
            var reason = GetStringField(fields, "System.Reason");
            var iterationPath = GetStringField(fields, "System.IterationPath") ?? "";
            var areaPath = GetStringField(fields, "System.AreaPath") ?? "";
            var tags = GetStringField(fields, "System.Tags");
            var severity = GetStringField(fields, "Microsoft.VSTS.Common.Severity");

            var createdDate = GetDateTimeField(fields, "System.CreatedDate");
            var changedDate = GetDateTimeField(fields, "System.ChangedDate");
            var closedDate = GetDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");
            var effort = GetDoubleField(fields, "Microsoft.VSTS.Scheduling.Effort", out var effortParseFailed, out var effortValueSnippet);
            if (effortParseFailed)
            {
                effortParseTracker?.RecordFailure(effortValueSnippet);
            }

            // If ChangedDate is missing, log warning and skip this revision - timestamp is critical for ordering
            if (!changedDate.HasValue)
            {
                var hasChangedDate = fields.TryGetProperty("System.ChangedDate", out var changedDateElement);
                LogParseWarning(
                    warningLimiter,
                    "System.ChangedDate",
                    changedDateElement,
                    !hasChangedDate,
                    warningContext,
                    "Missing or invalid System.ChangedDate field");
                return null;
            }

            var changedBy = GetStringField(fields, "System.ChangedBy");

            return new WorkItemRevision
            {
                WorkItemId = workItemId,
                RevisionNumber = revisionNumber,
                WorkItemType = workItemType,
                Title = title,
                State = state,
                Reason = reason,
                IterationPath = iterationPath,
                AreaPath = areaPath,
                CreatedDate = createdDate,
                ChangedDate = changedDate.Value,
                ClosedDate = closedDate,
                Effort = effort,
                Tags = tags,
                Severity = severity,
                ChangedBy = changedBy
            };
        }
        catch (Exception ex)
        {
            warningLimiter.TryLog(() => _logger.LogWarning(ex, "Failed to parse work item revision"));
            return null;
        }
    }

    private (WorkItemRevision Revision, Dictionary<string, string?> CurrentFields)? ParseWorkItemRevisionFromPerItem(
        JsonElement revision,
        int workItemId,
        Dictionary<string, string?>? previousFields)
    {
        try
        {
            var revisionNumber = 0;
            if (revision.TryGetProperty("rev", out var revEl) &&
                TryParseIntValue(revEl, out var parsedRevision, out _))
            {
                revisionNumber = parsedRevision;
            }

            if (!revision.TryGetProperty("fields", out var fields))
            {
                return null;
            }

            // Build current field values
            var currentFields = new Dictionary<string, string?>();
            foreach (var fieldName in FieldWhitelist)
            {
                currentFields[fieldName] = GetStringField(fields, fieldName);
            }

            // Calculate field deltas
            var fieldDeltas = new Dictionary<string, FieldDelta>();
            if (previousFields != null)
            {
                foreach (var (fieldName, newValue) in currentFields)
                {
                    var oldValue = previousFields.GetValueOrDefault(fieldName);
                    if (oldValue != newValue)
                    {
                        fieldDeltas[fieldName] = new FieldDelta
                        {
                            FieldName = fieldName,
                            OldValue = oldValue,
                            NewValue = newValue
                        };
                    }
                }
            }

            var workItemType = currentFields.GetValueOrDefault("System.WorkItemType") ?? "Unknown";
            var title = currentFields.GetValueOrDefault("System.Title") ?? "";
            var state = currentFields.GetValueOrDefault("System.State") ?? "Unknown";
            var iterationPath = currentFields.GetValueOrDefault("System.IterationPath") ?? "";
            var areaPath = currentFields.GetValueOrDefault("System.AreaPath") ?? "";

            var changedDate = GetDateTimeField(fields, "System.ChangedDate") ?? DateTimeOffset.UtcNow;
            var createdDate = GetDateTimeField(fields, "System.CreatedDate");
            var closedDate = GetDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");
            var effort = GetDoubleField(fields, "Microsoft.VSTS.Scheduling.Effort");

            var changedBy = GetStringField(fields, "System.ChangedBy");

            var workItemRevision = new WorkItemRevision
            {
                WorkItemId = workItemId,
                RevisionNumber = revisionNumber,
                WorkItemType = workItemType,
                Title = title,
                State = state,
                Reason = currentFields.GetValueOrDefault("System.Reason"),
                IterationPath = iterationPath,
                AreaPath = areaPath,
                CreatedDate = createdDate,
                ChangedDate = changedDate,
                ClosedDate = closedDate,
                Effort = effort,
                Tags = currentFields.GetValueOrDefault("System.Tags"),
                Severity = currentFields.GetValueOrDefault("Microsoft.VSTS.Common.Severity"),
                ChangedBy = changedBy,
                FieldDeltas = fieldDeltas.Count > 0 ? fieldDeltas : null,
                RelationDeltas = null
            };

            return (workItemRevision, currentFields);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse work item revision JSON for work item {WorkItemId}", workItemId);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON operation while parsing work item revision for work item {WorkItemId}", workItemId);
            return null;
        }
    }

    private static string TruncatePayloadForLogging(string payload)
    {
        if (payload.Length <= MaxPayloadLogLength)
        {
            return payload;
        }

        var maxLength = Math.Max(0, MaxPayloadLogLength - TruncationSuffix.Length);
        return $"{payload[..maxLength]}{TruncationSuffix}";
    }

    private static string? GetStringField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object when element.TryGetProperty("displayName", out var dn) => dn.GetString(),
            JsonValueKind.Object when element.TryGetProperty("name", out var n) => n.GetString(),
            _ => element.GetRawText()
        };
    }

    private static DateTimeOffset? GetDateTimeField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(element.GetString(), out var dto))
            {
                return dto;
            }
        }

        try
        {
            return element.GetDateTimeOffset();
        }
        catch
        {
            return null;
        }
    }

    private static double? GetDoubleField(
        JsonElement fields,
        string fieldName,
        out bool parseFailed,
        out string? valueSnippet)
    {
        parseFailed = false;
        valueSnippet = null;

        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetDouble(out var doubleValue))
                {
                    return doubleValue;
                }

                parseFailed = true;
                valueSnippet = GetValueSnippet(element);
                return null;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var rawValue = element.GetString()?.Trim();
                if (string.IsNullOrEmpty(rawValue))
                {
                    return null;
                }

                if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
                {
                    return parsedValue;
                }

                parseFailed = true;
                valueSnippet = GetValueSnippet(element);
                return null;
            }

            parseFailed = true;
            valueSnippet = GetValueSnippet(element);
            return null;
        }
        catch
        {
            parseFailed = true;
            return null;
        }
    }

    private static double? GetDoubleField(JsonElement fields, string fieldName)
    {
        return GetDoubleField(fields, fieldName, out _, out _);
    }

    private int? GetIntField(
        JsonElement fields,
        string fieldName,
        ParseWarningLimiter? warningLimiter = null,
        ParseWarningContext? warningContext = null)
    {
        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (element.TryGetInt64(out var longValue))
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    return (int)longValue;
                }

                LogParseWarning(
                    warningLimiter,
                    fieldName,
                    element,
                    isMissing: false,
                    warningContext,
                    "Integer value is out of range");
                return null;
            }

            LogParseWarning(
                warningLimiter,
                fieldName,
                element,
                isMissing: false,
                warningContext,
                "Invalid numeric value");
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var rawValue = element.GetString()?.Trim();
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            LogParseWarning(
                warningLimiter,
                fieldName,
                element,
                isMissing: false,
                warningContext,
                "Invalid integer string");
            return null;
        }

        LogParseWarning(
            warningLimiter,
            fieldName,
            element,
            isMissing: false,
            warningContext,
            "Unsupported JSON value kind");
        return null;
    }

    private static bool TryGetIntProperty(
        JsonElement parent,
        string propertyName,
        out JsonElement element,
        out int value,
        out string? failureReason)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out element))
        {
            failureReason = "missing";
            return false;
        }

        if (TryParseIntValue(element, out value, out failureReason))
        {
            return true;
        }

        failureReason ??= "Invalid integer value";
        return false;
    }

    private static bool TryParseIntValue(JsonElement element, out int value, out string? failureReason)
    {
        value = default;
        failureReason = null;

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out value))
            {
                return true;
            }

            if (element.TryGetInt64(out var longValue))
            {
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    value = (int)longValue;
                    return true;
                }

                failureReason = "Integer value is out of range";
                return false;
            }

            failureReason = "Invalid numeric value";
            return false;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var rawValue = element.GetString()?.Trim();
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            failureReason = "Invalid integer string";
            return false;
        }

        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            failureReason = "null";
            return false;
        }

        failureReason = $"Unsupported JSON value kind ({element.ValueKind})";
        return false;
    }

    private void LogParseWarning(
        ParseWarningLimiter? warningLimiter,
        string fieldName,
        JsonElement element,
        bool isMissing,
        ParseWarningContext? warningContext,
        string reason)
    {
        if (warningLimiter == null || warningContext == null)
        {
            return;
        }

        warningLimiter.TryLog(() =>
        {
            var valueKind = isMissing ? "Missing" : element.ValueKind.ToString();
            var valueSnippet = isMissing ? "<missing>" : GetValueSnippet(element);
            _logger.LogWarning(
                "Revision parse warning. Field={FieldName} Kind={ValueKind} Value={ValueSnippet} WorkItemId={WorkItemId} Revision={RevisionNumber} Reason={Reason}",
                fieldName,
                valueKind,
                valueSnippet,
                warningContext.Value.WorkItemId,
                warningContext.Value.RevisionNumber,
                reason);
        });
    }

    private static string GetValueSnippet(JsonElement element)
    {
        var rawValue = element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();

        if (rawValue.Length <= MaxValuePreviewLength)
        {
            return rawValue;
        }

        var maxLength = Math.Max(0, MaxValuePreviewLength - TruncationSuffix.Length);
        return $"{rawValue[..maxLength]}{TruncationSuffix}";
    }

    private record struct ParseWarningContext(int? WorkItemId, int? RevisionNumber);

    private sealed class ParseWarningLimiter
    {
        private readonly int _limit;
        private int _count;
        private int _suppressed;

        public ParseWarningLimiter(int limit)
        {
            _limit = limit;
        }

        public int SuppressedCount => Volatile.Read(ref _suppressed);

        public bool TryLog(Action logAction)
        {
            if (_limit <= 0)
            {
                return false;
            }

            var currentCount = Interlocked.Increment(ref _count);
            if (currentCount <= _limit)
            {
                logAction();
                return true;
            }

            Interlocked.Increment(ref _suppressed);
            return false;
        }
    }

    private sealed record RevisionParseResult(
        IReadOnlyList<WorkItemRevision> Revisions,
        EffortParseSummary EffortParseSummary);

    private sealed record EffortParseSummary(int FailedCount, IReadOnlyList<string> Samples)
    {
        public static EffortParseSummary Empty { get; } = new(0, Array.Empty<string>());
    }

    private sealed class EffortParseTracker
    {
        private const int MaxSamples = 3;
        private readonly List<string> _samples = new();
        private int _failedCount;

        public void RecordFailure(string? valueSnippet)
        {
            _failedCount++;
            if (_samples.Count >= MaxSamples)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(valueSnippet))
            {
                _samples.Add(valueSnippet);
            }
        }

        public EffortParseSummary ToSummary()
        {
            return _failedCount == 0
                ? EffortParseSummary.Empty
                : new EffortParseSummary(_failedCount, _samples.ToArray());
        }
    }

    private HttpClient GetAuthenticatedHttpClient()
    {
        var client = _httpClientFactory.CreateClient("TfsClient.NTLM");
        _logger.LogDebug("Using NTLM-authenticated HttpClient for revision TFS request");
        return client;
    }

    private void ValidateTfsConfiguration(TfsConfigEntity? config)
    {
        if (config == null)
        {
            throw new TfsConfigurationException("TFS is not configured. Please configure TFS settings first.");
        }

        if (string.IsNullOrWhiteSpace(config.Url))
        {
            throw new TfsConfigurationException("TFS URL is not configured.");
        }
    }

    private async Task HandleHttpErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;
        var requestUri = response.RequestMessage?.RequestUri is { } uri
            ? SanitizeUrlForLogging(uri.ToString())
            : "<unknown>";

        _logger.LogError(
            "TFS API error at {RequestUri}: {StatusCode} {ReasonPhrase}. Response: {Content}",
            requestUri,
            statusCode,
            response.ReasonPhrase,
            content);

        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => new TfsAuthenticationException("TFS authentication failed. Check credentials.", content),
            System.Net.HttpStatusCode.Forbidden => new TfsAuthorizationException("TFS access denied. Check permissions.", content),
            System.Net.HttpStatusCode.NotFound => new TfsResourceNotFoundException($"TFS resource not found: {requestUri}", content),
            _ => new TfsException($"TFS API error at {requestUri}: {statusCode} {response.ReasonPhrase}", statusCode, content)
        };
    }

    private async Task<T> ExecuteWithRetryAsync<T>(string stage, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var baseDelay = TimeSpan.FromSeconds(1);
        RevisionIngestionRunContext runContext = default;
        var hasDiagnostics = _diagnostics?.TryGetCurrentRun(out runContext) == true && runContext.IsEnabled;

        while (true)
        {
            var attemptStart = hasDiagnostics ? Stopwatch.GetTimestamp() : 0;

            try
            {
                return await _throttler.ExecuteReadAsync(operation, cancellationToken);
            }
            catch (TfsAuthenticationException)
            {
                throw; // Don't retry auth errors
            }
            catch (TfsAuthorizationException)
            {
                throw; // Don't retry authorization errors
            }
            catch (TfsException ex) when (IsNonRetryableReportingError(stage, ex))
            {
                _logger.LogError(
                    ex,
                    "Non-retryable reporting API error encountered for stage {Stage}.",
                    stage);
                throw;
            }
            catch (InvalidOperationException ex) when (IsNonRetryableReportingClientError(stage, ex))
            {
                _logger.LogError(
                    ex,
                    "Non-retryable reporting API client error encountered for stage {Stage}.",
                    stage);
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (retryCount < MaxRetries)
            {
                retryCount++;
                var delay = baseDelay * Math.Pow(2, retryCount - 1);

                if (hasDiagnostics)
                {
                    var durationMs = RevisionIngestionDiagnostics.GetElapsedMilliseconds(attemptStart);
                    var statusCode = (ex as TfsException)?.StatusCode;
                    _diagnostics!.LogRetryAttempt(
                        runContext,
                        stage,
                        retryCount,
                        MaxRetries,
                        delay.TotalMilliseconds,
                        statusCode,
                        durationMs,
                        ex);
                }

                _logger.LogWarning(
                    ex,
                    "TFS revision API call failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms",
                    retryCount, MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        string url,
        CancellationToken cancellationToken,
        bool handleErrors = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await _requestSender.SendAsync(
            httpClient,
            request,
            config.TimeoutSeconds,
            cancellationToken,
            handleErrors ? HandleHttpErrorsAsync : null);
    }

    public void Dispose()
    {
        _paginationGate.Dispose();
    }

    private sealed record ReportingRevisionsPagePayload(
        IReadOnlyList<WorkItemRevision> Revisions,
        string? ContinuationToken,
        int? HttpStatusCode,
        long? HttpDurationMs,
        long? ParseDurationMs,
        long? TransformDurationMs,
        string? PayloadError,
        EffortParseSummary EffortParseSummary)
    {
        public static ReportingRevisionsPagePayload Empty { get; } = new(
            Array.Empty<WorkItemRevision>(),
            null,
            null,
            null,
            null,
            null,
            null,
            EffortParseSummary.Empty);
    }

}
