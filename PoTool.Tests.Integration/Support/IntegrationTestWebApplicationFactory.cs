using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures in-memory database and mock TFS client.
/// </summary>
public class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private static int _databaseCounter = 0;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use Testing environment to trigger special configuration in Program.cs
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Add in-memory database for testing with a unique name per test
            // Using incrementing counter for better performance and debuggability
            var dbName = $"IntegrationTestDb_{Interlocked.Increment(ref _databaseCounter)}";
            services.AddDbContext<PoToolDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
                
                // Use SplitQuery to avoid cartesian explosion when loading multiple collections
                // This matches the production configuration
                options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            // Replace ITfsClient with mock implementation
            services.RemoveAll<ITfsClient>();
            services.AddScoped<ITfsClient, MockTfsClient>();
        });
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
