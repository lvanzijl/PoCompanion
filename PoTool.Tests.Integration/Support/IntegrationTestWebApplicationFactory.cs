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
        // Set environment to Testing BEFORE services are configured
        // This prevents Program.cs from registering SQLite/SqlServer
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Add in-memory database for testing
            services.AddDbContext<PoToolDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });

            // Replace ITfsClient with mock implementation
            services.RemoveAll<ITfsClient>();
            services.AddScoped<ITfsClient, MockTfsClient>();
        });
    }
}
