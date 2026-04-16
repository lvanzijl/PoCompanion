using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;

namespace PoTool.Tests.Unit.Support;

internal static class EndpointMetadataTestHostFactory
{
    public static WebApplication CreateConfiguredApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.FullName,
            ContentRootPath = GetRepositoryRoot(),
            EnvironmentName = "Testing"
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TfsIntegration:UseMockClient"] = "true"
        });

        builder.Services.AddPoToolApiServices(
            builder.Configuration,
            isDevelopment: false,
            configureDatabase: (services, _) =>
            {
                services.AddDbContext<PoToolDbContext>(options =>
                    options.UseInMemoryDatabase($"endpoint-metadata-{Guid.NewGuid():N}"));
            });

        var app = builder.Build();
        app.MapPoToolEndpoints();
        return app;
    }

    public static IEnumerable<RouteEndpoint> GetManagedRouteEndpoints(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => !DataSourceModeConfiguration.ShouldBypassMiddleware(endpoint.RoutePattern.RawText));
    }

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : normalized;
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
