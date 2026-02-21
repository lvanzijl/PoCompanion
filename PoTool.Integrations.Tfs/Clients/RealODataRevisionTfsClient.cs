using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Retrieves work item revisions from Analytics OData.
/// </summary>
public sealed class RealODataRevisionTfsClient : IWorkItemRevisionSource
{
    private const int MaxWarningLogs = 10;
    private static readonly string[] WorkItemIdAliases = ["WorkItemId", "System_Id", "id", "WorkItemSK", "System.Id"];
    private static readonly string[] RevisionAliases = ["Revision", "Rev", "System_Rev", "RevisionNumber", "System.Rev"];
    private static readonly string[] ChangedDateAliases = ["ChangedDate", "RevisedDate", "System_ChangedDate", "System.ChangedDate"];
    private static readonly RevisionFieldWhitelist.ODataRevisionSelectionSpec ODataSelectionSpec =
        RevisionFieldWhitelist.BuildODataRevisionSelectionSpec(includeRevision: true);
    private static readonly IReadOnlyDictionary<string, RevisionFieldWhitelist.ODataRevisionParseDescriptor> ParseDescriptorsByRestField =
        ODataSelectionSpec.ParseDescriptors.ToDictionary(descriptor => descriptor.RestFieldRef, StringComparer.Ordinal);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITfsConfigurationService _configService;
    private readonly TfsRequestSender _requestSender;
    private readonly IOptionsMonitor<RevisionIngestionPaginationOptions> _paginationOptions;
    private readonly ILogger<RealODataRevisionTfsClient> _logger;
    private readonly ODataRevisionQueryBuilder _queryBuilder = new();

    public RealODataRevisionTfsClient(
        IHttpClientFactory httpClientFactory,
        ITfsConfigurationService configService,
        TfsRequestSender requestSender,
        IOptionsMonitor<RevisionIngestionPaginationOptions> paginationOptions,
        ILogger<RealODataRevisionTfsClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _requestSender = requestSender;
        _paginationOptions = paginationOptions;
        _logger = logger;
    }

    public RevisionSource SourceType => RevisionSource.AnalyticsODataRevisions;

    public Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        return GetAllRevisionsForWorkItemAsync(workItemId, cancellationToken);
    }

    public Task<ReportingRevisionsResult> GetRevisionsForScopeAsync(
        IReadOnlyCollection<int> scopedWorkItemIds,
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default)
    {
        return GetRevisionsAsync(startDateTime, continuationToken, scopedWorkItemIds, expandMode, cancellationToken);
    }

    public async Task<ReportingRevisionsResult> GetRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        IReadOnlyCollection<int>? scopedWorkItemIds = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default)
    {
        _ = expandMode;
        var config = await GetValidatedConfigAsync(cancellationToken);
        var options = _paginationOptions.CurrentValue;
        var top = Math.Max(1, options.ODataTop);
        var seekTop = Math.Max(1, options.ODataSeekPageSize);
        var quoteDateStrings = options.ODataQuoteDateStrings;
        var useSegmentedScope = ShouldUseSegmentedScope(scopedWorkItemIds, options);
        var segments = useSegmentedScope
            ? WorkItemIdRangeSegmentBuilder.Build(scopedWorkItemIds)
            : [];
        if (useSegmentedScope && segments.Count == 0)
        {
            _logger.LogInformation("OData segmented scope produced no valid WorkItemId ranges. Returning empty revision page.");
            return new ReportingRevisionsResult([], continuationToken: null);
        }

        var segmentState = ResolveSegmentState(continuationToken, segments);
        var requestContext = ResolveRequestContext(
            config,
            startDateTime,
            segmentState.InnerContinuationToken,
            scopedWorkItemIds,
            segmentState.Segment,
            options,
            top,
            quoteDateStrings);
        var pageIndex = requestContext.SeekState?.PageIndex ?? 1;
        var mode = requestContext.Mode;
        var url = requestContext.Url;
        var fallbackTriggered = false;
        var scopeCount = scopedWorkItemIds?.Count ?? 0;
        var scopeMin = scopedWorkItemIds is { Count: > 0 } ? scopedWorkItemIds.Min() : (int?)null;
        var scopeMax = scopedWorkItemIds is { Count: > 0 } ? scopedWorkItemIds.Max() : (int?)null;
        var segmentCount = segments.Count;
        var totalSegmentSpan = segments.Sum(segment => segment.Span);
        var maxSegmentSpan = segments.Count == 0 ? 0 : segments.Max(segment => segment.Span);
        _logger.LogInformation(
            "Requesting OData revisions page. Mode={Mode} PageIndex={PageIndex} Top={Top} SeekTop={SeekTop} QuotedDateStrings={QuotedDateStrings} ScopeCount={ScopeCount} ScopeMin={ScopeMin} ScopeMax={ScopeMax} SegmentCount={SegmentCount} SegmentIndex={SegmentIndex} SegmentRange={SegmentRange} TotalSegmentSpan={TotalSegmentSpan} MaxSegmentSpan={MaxSegmentSpan} Filter={Filter}",
            mode,
            pageIndex,
            requestContext.Top,
            seekTop,
            quoteDateStrings,
            scopeCount,
            scopeMin,
            scopeMax,
            segmentCount,
            segmentState.SegmentIndex,
            segmentState.Segment is null ? "<none>" : $"{segmentState.Segment.Value.Start}-{segmentState.Segment.Value.End}",
            totalSegmentSpan,
            maxSegmentSpan,
            TryGetFilterFromUrl(url) ?? "<none>");
        var httpClient = _httpClientFactory.CreateClient("TfsClient.NTLM");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _requestSender.SendAsync(httpClient, request, config.TimeoutSeconds, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (ShouldRetryWithAlternateDateFormat(responseBody))
            {
                fallbackTriggered = true;
                quoteDateStrings = !quoteDateStrings;
                requestContext = ResolveRequestContext(
                    config,
                    startDateTime,
                    segmentState.InnerContinuationToken,
                    scopedWorkItemIds,
                    segmentState.Segment,
                    options,
                    top,
                    quoteDateStrings);
                url = requestContext.Url;
                _logger.LogWarning(
                    "Retrying OData request with alternate date literal format");

                response.Dispose();
                using var retryRequest = new HttpRequestMessage(HttpMethod.Get, url);
                response = await _requestSender.SendAsync(httpClient, retryRequest, config.TimeoutSeconds, cancellationToken);
            }
        }

        using (response)
        {
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncatedBody = TruncateForLog(responseBody);
            _logger.LogError(
                "OData revisions request failed. StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} Mode={Mode} PageIndex={PageIndex} Url={Url} Filter={Filter} QuotedDateStrings={QuotedDateStrings} FallbackTriggered={FallbackTriggered} ResponseBody={ResponseBody}",
                (int)response.StatusCode,
                response.ReasonPhrase ?? string.Empty,
                mode,
                pageIndex,
                RedactCredentials(url),
                TryGetFilterFromUrl(url) ?? "<none>",
                quoteDateStrings,
                fallbackTriggered,
                truncatedBody);
            throw new HttpRequestException(
                $"OData revisions request failed with status {(int)response.StatusCode} ({response.StatusCode}). Filter={TryGetFilterFromUrl(url) ?? "<none>"}. ResponseBody={truncatedBody}",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var revisions = ParseRevisions(root);
        var nextLink = TryGetCaseInsensitiveString(root, "@odata.nextLink");
        var minChangedDate = revisions.Count > 0 ? revisions.Min(revision => revision.ChangedDate) : (DateTimeOffset?)null;
        var maxChangedDate = revisions.Count > 0 ? revisions.Max(revision => revision.ChangedDate) : (DateTimeOffset?)null;
        RevisionCursorTuple? maxTuple = TryGetMaxTuple(revisions, out var tuple) ? tuple : null;

        _logger.LogInformation(
            "OData revisions page loaded. Mode={Mode} PageIndex={PageIndex} RequestedUrl={RequestedUrl} Count={Count} MinChangedDate={MinChangedDate} MaxChangedDate={MaxChangedDate} MaxTuple={MaxTuple} HasNextLink={HasNextLink}",
            mode,
            pageIndex,
            RedactCredentials(url),
            revisions.Count,
            minChangedDate,
            maxChangedDate,
            maxTuple,
            !string.IsNullOrWhiteSpace(nextLink));

        var continuation = NormalizeToken(nextLink);
        var stopReason = "NoMoreResults";
        if (continuation == null &&
            options.ODataEnableSeekPagingFallback &&
            revisions.Count >= requestContext.Top &&
            maxTuple is not null)
        {
            var noProgressPages = 0;
            if (requestContext.SeekState?.LastCursor is not null)
            {
                noProgressPages = requestContext.SeekState.LastCursor.Equals(maxTuple.Value) ? requestContext.SeekState.NoProgressPages + 1 : 0;
            }

            if (noProgressPages >= Math.Max(1, options.MaxNoProgressPages))
            {
                throw new InvalidOperationException(
                    $"OData seek paging detected no progress. Page={pageIndex}, Top={requestContext.Top}, Filter={TryGetFilterFromUrl(url) ?? "<none>"}, LastCursor={requestContext.SeekState?.LastCursor}, CurrentCursor={maxTuple}, SampleTuples={BuildTupleSample(revisions)}");
            }

            var seekPageUrl = _queryBuilder.BuildSeekPageUrl(
                config,
                startDateTime,
                scopedWorkItemIds,
                options,
                seekTop,
                maxTuple.Value.ChangedDate,
                maxTuple.Value.WorkItemId,
                maxTuple.Value.Revision,
                segmentState.Segment,
                quoteDateStrings);
            continuation = EncodeSeekState(new SeekContinuationState(
                seekPageUrl,
                pageIndex + 1,
                maxTuple.Value,
                noProgressPages));
            stopReason = "Seek";
        }
        else if (!string.IsNullOrWhiteSpace(nextLink))
        {
            stopReason = "NextLink";
        }
        else if (revisions.Count < requestContext.Top)
        {
            stopReason = "ShortPage";
        }

        _logger.LogInformation(
            "OData revisions request summary. TotalPages=1 TotalRows={TotalRows} StopReason={StopReason} Filter={Filter} QuotedDateStrings={QuotedDateStrings} FallbackTriggered={FallbackTriggered}",
            revisions.Count,
            stopReason,
            TryGetFilterFromUrl(url) ?? "<none>",
            quoteDateStrings,
            fallbackTriggered);

        return new ReportingRevisionsResult(
            revisions,
            BuildSegmentContinuationToken(segments, segmentState.SegmentIndex, continuation));
        }
    }

    private async Task<IReadOnlyList<WorkItemRevision>> GetAllRevisionsForWorkItemAsync(
        int workItemId,
        CancellationToken cancellationToken)
    {
        var maxTotalPages = Math.Max(1, _paginationOptions.CurrentValue.MaxTotalPages);
        var maxTotalRows = Math.Max(1, _paginationOptions.CurrentValue.MaxTotalRows);
        var maxEmptyPages = Math.Max(1, _paginationOptions.CurrentValue.MaxEmptyPages);
        var rows = new List<WorkItemRevision>();
        var observedTokens = new HashSet<string>(StringComparer.Ordinal);
        string? continuationToken = null;
        var pageIndex = 0;
        var emptyWithNextLinkPages = 0;

        while (rows.Count < maxTotalRows && pageIndex < maxTotalPages)
        {
            pageIndex++;
            var page = await GetRevisionsAsync(
                startDateTime: null,
                continuationToken: continuationToken,
                scopedWorkItemIds: [workItemId],
                expandMode: ReportingExpandMode.None,
                cancellationToken: cancellationToken);
            rows.AddRange(page.Revisions);

            if (!page.HasMoreResults)
            {
                break;
            }

            var nextToken = NormalizeToken(page.ContinuationToken);
            if (nextToken == null || !observedTokens.Add(nextToken))
            {
                _logger.LogWarning("Stopping OData per-work-item paging due to repeated/non-advancing nextLink. WorkItemId={WorkItemId} Page={PageIndex}", workItemId, pageIndex);
                break;
            }

            if (page.Revisions.Count == 0)
            {
                emptyWithNextLinkPages++;
                if (emptyWithNextLinkPages >= maxEmptyPages)
                {
                    _logger.LogWarning("Stopping OData per-work-item paging due to empty pages with nextLink. WorkItemId={WorkItemId} Page={PageIndex}", workItemId, pageIndex);
                    break;
                }
            }
            else
            {
                emptyWithNextLinkPages = 0;
            }

            continuationToken = nextToken;
        }

        return rows;
    }

    private async Task<TfsConfigEntity> GetValidatedConfigAsync(CancellationToken cancellationToken)
    {
        var config = await _configService.GetConfigEntityAsync(cancellationToken)
                     ?? throw new InvalidOperationException("TFS configuration not found. Configure TFS before revision sync.");

        if (string.IsNullOrWhiteSpace(config.AnalyticsODataBaseUrl))
        {
            throw new InvalidOperationException(
                "Analytics OData base URL is not configured. Check onboarding TFS Analytics/OData settings.");
        }

        if (string.IsNullOrWhiteSpace(config.AnalyticsODataEntitySetPath))
        {
            config.AnalyticsODataEntitySetPath = "WorkItemRevisions";
        }

        return config;
    }

    private RequestContext ResolveRequestContext(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        string? pageContinuationToken,
        IReadOnlyCollection<int>? scopedWorkItemIds,
        WorkItemIdRangeSegment? scopeSegment,
        RevisionIngestionPaginationOptions options,
        int top,
        bool quoteDateStrings)
    {
        var normalizedToken = NormalizeToken(pageContinuationToken);
        if (normalizedToken is null)
        {
            return new RequestContext(
                _queryBuilder.BuildInitialPageUrl(config, startDateTime, scopedWorkItemIds, options, top, scopeSegment, quoteDateStrings),
                RequestMode.Seek,
                top,
                null);
        }

        if (TryDecodeSeekState(normalizedToken, out var seekState) && seekState is not null)
        {
            return new RequestContext(seekState.Url, RequestMode.Seek, ExtractTopOrFallback(seekState.Url, top), seekState);
        }

        return new RequestContext(normalizedToken, RequestMode.NextLink, ExtractTopOrFallback(normalizedToken, top), null);
    }

    private static bool ShouldUseSegmentedScope(
        IReadOnlyCollection<int>? scopedWorkItemIds,
        RevisionIngestionPaginationOptions options)
    {
        return options.ODataScopeMode == ODataRevisionScopeMode.Range &&
               scopedWorkItemIds is { Count: > 0 };
    }

    private static SegmentContinuationState ResolveSegmentState(
        string? continuationToken,
        IReadOnlyList<WorkItemIdRangeSegment> segments)
    {
        var normalizedToken = NormalizeToken(continuationToken);
        if (segments.Count == 0)
        {
            return new SegmentContinuationState(normalizedToken, null, -1);
        }

        if (TryDecodeSegmentState(normalizedToken, out var state) &&
            state is not null &&
            state.SegmentIndex >= 0 &&
            state.SegmentIndex < segments.Count)
        {
            return new SegmentContinuationState(state.InnerContinuationToken, segments[state.SegmentIndex], state.SegmentIndex);
        }

        return new SegmentContinuationState(normalizedToken, segments[0], 0);
    }

    private static string? BuildSegmentContinuationToken(
        IReadOnlyList<WorkItemIdRangeSegment> segments,
        int segmentIndex,
        string? pageContinuationToken)
    {
        if (segments.Count <= 1 || segmentIndex < 0)
        {
            return NormalizeToken(pageContinuationToken);
        }

        var normalizedPageToken = NormalizeToken(pageContinuationToken);
        if (normalizedPageToken is not null)
        {
            return EncodeSegmentState(new SegmentContinuationStateToken(segmentIndex, normalizedPageToken));
        }

        var nextSegmentIndex = segmentIndex + 1;
        if (nextSegmentIndex < segments.Count)
        {
            return EncodeSegmentState(new SegmentContinuationStateToken(nextSegmentIndex, null));
        }

        return null;
    }

    private static bool ShouldRetryWithAlternateDateFormat(string responseBody)
    {
        return !string.IsNullOrWhiteSpace(responseBody) &&
               responseBody.Contains("Unrecognized", StringComparison.OrdinalIgnoreCase) &&
               (responseBody.Contains("ChangedDate", StringComparison.OrdinalIgnoreCase) ||
                responseBody.Contains("literal", StringComparison.OrdinalIgnoreCase));
    }

    private static int ExtractTopOrFallback(string url, int fallback)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var topSegment = uri.Query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(segment => segment.StartsWith("?$top=", StringComparison.OrdinalIgnoreCase) ||
                                           segment.StartsWith("$top=", StringComparison.OrdinalIgnoreCase));
            if (topSegment is not null)
            {
                var value = topSegment.Split('=', 2)[1];
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                {
                    return parsed;
                }
            }
        }

        return fallback;
    }

    private static string? TryGetFilterFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        foreach (var segment in uri.Query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = segment.TrimStart('?');
            if (trimmed.StartsWith("$filter=", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(trimmed["$filter=".Length..]);
            }
        }

        return null;
    }

    private static bool TryGetMaxTuple(IReadOnlyList<WorkItemRevision> revisions, out RevisionCursorTuple tuple)
    {
        tuple = default;
        if (revisions.Count == 0)
        {
            return false;
        }

        var max = revisions
            .OrderBy(revision => revision.ChangedDate)
            .ThenBy(revision => revision.WorkItemId)
            .ThenBy(revision => revision.RevisionNumber)
            .Last();
        tuple = new RevisionCursorTuple(max.ChangedDate.ToUniversalTime(), max.WorkItemId, max.RevisionNumber);
        return true;
    }

    private static string BuildTupleSample(IReadOnlyList<WorkItemRevision> revisions)
    {
        return string.Join(
            "; ",
            revisions.Take(3).Select(revision =>
                $"{revision.ChangedDate.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffffffZ}/{revision.WorkItemId}/{revision.RevisionNumber}"));
    }

    private static string RedactCredentials(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return url;
        }

        var builder = new UriBuilder(uri) { UserName = "***", Password = "***" };
        return builder.Uri.ToString();
    }

    private static string TruncateForLog(string? value, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
    }

    private static string EncodeSeekState(SeekContinuationState state)
    {
        var encodedUrl = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(state.Url));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"seek:{encodedUrl}|{state.PageIndex}|{state.LastCursor.ChangedDate.UtcTicks}|{state.LastCursor.WorkItemId}|{state.LastCursor.Revision}|{state.NoProgressPages}");
    }

    private static string EncodeSegmentState(SegmentContinuationStateToken state)
    {
        var encodedInnerToken = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(state.InnerContinuationToken ?? string.Empty));
        return string.Create(CultureInfo.InvariantCulture, $"seg:{state.SegmentIndex}|{encodedInnerToken}");
    }

    private static bool TryDecodeSeekState(string token, out SeekContinuationState? state)
    {
        state = null;
        if (!token.StartsWith("seek:", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var payload = token["seek:".Length..];
            var parts = payload.Split('|');
            if (parts.Length != 6)
            {
                return false;
            }

            var url = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
            if (string.IsNullOrWhiteSpace(url) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageIndex) ||
                !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var changedTicks) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var workItemId) ||
                !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision) ||
                !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var noProgressPages))
            {
                return false;
            }

            state = new SeekContinuationState(
                url,
                pageIndex,
                new RevisionCursorTuple(new DateTimeOffset(changedTicks, TimeSpan.Zero), workItemId, revision),
                noProgressPages);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeSegmentState(string? token, out SegmentContinuationStateToken? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("seg:", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var payload = token["seg:".Length..];
            var parts = payload.Split('|');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var segmentIndex))
            {
                return false;
            }

            var decodedInnerToken = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            state = new SegmentContinuationStateToken(segmentIndex, NormalizeToken(decodedInnerToken));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private List<WorkItemRevision> ParseRevisions(JsonElement root)
    {
        var revisions = new List<WorkItemRevision>();
        if (!TryGetCaseInsensitiveProperty(root, "value", out var valueArray) || valueArray.ValueKind != JsonValueKind.Array)
        {
            return revisions;
        }

        var parseStats = new RevisionRowParseStats();
        var warningCount = 0;
        foreach (var row in valueArray.EnumerateArray())
        {
            var parsed = TryParseRevisionRow(row, parseStats);
            if (parsed != null)
            {
                revisions.Add(parsed);
            }
            else if (warningCount < MaxWarningLogs)
            {
                warningCount++;
                _logger.LogWarning("Skipping malformed OData revision row.");
            }
        }

        parseStats.LogSummary(_logger);
        return revisions;
    }

    /// <summary>
    /// Runtime requiredness rule for OData revision ingestion:
    /// Only WorkItemId is business-required; WorkItemId+ChangedDate+(Revision if needed) are infra-required.
    /// Revision is required for storage identity in this pipeline, so rows missing Revision are skipped with warning.
    /// </summary>
    private WorkItemRevision? TryParseRevisionRow(JsonElement row, RevisionRowParseStats parseStats)
    {
        var workItemId = ReadIntBySpec(row, "System.Id", parseStats) ?? TryReadInt(row, WorkItemIdAliases);
        var revisionNumber = TryReadInt(row, RevisionAliases);
        if (!workItemId.HasValue)
        {
            throw new InvalidOperationException("OData revision row is missing required field 'WorkItemId'.");
        }

        if (!revisionNumber.HasValue)
        {
            parseStats.MissingRevisionRows++;
            return null;
        }

        var createdDate = ReadDateBySpec(row, "System.CreatedDate", parseStats);
        var changedDate = ReadDateBySpec(row, "System.ChangedDate", parseStats) ?? TryReadDate(row, ChangedDateAliases);
        if (!changedDate.HasValue)
        {
            parseStats.MissingChangedDateRows++;
            throw new InvalidOperationException("OData revision row is missing required field 'ChangedDate'.");
        }

        return new WorkItemRevision
        {
            WorkItemId = workItemId.Value,
            RevisionNumber = revisionNumber.Value,
            WorkItemType = ReadStringBySpec(row, "System.WorkItemType", parseStats),
            Title = ReadStringBySpec(row, "System.Title", parseStats),
            State = ReadStringBySpec(row, "System.State", parseStats),
            Reason = ReadNullableStringBySpec(row, "System.Reason", parseStats),
            IterationPath = ReadStringBySpec(row, "System.IterationPath", parseStats),
            AreaPath = ReadStringBySpec(row, "System.AreaPath", parseStats),
            CreatedDate = createdDate,
            ChangedDate = changedDate.Value.ToUniversalTime(),
            ClosedDate = ReadDateBySpec(row, "Microsoft.VSTS.Common.ClosedDate", parseStats),
            Effort = ReadDoubleBySpec(row, "Microsoft.VSTS.Scheduling.Effort", parseStats),
            BusinessValue = ReadIntBySpec(row, "Microsoft.VSTS.Common.BusinessValue", parseStats),
            Tags = ReadNullableStringBySpec(row, "System.Tags", parseStats),
            Severity = ReadNullableStringBySpec(row, "Microsoft.VSTS.Common.Severity", parseStats),
            ChangedBy = ReadNullableStringBySpec(row, "System.ChangedBy", parseStats),
            FieldDeltas = null,
            RelationDeltas = null
        };
    }

    private static string ReadStringBySpec(JsonElement row, string restFieldRef, RevisionRowParseStats? parseStats = null)
    {
        return ReadNullableStringBySpec(row, restFieldRef, parseStats) ?? string.Empty;
    }

    private static string? ReadNullableStringBySpec(JsonElement row, string restFieldRef, RevisionRowParseStats? parseStats = null)
    {
        var value = ReadElementBySpec(row, restFieldRef, parseStats);
        if (value is null)
        {
            return null;
        }

        return ToNullableString(value.Value);
    }

    private static DateTimeOffset? ReadDateBySpec(JsonElement row, string restFieldRef, RevisionRowParseStats? parseStats = null)
    {
        var value = ReadElementBySpec(row, restFieldRef, parseStats);
        if (value is null || value.Value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.Value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static double? ReadDoubleBySpec(JsonElement row, string restFieldRef, RevisionRowParseStats? parseStats = null)
    {
        var value = ReadElementBySpec(row, restFieldRef, parseStats);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var number))
        {
            return number;
        }

        if (value.Value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static int? ReadIntBySpec(JsonElement row, string restFieldRef, RevisionRowParseStats? parseStats = null)
    {
        var value = ReadElementBySpec(row, restFieldRef, parseStats);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.Value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static JsonElement? ReadElementBySpec(JsonElement row, string restFieldRef, RevisionRowParseStats? parseStats = null)
    {
        if (!ParseDescriptorsByRestField.TryGetValue(restFieldRef, out var descriptor))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.ScalarProperty) &&
            TryGetCaseInsensitiveProperty(row, descriptor.ScalarProperty!, out var scalarValue) &&
            !IsMissingValue(scalarValue))
        {
            return scalarValue;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.NavigationProperty) &&
            TryGetCaseInsensitiveProperty(row, descriptor.NavigationProperty!, out var navigationValue) &&
            navigationValue.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in descriptor.ReadProperties)
            {
                if (TryGetCaseInsensitiveProperty(navigationValue, property, out var nestedValue) &&
                    !IsMissingValue(nestedValue))
                {
                    return nestedValue;
                }
            }
        }

        foreach (var property in descriptor.ReadProperties)
        {
            if (TryGetCaseInsensitiveProperty(row, property, out var directNestedValue) &&
                !IsMissingValue(directNestedValue))
            {
                return directNestedValue;
            }
        }

        var restFieldTail = descriptor.RestFieldRef.Split('.').LastOrDefault();
        if (!string.IsNullOrWhiteSpace(restFieldTail) &&
            TryGetCaseInsensitiveProperty(row, restFieldTail, out var restFieldTailValue) &&
            !IsMissingValue(restFieldTailValue))
        {
            return restFieldTailValue;
        }

        var dotStyleAlias = descriptor.RestFieldRef.Replace('.', '_');
        if (TryGetCaseInsensitiveProperty(row, dotStyleAlias, out var dotStyleAliasValue) &&
            !IsMissingValue(dotStyleAliasValue))
        {
            return dotStyleAliasValue;
        }

        if (TryGetCaseInsensitiveProperty(row, descriptor.RestFieldRef, out var restFieldValue) &&
            !IsMissingValue(restFieldValue))
        {
            return restFieldValue;
        }

        parseStats?.TrackMissing(descriptor);
        return null;
    }

    /// <summary>
    /// Returns true when a JSON element represents an absent value in OData payloads.
    /// </summary>
    private static bool IsMissingValue(JsonElement element)
        => element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static string? ToNullableString(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            return value.ToString();
        }

        return null;
    }

    private static string ReadString(JsonElement row, params string[] keys)
    {
        return ReadNullableString(row, keys) ?? string.Empty;
    }

    private static string? ReadNullableString(JsonElement row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetCaseInsensitiveProperty(row, key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }

            if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return element.ToString();
            }

            if (element.ValueKind == JsonValueKind.Object && TryGetCaseInsensitiveProperty(element, "DisplayName", out var displayName))
            {
                return displayName.GetString();
            }
        }

        return null;
    }

    private static int? TryReadInt(JsonElement row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetCaseInsensitiveProperty(row, key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value))
            {
                return value;
            }

            if (element.ValueKind == JsonValueKind.String &&
                int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
        }

        return null;
    }

    private static DateTimeOffset? TryReadDate(JsonElement row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!TryGetCaseInsensitiveProperty(row, key, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static double? TryReadDouble(JsonElement row, string key)
    {
        if (!TryGetCaseInsensitiveProperty(row, key, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static bool TryGetCaseInsensitiveProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? TryGetCaseInsensitiveString(JsonElement element, string propertyName)
    {
        if (!TryGetCaseInsensitiveProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? NormalizeToken(string? token)
    {
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    private sealed class RevisionRowParseStats
    {
        private readonly Dictionary<string, int> _missingScalarCount = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _missingNavigationCount = new(StringComparer.Ordinal);

        public int MissingRevisionRows { get; set; }

        public int MissingChangedDateRows { get; set; }

        public void TrackMissing(RevisionFieldWhitelist.ODataRevisionParseDescriptor descriptor)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.NavigationProperty))
            {
                Increment(_missingNavigationCount, descriptor.RestFieldRef);
                return;
            }

            Increment(_missingScalarCount, descriptor.RestFieldRef);
        }

        public void LogSummary(ILogger logger)
        {
            if (MissingRevisionRows > 0)
            {
                logger.LogWarning(
                    "Skipped {Count} OData revision rows because Revision was missing.",
                    MissingRevisionRows);
            }

            if (MissingChangedDateRows > 0)
            {
                logger.LogWarning(
                    "Encountered {Count} OData revision rows missing required ChangedDate.",
                    MissingChangedDateRows);
            }

            foreach (var kvp in _missingScalarCount.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                logger.LogDebug("OData missingScalarCount[{Field}]={Count}", kvp.Key, kvp.Value);
            }

            foreach (var kvp in _missingNavigationCount.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                logger.LogDebug("OData missingNavCount[{Field}]={Count}", kvp.Key, kvp.Value);
            }
        }

        private static void Increment(Dictionary<string, int> counts, string field)
        {
            counts.TryGetValue(field, out var current);
            counts[field] = current + 1;
        }
    }

    private enum RequestMode
    {
        NextLink,
        Seek
    }

    private sealed record RequestContext(string Url, RequestMode Mode, int Top, SeekContinuationState? SeekState);
    private sealed record SegmentContinuationState(string? InnerContinuationToken, WorkItemIdRangeSegment? Segment, int SegmentIndex);
    private sealed record SegmentContinuationStateToken(int SegmentIndex, string? InnerContinuationToken);

    private readonly record struct RevisionCursorTuple(DateTimeOffset ChangedDate, int WorkItemId, int Revision)
    {
        public override string ToString()
            => $"{ChangedDate:yyyy-MM-ddTHH:mm:ss.fffffffZ}/{WorkItemId}/{Revision}";
    }

    private sealed record SeekContinuationState(string Url, int PageIndex, RevisionCursorTuple LastCursor, int NoProgressPages);
}
