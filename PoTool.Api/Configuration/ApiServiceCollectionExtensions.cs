using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
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

        // Register DataSourceMode provider (reads from configuration)
        // This is a singleton because the mode is determined at startup and doesn't change
        services.AddSingleton<IDataSourceModeProvider, DataSourceModeProvider>();

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

        // Read the data source configuration setting
        // This single setting controls whether mock data or TFS data is used
        var useMockClient = configuration.GetValue<bool>("TfsIntegration:UseMockClient", false);

        // Register repositories based on data source configuration
        // When UseMockClient is false (TFS mode), always use the real WorkItemRepository
        // regardless of environment to ensure mock data is never used
        if (useMockClient && isDevelopment)
        {
            // Use in-memory dev repository with mock data only when explicitly configured
            // and in development mode
            services.AddSingleton<IWorkItemRepository, DevWorkItemRepository>();
        }
        else
        {
            // Use real database-backed repository for TFS mode or production
            services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        }
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IPullRequestRepository, PullRequestRepository>();
        services.AddScoped<IReleasePlanningRepository, ReleasePlanningRepository>();
        services.AddScoped<IPipelineRepository, Repositories.PipelineRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<Repositories.RepositoryRepository>();

        // Register Classification service
        services.AddScoped<IWorkItemClassificationService, WorkItemClassificationService>();

        // Register Work Item Read Providers
        // These provide mode-aware read access to work items (Live vs Cached)
        services.AddScoped<CachedWorkItemReadProvider>();
        services.AddScoped<LiveWorkItemReadProvider>();
        services.AddScoped<WorkItemReadProviderFactory>();

        // Register Release Planning services
        services.AddScoped<ConnectorDerivationService>();

        // Register Battleship mock data generation system only when mock client is enabled
        // When TFS mode is selected (UseMockClient=false), mock data services should not be registered
        if (useMockClient)
        {
            services.AddSingleton<BattleshipWorkItemGenerator>();
            services.AddSingleton<BattleshipDependencyGenerator>();
            services.AddSingleton<BattleshipPullRequestGenerator>();
            services.AddSingleton<BattleshipPipelineGenerator>();
            services.AddSingleton<MockDataValidator>();
            services.AddSingleton<BattleshipMockDataFacade>();
        }

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
        services.AddScoped<IEfConcurrencyGate, EfConcurrencyGate>();
        services.AddScoped<TfsConfigurationService>();
        services.AddScoped<TfsAuthenticationProvider>();
        services.AddScoped<ProfileFilterService>();

        // Register TFS throttling and request services (used by RealTfsClient)
        services.AddSingleton<TfsRequestThrottler>();
        services.AddScoped<TfsRequestSender>();

        // Always register HttpClientFactory for handlers that need it
        services.AddHttpClient();

        // Register TFS client based on configuration (useMockClient already read above)
        if (useMockClient)
        {
            // Use mock TFS client with predefined test data
            services.AddScoped<ITfsClient, MockTfsClient>();
        }
        else
        {
            // Use real TFS client that connects to Azure DevOps/TFS
            // Register named HttpClient for NTLM authentication

            // NTLM authentication client - WITH default Windows credentials
            services.AddHttpClient("TfsClient.NTLM")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseDefaultCredentials = true,
                    Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 5
                });

            // Register RealTfsClient as a regular scoped service
            // RealTfsClient uses IHttpClientFactory internally (via GetAuthenticatedHttpClient)
            // and has multiple constructor dependencies, so it doesn't follow the typed HttpClient pattern
            services.AddScoped<ITfsClient, RealTfsClient>();
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
                            "http://localhost:5291",  // Allow API self-reference for Swagger UI
                            "http://localhost:5292",  // Blazor WebAssembly dev server
                            "https://localhost:7215"  // Blazor WebAssembly HTTPS dev server
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
