using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PoTool.Client.Services;

namespace PoTool.Client.Handlers;

/// <summary>
/// HTTP message handler that adds PAT from secure storage to outgoing API requests.
/// Adds X-TFS-PAT header to all requests so the API can authenticate with TFS/Azure DevOps.
/// </summary>
public class PatHeaderHandler : DelegatingHandler
{
    private readonly ISecureStorageService _secureStorage;
    private const string PatHeaderName = "X-TFS-PAT";
    private const string PatStorageKey = "tfs_pat";

    public PatHeaderHandler(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Get PAT from secure storage on each request
        // Note: Not cached intentionally for security - PAT should be retrieved fresh from
        // secure storage on each request to ensure it's always current and to minimize
        // time PAT is held in memory. SecureStorage retrieval is fast (platform API call).
        var pat = await _secureStorage.GetAsync(PatStorageKey);

        // Add PAT header if available
        if (!string.IsNullOrEmpty(pat))
        {
            request.Headers.Add(PatHeaderName, pat);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
