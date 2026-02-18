using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Retrieves work item revisions from Analytics OData.
/// </summary>
public sealed class RealODataRevisionTfsClient : IWorkItemRevisionSource
{
    private const int DefaultTop = 200;
    private const int MaxWarningLogs = 10;
    private static readonly string[] WorkItemIdAliases = ["WorkItemId", "System_Id", "id", "WorkItemSK", "System.Id"];
    private static readonly string[] RevisionAliases = ["Revision", "Rev", "System_Rev", "RevisionNumber", "System.Rev"];
    private static readonly string[] ChangedDateAliases = ["ChangedDate", "RevisedDate", "System_ChangedDate", "System.ChangedDate"];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITfsConfigurationService _configService;
    private readonly TfsRequestSender _requestSender;
    private readonly IOptionsMonitor<RevisionIngestionPaginationOptions> _paginationOptions;
    private readonly ILogger<RealODataRevisionTfsClient> _logger;

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

    public async Task<ReportingRevisionsResult> GetRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default)
    {
        _ = expandMode;
        var config = await GetValidatedConfigAsync(cancellationToken);
        var url = BuildPageUrl(config, startDateTime, continuationToken);
        var httpClient = _httpClientFactory.CreateClient("TfsClient.NTLM");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _requestSender.SendAsync(httpClient, request, config.TimeoutSeconds, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var revisions = ParseRevisions(root);
        var nextLink = TryGetCaseInsensitiveString(root, "@odata.nextLink");

        var minChangedDate = revisions.Count > 0 ? revisions.Min(revision => revision.ChangedDate) : (DateTimeOffset?)null;
        var maxChangedDate = revisions.Count > 0 ? revisions.Max(revision => revision.ChangedDate) : (DateTimeOffset?)null;

        _logger.LogInformation(
            "OData revisions page loaded. Count={Count} HasNextLink={HasNextLink} MinChangedDate={MinChangedDate} MaxChangedDate={MaxChangedDate}",
            revisions.Count,
            !string.IsNullOrWhiteSpace(nextLink),
            minChangedDate,
            maxChangedDate);

        return new ReportingRevisionsResult(revisions, NormalizeToken(nextLink));
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
            var page = await GetRevisionsAsync(null, continuationToken, ReportingExpandMode.None, cancellationToken);
            var pageRows = page.Revisions.Where(revision => revision.WorkItemId == workItemId).ToList();
            rows.AddRange(pageRows);

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

            if (pageRows.Count == 0)
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

    private static string BuildPageUrl(TfsConfigEntity config, DateTimeOffset? startDateTime, string? continuationToken)
    {
        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            return continuationToken!;
        }

        var baseUrl = config.AnalyticsODataBaseUrl.TrimEnd('/');
        var entitySet = config.AnalyticsODataEntitySetPath.Trim().TrimStart('/');
        var builder = new List<string> { $"$top={DefaultTop}" };
        if (startDateTime.HasValue)
        {
            var timestamp = Uri.EscapeDataString(startDateTime.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            builder.Add($"$filter=ChangedDate ge {timestamp}");
        }

        return $"{baseUrl}/{entitySet}?{string.Join("&", builder)}";
    }

    private List<WorkItemRevision> ParseRevisions(JsonElement root)
    {
        var revisions = new List<WorkItemRevision>();
        if (!TryGetCaseInsensitiveProperty(root, "value", out var valueArray) || valueArray.ValueKind != JsonValueKind.Array)
        {
            return revisions;
        }

        var warningCount = 0;
        foreach (var row in valueArray.EnumerateArray())
        {
            var parsed = TryParseRevisionRow(row);
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

        return revisions;
    }

    private WorkItemRevision? TryParseRevisionRow(JsonElement row)
    {
        var workItemId = TryReadInt(row, WorkItemIdAliases);
        var revisionNumber = TryReadInt(row, RevisionAliases);
        var changedDate = TryReadDate(row, ChangedDateAliases);
        if (!workItemId.HasValue || !revisionNumber.HasValue || !changedDate.HasValue)
        {
            return null;
        }

        return new WorkItemRevision
        {
            WorkItemId = workItemId.Value,
            RevisionNumber = revisionNumber.Value,
            WorkItemType = ReadString(row, "WorkItemType", "System_WorkItemType"),
            Title = ReadString(row, "Title", "System_Title"),
            State = ReadString(row, "State", "System_State"),
            Reason = ReadNullableString(row, "Reason", "System_Reason"),
            IterationPath = ReadString(row, "IterationPath", "System_IterationPath"),
            AreaPath = ReadString(row, "AreaPath", "System_AreaPath"),
            CreatedDate = TryReadDate(row, "CreatedDate", "System_CreatedDate", "System.CreatedDate"),
            ChangedDate = changedDate.Value.ToUniversalTime(),
            ClosedDate = TryReadDate(row, "ClosedDate", "Microsoft_VSTS_Common_ClosedDate", "Microsoft.VSTS.Common.ClosedDate"),
            Effort = TryReadDouble(row, "Effort") ?? TryReadDouble(row, "Microsoft_VSTS_Scheduling_Effort"),
            Tags = ReadNullableString(row, "Tags", "System_Tags"),
            Severity = ReadNullableString(row, "Severity", "Microsoft_VSTS_Common_Severity", "Microsoft.VSTS.Common.Severity"),
            ChangedBy = ReadNullableString(row, "ChangedBy", "System_ChangedBy"),
            FieldDeltas = null,
            RelationDeltas = null
        };
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
}
