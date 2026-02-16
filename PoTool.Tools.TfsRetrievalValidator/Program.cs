using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

var runDirectory = Path.Combine(AppContext.BaseDirectory, $"Run_{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}");
Directory.CreateDirectory(runDirectory);
var runLogPath = Path.Combine(runDirectory, "run.log");
var zipPath = Path.Combine(runDirectory, "tfs-dump.zip");
var dumpCollector = new TfsDumpCollector();
RawModelSnapshot? snapshot = null;

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environments.Production;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.ClearProviders();
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        options.SingleLine = true;
    });
    builder.AddProvider(new FileLoggerProvider(runLogPath));
});

var logger = loggerFactory.CreateLogger("PoTool.Tools.TfsRetrievalValidator");

try
{
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddSingleton(loggerFactory);
    services.AddLogging();
    services.AddOptions();
    services.Configure<TfsConfigEntity>(configuration.GetSection("Tfs"));
    services.Configure<ValidatorOptions>(configuration.GetSection("TfsRetrievalValidator"));
    services.AddOptions<RevisionIngestionPaginationOptions>()
        .Bind(configuration.GetSection("RevisionIngestionPagination"));

    services.AddSingleton<ITfsConfigurationService, AppSettingsTfsConfigurationService>();
    services.AddSingleton(dumpCollector);
    services.AddTransient<TfsCaptureHandler>();
    services.AddSingleton<TfsRequestThrottler>();
    services.AddScoped<TfsRequestSender>();

    services.AddHttpClient("TfsClient.NTLM")
        .AddHttpMessageHandler<TfsCaptureHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            UseDefaultCredentials = true,
            Credentials = CredentialCache.DefaultNetworkCredentials,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        });

    services.AddScoped<ITfsClient, RealTfsClient>();
    services.AddScoped<IRevisionTfsClient, RealRevisionTfsClient>();

    await using var provider = services.BuildServiceProvider().CreateAsyncScope();
    var scopedProvider = provider.ServiceProvider;

    var options = scopedProvider.GetRequiredService<IOptions<ValidatorOptions>>().Value;
    if (options.RootWorkItemId <= 0)
    {
        throw new InvalidOperationException("TfsRetrievalValidator:RootWorkItemId must be greater than zero.");
    }

    var tfsClient = scopedProvider.GetRequiredService<ITfsClient>();
    var revisionClient = scopedProvider.GetRequiredService<IRevisionTfsClient>();
    logger.LogInformation("Starting retrieval for RootWorkItemId={RootWorkItemId}", options.RootWorkItemId);

    var workItems = (await tfsClient.GetWorkItemsByRootIdsAsync(
            [options.RootWorkItemId],
            progressCallback: (step, total, message) =>
                logger.LogInformation("Work item retrieval progress {Step}/{Total}: {Message}", step, total, message)))
        .ToList();

    var allowedWorkItemIds = workItems.Select(item => item.TfsId).ToHashSet();
    var revisions = new List<WorkItemRevision>();
    string? continuationToken = null;
    var pageNumber = 0;
    var totalRawRevisions = 0;
    var pagesWithoutScopedRevisions = 0;
    string? reportingTerminationReason = null;
    string? reportingTerminationMessage = null;

    do
    {
        var page = await revisionClient.GetReportingRevisionsAsync(
            continuationToken: continuationToken,
            expandMode: ReportingExpandMode.Fields);

        pageNumber++;
        var scopedRevisions = page.Revisions
            .Where(revision => allowedWorkItemIds.Contains(revision.WorkItemId))
            .ToList();

        totalRawRevisions += page.Revisions.Count;
        if (scopedRevisions.Count == 0)
        {
            pagesWithoutScopedRevisions++;
        }

        revisions.AddRange(scopedRevisions);
        continuationToken = page.ContinuationToken;

        logger.LogInformation(
            "Revision page {PageNumber}: raw={RawCount}, scoped={ScopedCount}, hasMore={HasMore}",
            pageNumber,
            page.Revisions.Count,
            scopedRevisions.Count,
            continuationToken is not null);

        if (page.WasTerminatedEarly)
        {
            reportingTerminationReason = page.Termination?.Reason.ToString();
            reportingTerminationMessage = page.Termination?.Message;
            logger.LogWarning(
                "Revision retrieval terminated early. Reason={Reason}, Message={Message}",
                page.Termination?.Reason,
                page.Termination?.Message);
            break;
        }
    }
    while (continuationToken is not null);

    var hierarchyRelations = ReconstructHierarchy(workItems);

    logger.LogInformation(
        "Retrieved {WorkItemCount} work items, {RevisionCount} scoped revisions, {HierarchyCount} hierarchy relations",
        workItems.Count,
        revisions.Count,
        hierarchyRelations.Count);
    logger.LogInformation(
        "Revision pagination summary: pages={PageCount}, rawRevisions={RawRevisions}, scopedRevisions={ScopedRevisions}, pagesWithoutScopedRevisions={PagesWithoutScopedRevisions}",
        pageNumber,
        totalRawRevisions,
        revisions.Count,
        pagesWithoutScopedRevisions);

    if (pagesWithoutScopedRevisions > 0)
    {
        logger.LogWarning(
            "Detected {PagesWithoutScopedRevisions} pages without scoped revisions. This can indicate poor root filtering coverage or sparse historical data.",
            pagesWithoutScopedRevisions);
    }

    snapshot = new RawModelSnapshot(
        RetrievedAt: DateTimeOffset.Now,
        RootWorkItemId: options.RootWorkItemId,
        WorkItems: workItems,
        Revisions: revisions,
        HierarchyRelations: hierarchyRelations,
        RevisionPagesFetched: pageNumber,
        TotalRawRevisions: totalRawRevisions,
        PagesWithoutScopedRevisions: pagesWithoutScopedRevisions,
        ReportingTerminationReason: reportingTerminationReason,
        ReportingTerminationMessage: reportingTerminationMessage);

    logger.LogInformation("Run completed successfully. Output directory: {RunDirectory}", runDirectory);
}
catch (Exception ex)
{
    logger.LogError(ex, "Run failed.");
    Environment.ExitCode = 1;
}
finally
{
    snapshot ??= new RawModelSnapshot(
        RetrievedAt: DateTimeOffset.Now,
        RootWorkItemId: 0,
        WorkItems: [],
        Revisions: [],
        HierarchyRelations: [],
        RevisionPagesFetched: 0,
        TotalRawRevisions: 0,
        PagesWithoutScopedRevisions: 0,
        ReportingTerminationReason: null,
        ReportingTerminationMessage: null);
    await dumpCollector.WriteDumpAsync(zipPath, snapshot);
}

static List<HierarchyRelationSnapshot> ReconstructHierarchy(IEnumerable<WorkItemDto> workItems)
{
    var hierarchy = new List<HierarchyRelationSnapshot>();
    foreach (var workItem in workItems)
    {
        if (workItem.ParentTfsId.HasValue)
        {
            hierarchy.Add(new HierarchyRelationSnapshot(workItem.ParentTfsId.Value, workItem.TfsId, "ParentTfsId"));
        }

        if (workItem.Relations is null)
        {
            continue;
        }

        foreach (var relation in workItem.Relations)
        {
            if (relation.TargetWorkItemId.HasValue &&
                relation.LinkType.Contains("Hierarchy", StringComparison.OrdinalIgnoreCase))
            {
                hierarchy.Add(new HierarchyRelationSnapshot(workItem.TfsId, relation.TargetWorkItemId.Value, relation.LinkType));
            }
        }
    }

    return hierarchy
        .Distinct()
        .ToList();
}

internal sealed record ValidatorOptions
{
    public int RootWorkItemId { get; init; }
}

internal sealed record HierarchyRelationSnapshot(int SourceWorkItemId, int TargetWorkItemId, string RelationType);

internal sealed record RawModelSnapshot(
    DateTimeOffset RetrievedAt,
    int RootWorkItemId,
    IReadOnlyList<WorkItemDto> WorkItems,
    IReadOnlyList<WorkItemRevision> Revisions,
    IReadOnlyList<HierarchyRelationSnapshot> HierarchyRelations,
    int RevisionPagesFetched,
    int TotalRawRevisions,
    int PagesWithoutScopedRevisions,
    string? ReportingTerminationReason,
    string? ReportingTerminationMessage);

internal sealed class AppSettingsTfsConfigurationService(IOptions<TfsConfigEntity> options) : ITfsConfigurationService
{
    private readonly TfsConfigEntity _config = options.Value;

    public Task<TfsConfigEntity?> GetConfigEntityAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<TfsConfigEntity?>(_config);
    }

    public Task SaveConfigEntityAsync(TfsConfigEntity entity, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This tool uses read-only appsettings-based TFS configuration.");
    }
}

internal sealed class TfsCaptureHandler(TfsDumpCollector dumpCollector, ILogger<TfsCaptureHandler> logger) : DelegatingHandler
{
    private static long _sequence;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var requestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        HttpResponseMessage response;
        long durationMs;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            durationMs = (long)(DateTimeOffset.Now - startedAt).TotalMilliseconds;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            durationMs = (long)(DateTimeOffset.Now - startedAt).TotalMilliseconds;
            dumpCollector.AddResponse(new CapturedResponse(
                Sequence: Interlocked.Increment(ref _sequence),
                RequestMethod: request.Method.Method,
                RequestUrl: SanitizeRequestUrl(request.RequestUri),
                RequestedAtLocal: startedAt,
                DurationMs: durationMs,
                RequestHeaders: FlattenHeaders(request.Headers),
                ResponseStatusCode: 0,
                ResponseReasonPhrase: null,
                ResponseHeaders: new Dictionary<string, string>(),
                ResponseContentType: null,
                RequestBody: requestBody,
                ResponseBodyCaptured: false,
                ResponseBody: string.Empty,
                TransportError: ex.GetType().Name + ": " + ex.Message));
            throw;
        }

        if (response.Content is not null)
        {
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseBody = Encoding.UTF8.GetString(responseBytes);
            var responseContentType = response.Content.Headers.ContentType?.MediaType;
            var responseBodyIsJson = LooksLikeJson(responseBody, responseContentType);
            dumpCollector.AddResponse(new CapturedResponse(
                Sequence: Interlocked.Increment(ref _sequence),
                RequestMethod: request.Method.Method,
                RequestUrl: SanitizeRequestUrl(request.RequestUri),
                RequestedAtLocal: startedAt,
                DurationMs: durationMs,
                RequestHeaders: FlattenHeaders(request.Headers),
                ResponseStatusCode: (int)response.StatusCode,
                ResponseReasonPhrase: response.ReasonPhrase,
                ResponseHeaders: FlattenHeaders(response.Headers),
                ResponseContentType: responseContentType,
                RequestBody: requestBody,
                ResponseBodyCaptured: responseBodyIsJson,
                ResponseBody: responseBodyIsJson ? responseBody : string.Empty,
                TransportError: null));

            var bufferedContent = new ByteArrayContent(responseBytes);
            foreach (var header in response.Content.Headers)
            {
                bufferedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content.Dispose();
            response.Content = bufferedContent;
        }

        logger.LogTrace("Captured TFS response: {Method} {Url} -> {StatusCode}", request.Method, request.RequestUri, (int)response.StatusCode);
        return response;
    }

    private static bool LooksLikeJson(string body, string? mediaType)
    {
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static IReadOnlyDictionary<string, string> FlattenHeaders(HttpHeaders headers)
    {
        return headers.ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
    }

    private static string SanitizeRequestUrl(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return string.Empty;
        }

        var uriBuilder = new UriBuilder(requestUri)
        {
            UserName = string.Empty,
            Password = string.Empty
        };

        var query = uriBuilder.Query;
        if (query.Contains("continuationToken=", StringComparison.OrdinalIgnoreCase))
        {
            var parts = query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    var separatorIndex = part.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        return part;
                    }

                    var key = part[..separatorIndex];
                    return key.Equals("continuationToken", StringComparison.OrdinalIgnoreCase)
                        ? $"{key}=<redacted>"
                        : part;
                });
            uriBuilder.Query = string.Join("&", parts);
        }

        return uriBuilder.Uri.ToString();
    }
}

internal sealed class TfsDumpCollector
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ConcurrentQueue<CapturedResponse> _responses = new();

    public void AddResponse(CapturedResponse response)
    {
        _responses.Enqueue(response);
    }

    public async Task WriteDumpAsync(string zipPath, RawModelSnapshot snapshot)
    {
        await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);
        var orderedResponses = _responses.OrderBy(item => item.Sequence).ToList();

        foreach (var response in orderedResponses)
        {
            var metadataEntry = archive.CreateEntry($"responses/{response.Sequence:D5}.metadata.json", CompressionLevel.Optimal);
            await using (var metadataStream = metadataEntry.Open())
            {
                await JsonSerializer.SerializeAsync(metadataStream, response with { ResponseBody = string.Empty }, JsonOptions);
            }

            if (!response.ResponseBodyCaptured)
            {
                continue;
            }

            var bodyEntry = archive.CreateEntry($"responses/{response.Sequence:D5}.json", CompressionLevel.Optimal);
            await using (var bodyStream = new StreamWriter(bodyEntry.Open(), Encoding.UTF8))
            {
                await bodyStream.WriteAsync(AnonymizeJson(response.ResponseBody));
            }
        }

        var modelEntry = archive.CreateEntry("model-snapshot.json", CompressionLevel.Optimal);
        await using (var modelStream = modelEntry.Open())
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var anonymized = AnonymizeJson(json);
            await using var writer = new StreamWriter(modelStream, Encoding.UTF8);
            await writer.WriteAsync(anonymized);
        }

        var summaryEntry = archive.CreateEntry("analysis-summary.json", CompressionLevel.Optimal);
        await using var summaryStream = summaryEntry.Open();
        await JsonSerializer.SerializeAsync(summaryStream, BuildSummary(snapshot, orderedResponses), JsonOptions);
    }

    private static DumpAnalysisSummary BuildSummary(
        RawModelSnapshot snapshot,
        IReadOnlyList<CapturedResponse> responses)
    {
        var statusCounts = responses
            .GroupBy(response => response.ResponseStatusCode)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key.ToString(CultureInfo.InvariantCulture), group => group.Count());
        var transportErrorCount = responses.Count(response => !string.IsNullOrWhiteSpace(response.TransportError));
        if (transportErrorCount > 0)
        {
            statusCounts["transport_error"] = transportErrorCount;
        }

        var endpointFailures = responses
            .Where(response => response.ResponseStatusCode >= 400 || !string.IsNullOrWhiteSpace(response.TransportError))
            .GroupBy(response => NormalizeEndpoint(response.RequestUrl))
            .Select(group => new EndpointFailureSummary(group.Key, group.Count(), group.First().ResponseStatusCode))
            .OrderByDescending(summary => summary.Count)
            .ThenBy(summary => summary.Endpoint, StringComparer.Ordinal)
            .Take(10)
            .ToList();

        var recommendations = new List<string>();
        if (responses.Any(response => response.ResponseStatusCode is 401 or 403))
        {
            recommendations.Add("Validate NTLM identity and TFS permissions for the configured collection/project.");
        }

        if (responses.Any(response => response.ResponseStatusCode is 429 or 503))
        {
            recommendations.Add("Tune retry/backoff and request throttling for reporting and work item endpoints.");
        }

        if (transportErrorCount > 0)
        {
            recommendations.Add("Transport-level failures detected; validate DNS/connectivity from validator host and TFS base URL reachability.");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ReportingTerminationReason))
        {
            recommendations.Add("Investigate reporting pagination termination and consider fallback or smaller windows for continuation token progress.");
        }

        if (snapshot.RevisionPagesFetched > 0 &&
            snapshot.PagesWithoutScopedRevisions > snapshot.RevisionPagesFetched / 2)
        {
            recommendations.Add("High number of pages without scoped revisions suggests filter mismatch; verify root hierarchy coverage and revision field projection.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("No obvious transport failures detected; inspect model-snapshot consistency against expected hierarchy and revision counts.");
        }

        return new DumpAnalysisSummary(
            GeneratedAt: DateTimeOffset.Now,
            RequestCount: responses.Count,
            ErrorCount: responses.Count(response => response.ResponseStatusCode >= 400 || !string.IsNullOrWhiteSpace(response.TransportError)),
            StatusCodeCounts: statusCounts,
            TopFailingEndpoints: endpointFailures,
            RevisionPagesFetched: snapshot.RevisionPagesFetched,
            TotalRawRevisions: snapshot.TotalRawRevisions,
            ScopedRevisions: snapshot.Revisions.Count,
            PagesWithoutScopedRevisions: snapshot.PagesWithoutScopedRevisions,
            ReportingTerminationReason: snapshot.ReportingTerminationReason,
            ReportingTerminationMessage: snapshot.ReportingTerminationMessage,
            Recommendations: recommendations);
    }

    private static string NormalizeEndpoint(string requestUrl)
    {
        if (Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath;
        }

        var queryIndex = requestUrl.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? requestUrl[..queryIndex] : requestUrl;
    }

    private static string AnonymizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch
        {
            return json;
        }

        if (node is null)
        {
            return json;
        }

        ScrubNode(node);
        return node.ToJsonString(JsonOptions);
    }

    private static void ScrubNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(item => item.Key).ToList();
            foreach (var key in keys)
            {
                if (obj[key] is null)
                {
                    continue;
                }

                if (IsTitleProperty(key))
                {
                    obj[key] = BuildReplacement("Title", TryGetNodeText(obj[key]!));
                    continue;
                }

                if (IsDescriptionProperty(key))
                {
                    obj[key] = BuildReplacement("Description", TryGetNodeText(obj[key]!));
                    continue;
                }

                ScrubNode(obj[key]!);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    ScrubNode(child);
                }
            }
        }
    }

    private static bool IsTitleProperty(string propertyName)
    {
        return propertyName.Equals("title", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("System.Title", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDescriptionProperty(string propertyName)
    {
        return propertyName.Equals("description", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("System.Description", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReplacement(string fieldName, string? currentValue)
    {
        var length = currentValue?.Length ?? 0;
        return $"[{fieldName} removed] (len={length})";
    }

    private static string? TryGetNodeText(JsonNode node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            return value.ToJsonString();
        }

        return node.ToJsonString();
    }
}

internal sealed record CapturedResponse(
    long Sequence,
    string RequestMethod,
    string RequestUrl,
    DateTimeOffset RequestedAtLocal,
    long DurationMs,
    IReadOnlyDictionary<string, string> RequestHeaders,
    int ResponseStatusCode,
    string? ResponseReasonPhrase,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string? ResponseContentType,
    string? RequestBody,
    bool ResponseBodyCaptured,
    string ResponseBody,
    string? TransportError);

internal sealed record DumpAnalysisSummary(
    DateTimeOffset GeneratedAt,
    int RequestCount,
    int ErrorCount,
    IReadOnlyDictionary<string, int> StatusCodeCounts,
    IReadOnlyList<EndpointFailureSummary> TopFailingEndpoints,
    int RevisionPagesFetched,
    int TotalRawRevisions,
    int ScopedRevisions,
    int PagesWithoutScopedRevisions,
    string? ReportingTerminationReason,
    string? ReportingTerminationMessage,
    IReadOnlyList<string> Recommendations);

internal sealed record EndpointFailureSummary(string Endpoint, int Count, int SampleStatusCode);

internal sealed class FileLoggerProvider(string logPath) : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer = new(
        new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read),
        Encoding.UTF8)
    {
        AutoFlush = true
    };

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _sync);

    public void Dispose() => _writer.Dispose();
}

internal sealed class FileLogger(string categoryName, StreamWriter writer, object sync) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (sync)
        {
            writer.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {categoryName}: {message}");
            if (exception is not null)
            {
                writer.WriteLine(exception);
            }
        }
    }
}
