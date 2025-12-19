using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures in-memory database and mock TFS client.
/// </summary>
public class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context options that were added by the API
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PoToolDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<PoToolDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });

            // Replace ITfsClient with mock implementation
            services.RemoveAll<ITfsClient>();
            services.AddScoped<ITfsClient, MockTfsClient>();
        });

        // Initialize the database after the host is built
        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure database is created after the host is built
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }
}
