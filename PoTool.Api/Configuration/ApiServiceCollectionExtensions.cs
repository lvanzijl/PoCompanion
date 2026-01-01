using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Filtering;
using PoTool.Core.Health;

namespace PoTool.Api.Configuration;

/// <summary>
/// Extension methods for configuring API services in the dependency injection container.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds all PoTool API services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="isDevelopment">Whether the application is running in development mode.</param>
    /// <param name="configureDatabase">Optional action to configure the database. If not provided, uses default SQLite/SqlServer configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPoToolApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment,
        Action<IServiceCollection, IConfiguration>? configureDatabase = null)
    {
        // Add controllers and OpenAPI
        services.AddControllers();
        services.AddOpenApi();

        // Add OpenAPI/Swagger support with NSwag
        services.AddOpenApiDocument(config =>
        {
            config.Title = "PoTool API";
            config.Version = "v1";
            config.Description = "API for PO Companion work item management";
        });

        // Add Mediator (source-generated)
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        // Configure database - allow override for testing
        if (configureDatabase != null)
        {
            configureDatabase(services, configuration);
        }
        else
        {
            // Default database configuration: prefer SqlServer if connection string present, otherwise SQLite
            var sqlServerConn = configuration.GetConnectionString("SqlServerConnection");
            if (!string.IsNullOrWhiteSpace(sqlServerConn))
            {
                services.AddDbContext<PoToolDbContext>(options =>
                    options.UseSqlServer(sqlServerConn));
            }
            else
            {
                services.AddDbContext<PoToolDbContext>(options =>
                {
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                        ?? "Data Source=potool.db");
                    
                    // Suppress pending model changes warning in development for exploratory testing
                    if (isDevelopment)
                    {
                        options.ConfigureWarnings(warnings =>
                            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    }
                });
            }
        }

        // Register repositories
        if (isDevelopment)
        {
            // Use in-memory dev repository to allow frontend development without TFS or DB
            services.AddSingleton<IWorkItemRepository, DevWorkItemRepository>();
        }
        else
        {
            services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        }
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IPullRequestRepository, PullRequestRepository>();

        // Register new Battleship mock data generation system
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        
        // Keep old mock data providers for backward compatibility during migration
        services.AddSingleton<MockDataProvider>();
        services.AddSingleton<MockPullRequestDataProvider>();

        // Register validators
        services.AddScoped<WorkItemParentProgressValidator>();
        services.AddScoped<WorkItemInProgressWithoutEffortValidator>();
        services.AddScoped<IWorkItemValidator>(provider =>
        {
            var validators = new List<IWorkItemValidator>
            {
                provider.GetRequiredService<WorkItemParentProgressValidator>(),
                provider.GetRequiredService<WorkItemInProgressWithoutEffortValidator>()
            };
            return new CompositeWorkItemValidator(validators);
        });

        // Register Core business logic services
        services.AddScoped<WorkItemFilterer>();
        services.AddScoped<BacklogHealthCalculator>();

        // Register TFS configuration and client
        services.AddDataProtection();
        services.AddScoped<TfsConfigurationService>();
        services.AddScoped<TfsAuthenticationProvider>();
        services.AddScoped<ProfileFilterService>();
        
        // Register PAT accessor to retrieve PAT from request context
        services.AddHttpContextAccessor();
        services.AddScoped<PatAccessor>();
        
        // Register TFS client based on configuration
        var useMockClient = configuration.GetValue<bool>("TfsIntegration:UseMockClient", false);
        
        if (useMockClient)
        {
            // Use mock TFS client with predefined test data
            services.AddScoped<ITfsClient, MockTfsClient>();
        }
        else
        {
            // Use real TFS client that connects to Azure DevOps/TFS
            services.AddHttpClient<ITfsClient, RealTfsClient>();
        }

        // Register background services
        services.AddSingleton<WorkItemSyncService>();
        services.AddHostedService(provider => provider.GetRequiredService<WorkItemSyncService>());
        services.AddHostedService<EffortEstimationNotificationService>();

        // Add SignalR
        services.AddSignalR();

        // Add CORS for Blazor client
        services.AddCors(options =>
        {
            options.AddPolicy("AllowBlazorClient", policy =>
            {
                if (isDevelopment)
                {
                    // In development, restrict to known local origins and enable credentials for SignalR
                    policy.WithOrigins(
                            "https://localhost:5001",
                            "http://localhost:5000",
                            "http://localhost:5291"  // Allow API self-reference for Swagger UI
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
            });
        });

        return services;
    }
}
