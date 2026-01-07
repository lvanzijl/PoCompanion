using System.Net;

namespace PoTool.Api.Services;

/// <summary>
/// Provides HTTP message handlers configured for NTLM authentication.
/// </summary>
public class TfsAuthenticationProvider
{
    /// <summary>
    /// Creates an HTTP message handler configured for NTLM authentication.
    /// </summary>
    /// <returns>Configured HttpMessageHandler with NTLM credentials.</returns>
    public HttpMessageHandler CreateNtlmHandler()
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
