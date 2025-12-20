using System.Net;
using System.Text;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

/// <summary>
/// Provides HTTP message handlers configured for TFS authentication.
/// </summary>
public class TfsAuthenticationProvider
{
    /// <summary>
    /// Creates an HttpClient configured for the specified authentication mode.
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="pat">Personal Access Token (required for PAT mode).</param>
    /// <returns>Configured HttpClient.</returns>
    public HttpClient CreateAuthenticatedClient(TfsConfigEntity config, string? pat = null)
    {
        var handler = CreateAuthHandler(config.AuthMode, pat);
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        return client;
    }

    /// <summary>
    /// Creates an HTTP message handler configured for the specified authentication mode.
    /// </summary>
    /// <param name="mode">Authentication mode.</param>
    /// <param name="pat">Personal Access Token (required for PAT mode).</param>
    /// <returns>Configured HttpMessageHandler.</returns>
    public HttpMessageHandler CreateAuthHandler(TfsAuthMode mode, string? pat = null)
    {
        return mode switch
        {
            TfsAuthMode.Pat => CreatePatHandler(pat),
            TfsAuthMode.Ntlm => CreateNtlmHandler(),
            _ => throw new NotSupportedException($"Authentication mode {mode} is not supported.")
        };
    }

    /// <summary>
    /// Configures an existing HttpClient with PAT authentication.
    /// </summary>
    /// <param name="client">HttpClient to configure.</param>
    /// <param name="pat">Personal Access Token.</param>
    public void ConfigurePatAuthentication(HttpClient client, string pat)
    {
        if (string.IsNullOrEmpty(pat))
        {
            throw new ArgumentException("PAT cannot be null or empty for PAT authentication.", nameof(pat));
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }

    private HttpMessageHandler CreatePatHandler(string? pat)
    {
        // For PAT mode, we'll use default handler and configure auth headers separately
        // This allows for easier testing and flexibility
        return new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };
    }

    private HttpMessageHandler CreateNtlmHandler()
    {
        return new HttpClientHandler
        {
            UseDefaultCredentials = true,
            Credentials = CredentialCache.DefaultNetworkCredentials,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };
    }
}
