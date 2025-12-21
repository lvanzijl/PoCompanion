using Microsoft.AspNetCore.Http;
using PoTool.Api.Middleware;

namespace PoTool.Api.Services;

/// <summary>
/// Service to access the current request's PAT from HttpContext.
/// PAT is extracted from X-TFS-PAT header by PatAuthenticationMiddleware.
/// </summary>
public class PatAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PatAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the PAT from the current HTTP request context.
    /// This method is thread-safe as HttpContext is scoped to the current request.
    /// </summary>
    /// <returns>The PAT if present in the request, otherwise null.</returns>
    public string? GetPat()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return null;
        }

        if (context.Items.TryGetValue(PatAuthenticationMiddleware.PatContextKey, out var pat))
        {
            return pat as string;
        }

        return null;
    }
}
