using System.Net.Http;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Settings;
using PoTool.Shared.Exceptions;
using PoTool.Core.Contracts;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation - Core infrastructure.
/// Contains constructor, fields, constants, and basic HTTP client configuration.
/// </summary>
public partial class RealTfsClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITfsConfigurationService _configService;
    private readonly ILogger<RealTfsClient> _logger;
    private readonly TfsRequestThrottler _throttler;
    private readonly TfsRequestSender _requestSender;
    private const int MaxRetries = 3;

    // ID offset for release pipelines/runs to avoid collision with build IDs
    private const int ReleaseIdOffset = 100000;

    // TFS field paths
    private const string TfsFieldEffort = "Microsoft.VSTS.Scheduling.Effort";
    private const string TfsFieldStoryPoints = "Microsoft.VSTS.Scheduling.StoryPoints";
    private const string TfsFieldState = "System.State";

    // Required work item fields for queries
    private static readonly string[] RequiredWorkItemFields = new[]
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.AreaPath",
        "System.IterationPath",
        "System.Description",
        "System.CreatedDate",
        TfsFieldEffort,
        TfsFieldStoryPoints
    };

    // Batch size for Work Items Batch API calls
    // Azure DevOps supports up to 200 work items per batch for optimal performance
    // Larger batches (up to 500) may work but could impact response time
    public const int WorkItemBatchSize = 200;

    // Ancestor completion safety limits
    // MaxAncestorDepth: Prevents infinite loops in case of circular references or very deep hierarchies
    // Typical org hierarchies: Goal (1) → Objective (2) → Epic (3) → Feature (4) → PBI (5) → Task (6) = 6 levels
    // Setting to 20 provides comfortable headroom while preventing runaway scenarios
    private const int MaxAncestorDepth = 20;
    
    // MaxAncestorCount: Caps total ancestors to add, preventing excessive API calls
    // In practice, most hierarchies have < 100 ancestors
    // Setting to 1000 handles large org structures while maintaining reasonable performance
    private const int MaxAncestorCount = 1000;

    public RealTfsClient(
        IHttpClientFactory httpClientFactory,
        ITfsConfigurationService configService,
        ILogger<RealTfsClient> logger,
        TfsRequestThrottler throttler,
        TfsRequestSender requestSender)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
        _throttler = throttler;
        _requestSender = requestSender;
    }

    /// <summary>
    /// Gets an HttpClient configured for NTLM authentication.
    /// Uses named HttpClient from IHttpClientFactory to ensure correct handler configuration.
    /// Per-request timeouts are handled via CancellationToken, not HttpClient.Timeout property.
    /// </summary>
    /// <returns>Configured HttpClient with NTLM authentication.</returns>
    private HttpClient GetAuthenticatedHttpClient()
    {
        // Get NTLM-configured client (with UseDefaultCredentials=true in handler)
        var client = _httpClientFactory.CreateClient("TfsClient.NTLM");

        // NOTE: Do NOT set client.Timeout here - factory-managed clients should not have their
        // timeout mutated. Per-request timeouts are handled via CancellationTokenSource.

        _logger.LogDebug("Using NTLM-authenticated HttpClient for TFS request");

        return client;
    }

    /// <summary>
    /// Builds a collection-scoped URL (no project in path).
    /// Use for: _apis/projects, _apis/wit/fields, _apis/wit/workitems?ids=...
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="relativePath">Path relative to collection root (e.g., "_apis/projects").</param>
    /// <returns>Full URL including api-version.</returns>
    private string CollectionUrl(TfsConfigEntity config, string relativePath)
    {
        ValidateCollectionUrl(config.Url);

        // Ensure relativePath doesn't start with /
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{path}{separator}api-version={config.ApiVersion}";
    }

    /// <summary>
    /// Builds a project-scoped URL (project in path).
    /// Use for: WIQL, Git repositories, pull requests, build/release pipelines.
    /// Project name is URL-encoded to support spaces.
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="relativePath">Path relative to project (e.g., "_apis/wit/wiql").</param>
    /// <returns>Full URL including api-version.</returns>
    private string ProjectUrl(TfsConfigEntity config, string relativePath)
    {
        ValidateCollectionUrl(config.Url);

        // URL-encode project name to support spaces and special characters
        var encodedProject = Uri.EscapeDataString(config.Project);
        // Ensure relativePath doesn't start with /
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{encodedProject}/{path}{separator}api-version={config.ApiVersion}";
    }

    /// <summary>
    /// Validates that the TFS URL is a collection root (not a project URL).
    /// Expected format: https://server/tfs/DefaultCollection or https://dev.azure.com/org
    /// </summary>
    /// <param name="url">The TFS URL to validate.</param>
    private void ValidateCollectionUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new TfsConfigurationException("TFS URL cannot be empty");
        }

        // Basic validation that it's a valid URI
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new TfsConfigurationException($"TFS URL is not a valid absolute URI: {url}");
        }

        // Check for common mistakes - project URLs typically have _apis or project-specific paths
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Contains("/_apis/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "TFS URL appears to include an API path segment (_apis). " +
                "Expected collection root (e.g., https://server/tfs/DefaultCollection), got: {Url}", url);
        }

        // For Azure DevOps Services, validate format
        if (uri.Host.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1)
            {
                _logger.LogWarning(
                    "Azure DevOps URL may include project name in path. " +
                    "Expected organization root (e.g., https://dev.azure.com/org), got: {Url}", url);
            }
        }
    }
}
