using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Api.Services.Sync;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Core.WorkItems.Filtering;
using PoTool.Core.Health;
using PoTool.Client.Services;
using PoTool.Integrations.Tfs.Diagnostics;

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

        // Configure revision ingestion diagnostics options (runtime reloadable)
        services.AddOptions<RevisionIngestionDiagnosticsOptions>()
            .Bind(configuration.GetSection("RevisionIngestionDiagnostics"));
        services.AddOptions<RevisionIngestionPaginationOptions>()
            .Bind(configuration.GetSection("RevisionIngestionPagination"))
            .Validate(
                options => options.MaxEmptyPages >= 1 &&
                           options.MaxNoProgressPages >= 1 &&
                           options.MaxProgressWithoutDataPages >= 1 &&
                           options.MaxTotalPages >= 1 &&
                           options.MaxPageRetries >= 0 &&
                           options.RetryBackoffSeconds >= 0 &&
                           options.RetryBackoffJitterSeconds >= 0 &&
                           options.FallbackBatchSize >= 1 &&
                           options.ODataTop >= 1 &&
                           options.ODataSeekPageSize >= 1 &&
                           options.RetryMaxIterations >= 1 &&
                           options.RetryOverlapMinutes >= 0 &&
                           options.ProgressEpsilonSeconds >= 0,
                "Revision ingestion pagination configuration has invalid bounds.")
            .ValidateOnStart();
        services.AddOptions<RevisionIngestionPersistenceOptimizationOptions>()
            .Bind(configuration.GetSection("RevisionIngestionPersistenceOptimization"))
            .Validate(
                options => isDevelopment || !string.Equals(options.SqliteSynchronous, "OFF", StringComparison.OrdinalIgnoreCase),
                "SqliteSynchronous=OFF is only allowed in Development.")
            .ValidateOnStart();

        services.AddSingleton<SqlitePragmaConnectionInterceptor>();

        // Configure database - allow override for testing
        if (configureDatabase != null)
        {
            configureDatabase(services, configuration);
        }
        else
        {
            // Default database configuration: prefer SqlServer if connection string present, otherwise SQLite
            // Database is used for configuration persistence (profiles, products, teams) only
            var sqlServerConn = configuration.GetConnectionString("SqlServerConnection");
            if (!string.IsNullOrWhiteSpace(sqlServerConn))
            {
                services.AddDbContext<PoToolDbContext>((provider, options) =>
                {
                    options.UseSqlServer(sqlServerConn, sqlOptions =>
                    {
                        // Use SplitQuery to avoid cartesian explosion when loading multiple collections
                        // This is the recommended approach for queries with multiple Include statements
                        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    options.AddInterceptors(provider.GetRequiredService<SqlitePragmaConnectionInterceptor>());
                });
            }
            else
            {
                services.AddDbContext<PoToolDbContext>((provider, options) =>
                {
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                        ?? "Data Source=potool.db", sqliteOptions =>
                    {
                        // Use SplitQuery to avoid cartesian explosion when loading multiple collections
                        // This is the recommended approach for queries with multiple Include statements
                        sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    options.AddInterceptors(provider.GetRequiredService<SqlitePragmaConnectionInterceptor>());

                    // Configure warnings
                    options.ConfigureWarnings(warnings =>
                    {
                        // Suppress pending model changes warning in development for exploratory testing
                        if (isDevelopment)
                        {
                            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                        }
                        
                        // Suppress SQLite PRAGMA foreign_keys transaction warnings (Event ID: 20410)
                        // SQLite migrations that alter tables require temporarily disabling foreign keys,
                        // which cannot be done inside a transaction. This is expected SQLite behavior
                        // and does not indicate an issue with the migrations.
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.NonTransactionalMigrationOperationWarning);
                    });
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
        services.AddScoped<IRepositoryConfigRepository, Repositories.RepositoryRepository>();
        services.AddScoped<ICacheStateRepository, CacheStateRepository>();

        // Register Cache Management Service (insights, granular reset, validation)
        services.AddScoped<CacheManagementService>();

        // Register Classification service
        services.AddScoped<IWorkItemClassificationService, WorkItemClassificationService>();

        // Register Work Item State Classification service
        services.AddScoped<IWorkItemStateClassificationService, WorkItemStateClassificationService>();

        // Register Sync Pipeline services (Stages 1-11)
        services.AddScoped<WorkItemSyncStage>();
        services.AddScoped<TeamSprintSyncStage>();
        services.AddScoped<RevisionSyncStage>();
        services.AddScoped<WorkItemRelationshipSnapshotStage>();
        services.AddScoped<WorkItemResolutionSyncStage>();
        services.AddScoped<SprintTrendProjectionSyncStage>();
        services.AddScoped<PullRequestSyncStage>();
        services.AddScoped<PipelineSyncStage>();
        services.AddScoped<ValidationComputeStage>();
        services.AddScoped<MetricsComputeStage>();
        services.AddScoped<FinalizeCacheStage>();
        services.AddSingleton<ISyncPipeline, SyncPipelineRunner>();

        // Register SignalR broadcaster for sync progress
        services.AddSingleton<ISyncProgressBroadcaster, SyncProgressBroadcaster>();
        
        // Register SignalR broadcaster for TFS config progress
        services.AddSingleton<ITfsConfigProgressBroadcaster, TfsConfigProgressBroadcaster>();

        // Register Data Source Mode Provider (for switching between Live and Cache)
        services.AddScoped<IDataSourceModeProvider, DataSourceModeProvider>();

        // Register Current Profile Provider (for getting active ProductOwner ID)
        services.AddScoped<ICurrentProfileProvider, CurrentProfileProvider>();

        // Register Live Read Providers with keyed services
        services.AddKeyedScoped<IWorkItemReadProvider, LiveWorkItemReadProvider>("Live");
        services.AddKeyedScoped<IPullRequestReadProvider, LivePullRequestReadProvider>("Live");
        services.AddKeyedScoped<IPipelineReadProvider, LivePipelineReadProvider>("Live");

        // Register Cached Read Providers with keyed services
        services.AddKeyedScoped<IWorkItemReadProvider, CachedWorkItemReadProvider>("Cached");
        services.AddKeyedScoped<IPullRequestReadProvider, CachedPullRequestReadProvider>("Cached");
        services.AddKeyedScoped<IPipelineReadProvider, CachedPipelineReadProvider>("Cached");

        // Register the factory for data-source-aware provider resolution
        services.AddScoped<DataSourceAwareReadProviderFactory>();

        // Register lazy wrappers that delay provider resolution until method calls
        // This ensures DataSourceModeMiddleware has set the correct mode before resolving
        // the actual Live or Cached provider from the factory
        services.AddScoped<IWorkItemReadProvider, LazyWorkItemReadProvider>();
        services.AddScoped<IPullRequestReadProvider, LazyPullRequestReadProvider>();
        services.AddScoped<IPipelineReadProvider, LazyPipelineReadProvider>();

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

        // Legacy validators are being phased out in favor of hierarchical validation
        // WorkItemInProgressWithoutEffortValidator is replaced by PbiEffortEmptyRule (RC-2)
        services.AddScoped<WorkItemParentProgressValidator>();
        services.AddScoped<IWorkItemValidator>(provider =>
        {
            var validators = new List<IWorkItemValidator>
            {
                provider.GetRequiredService<WorkItemParentProgressValidator>()
                // WorkItemInProgressWithoutEffortValidator removed - replaced by RC-2 hierarchical rule
            };
            return new CompositeWorkItemValidator(validators);
        });

        // Register hierarchical validation rules (new validation system)
        // Structural Integrity (SI) rules
        services.AddScoped<IHierarchicalValidationRule, DoneParentWithUnfinishedDescendantsRule>();
        services.AddScoped<IHierarchicalValidationRule, RemovedParentWithUnfinishedDescendantsRule>();
        services.AddScoped<IHierarchicalValidationRule, NewParentWithInProgressDescendantsRule>();
        
        // Refinement Readiness (RR) rules
        services.AddScoped<IHierarchicalValidationRule, EpicDescriptionEmptyRule>();
        services.AddScoped<IHierarchicalValidationRule, FeatureDescriptionEmptyRule>();
        services.AddScoped<IHierarchicalValidationRule, EpicWithoutFeaturesRule>();
        
        // Refinement Completeness (RC) rules
        services.AddScoped<IHierarchicalValidationRule, PbiDescriptionEmptyRule>();
        services.AddScoped<IHierarchicalValidationRule, PbiEffortEmptyRule>();
        services.AddScoped<IHierarchicalValidationRule, FeatureWithoutChildrenRule>();

        // Register hierarchical work item validator
        services.AddScoped<IHierarchicalWorkItemValidator, HierarchicalWorkItemValidator>();

        // Register Core business logic services
        services.AddScoped<WorkItemFilterer>();
        services.AddScoped<BacklogHealthCalculator>();

        // Register TFS configuration and client
        services.AddDataProtection();
        services.AddScoped<IEfConcurrencyGate, EfConcurrencyGate>();
        services.AddScoped<TfsConfigurationService>();
        services.AddScoped<ITfsConfigurationService>(sp => sp.GetRequiredService<TfsConfigurationService>());
        services.AddScoped<TfsAuthenticationProvider>();
        services.AddScoped<ProfileFilterService>();

        // Register Bug Triage services
        services.AddScoped<BugTriageStateService>();
        services.AddScoped<Api.Services.TriageTagService>();

        // Register TFS throttling and request services (used by RealTfsClient)
        services.AddSingleton<TfsRequestThrottler>();
        services.AddScoped<TfsRequestSender>();
        services.AddSingleton<RevisionIngestionDiagnostics>();

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

            services.AddScoped<IWorkItemRevisionSource, RealODataRevisionTfsClient>();
        }

        // Register Revision Ingestion V2 options
        services.AddOptions<RevisionIngestionV2Options>()
            .Bind(configuration.GetSection("RevisionIngestionV2"));

        // Register Revision Ingestion Service (for Sprint Trend feature)
        services.AddSingleton<RevisionIngestionService>();
        services.AddSingleton<RevisionIngestionServiceV2>();
        services.AddSingleton<IRevisionIngestionService, RevisionIngestionDispatcher>();

        // Register Relations Revision Hydrator (for ingesting relation changes)
        services.AddScoped<IRelationRevisionHydrator, RelationRevisionHydrator>();

        // Register Work Item Resolution Service (for Sprint Trend hierarchical resolution)
        services.AddSingleton<WorkItemResolutionService>();

        // Register Work Item Relationship Snapshot Service (for building relationship edges)
        services.AddSingleton<WorkItemRelationshipSnapshotService>();

        // Register Sprint Trend Projection Service (for sprint metrics computation)
        services.AddSingleton<SprintTrendProjectionService>();

        // Register background services
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
