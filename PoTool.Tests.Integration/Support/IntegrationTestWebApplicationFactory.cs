using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            // Remove the real database context
            services.RemoveAll<DbContextOptions<PoToolDbContext>>();
            services.RemoveAll<PoToolDbContext>();

            // Add in-memory database for testing
            services.AddDbContext<PoToolDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });

            // Replace ITfsClient with mock implementation
            services.RemoveAll<ITfsClient>();
            services.AddScoped<ITfsClient, MockTfsClient>();

            // Build service provider to ensure database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            dbContext.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}
