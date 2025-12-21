using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PoTool.Api.Middleware;

/// <summary>
/// Middleware to extract PAT from request headers and make it available to services.
/// PAT is sent via X-TFS-PAT header from client and stored in HttpContext.Items for request duration only.
/// </summary>
public class PatAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    public const string PatHeaderName = "X-TFS-PAT";
    public const string PatContextKey = "TFS_PAT";

    public PatAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract PAT from header if present
        if (context.Request.Headers.TryGetValue(PatHeaderName, out var patValue))
        {
            // Store PAT in HttpContext.Items for this request only
            // It will be automatically cleared when request completes
            context.Items[PatContextKey] = patValue.ToString();
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the PAT authentication middleware.
/// </summary>
public static class PatAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UsePatAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PatAuthenticationMiddleware>();
    }
}
