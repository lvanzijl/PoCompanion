using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Filters;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Api.Services.BuildQuality;
using PoTool.Api.Services.Configuration;
using PoTool.Api.Services.MockData;
using PoTool.Api.Services.Sync;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Configuration;
using PoTool.Core.Filters;
using PoTool.Core.Sync;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems.Validators.Rules;
using PoTool.Core.WorkItems.Filtering;
using PoTool.Core.Health;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.BacklogQuality.Services;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Metrics;
using PoTool.Core.Domain.Portfolio;

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
        services.AddControllers(options =>
        {
            options.Filters.Add(new EnforceSharedDtoActionResultContractFilter());
        });
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
        services.AddSingleton(new TfsRuntimeMode(useMockClient));

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
        services.AddScoped<SyncChangesSummaryService>();
        services.AddScoped<ExportConfigurationService>();
        services.AddScoped<ImportConfigurationService>();
        services.AddScoped<IBuildQualityReadStore, EfBuildQualityReadStore>();
        services.AddScoped<IBuildQualityProvider, BuildQualityProvider>();
        services.AddScoped<DeliveryFilterResolutionService>();
        services.AddScoped<PipelineFilterResolutionService>();
        services.AddScoped<IPipelineInsightsReadStore, EfPipelineInsightsReadStore>();
        services.AddScoped<IWorkItemQuery, EfWorkItemQuery>();
        services.AddScoped<SprintFilterResolutionService>();
        services.AddScoped<SprintScopedWorkItemLoader>();

        // Register Classification service
        services.AddScoped<IWorkItemClassificationService, WorkItemClassificationService>();

        // Register Work Item State Classification service
        services.AddScoped<IWorkItemStateClassificationService, WorkItemStateClassificationService>();
        services.AddScoped<IIncrementalSyncPlanner, DefaultIncrementalSyncPlanner>();

        // Register Sync Pipeline services (Stages 1-10)
        services.AddScoped<WorkItemSyncStage>();
        services.AddScoped<ActivityIngestionSyncStage>();
        services.AddScoped<TeamSprintSyncStage>();
        services.AddScoped<WorkItemRelationshipSnapshotStage>();
        services.AddScoped<WorkItemResolutionSyncStage>();
        services.AddScoped<SprintTrendProjectionSyncStage>();
        services.AddScoped<PullRequestSyncStage>();
        services.AddScoped<PipelineSyncStage>();
        services.AddScoped<ValidationComputeStage>();
        services.AddScoped<MetricsComputeStage>();
        services.AddScoped<FinalizeCacheStage>();
        services.AddSingleton<ISyncPipeline, SyncPipelineRunner>();

        services.Configure<ActivityIngestionOptions>(configuration.GetSection("ActivityIngestion"));

        // Register SignalR broadcaster for sync progress
        services.AddSingleton<ISyncProgressBroadcaster, SyncProgressBroadcaster>();
        
        // Register SignalR broadcaster for TFS config progress
        services.AddSingleton<ITfsConfigProgressBroadcaster, TfsConfigProgressBroadcaster>();

        // Register Data Source Mode Provider (for switching between Live and Cache)
        services.AddScoped<IDataSourceModeProvider, DataSourceModeProvider>();
        services.AddHttpContextAccessor();

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

        // Register the factory for data-source-aware provider resolution where requests still switch
        // between cache and live at runtime.
        services.AddScoped<DataSourceAwareReadProviderFactory>();

        // Work item reads still resolve from the request mode at call time.
        services.AddScoped<IWorkItemReadProvider, LazyWorkItemReadProvider>();

        // Analytical pipeline reads are cache-only after middleware guardrails.
        // Definitions discovery resolves the live provider explicitly in its handler.
        services.AddScoped<IPipelineReadProvider, CachedPipelineReadProvider>();

        // Pull request analytical reads are cache-only after middleware guardrails.
        // Generic PR read-provider operations remain provider-shaped, while multi-PR analytical
        // composition lives behind IPullRequestQueryStore.
        services.AddScoped<IPullRequestReadProvider, CachedPullRequestReadProvider>();
        services.AddScoped<IPullRequestQueryStore, EfPullRequestQueryStore>();

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
            services.AddHostedService<MockConfigurationSeedHostedService>();
        }

        // Bridge hierarchical rules into the legacy pipeline so all SI/RR/RC violations
        // appear in the Validation Queue and Fix Session pages
        services.AddScoped<HierarchicalToLegacyValidatorAdapter>();
        services.AddScoped<IWorkItemValidator>(provider =>
            provider.GetRequiredService<HierarchicalToLegacyValidatorAdapter>());

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
        services.AddSingleton<BacklogQualityAnalyzer>();
        services.AddScoped<IBacklogQualityAnalysisService, BacklogQualityAnalysisService>();

        // Register Core business logic services
        services.AddScoped<WorkItemFilterer>();
        services.AddScoped<BacklogHealthCalculator>();
        services.AddScoped<BacklogStateComputationService>();
        services.AddSingleton<ISprintCommitmentService, SprintCommitmentService>();
        services.AddSingleton<ISprintScopeChangeService, SprintScopeChangeService>();
        services.AddSingleton<ISprintCompletionService, SprintCompletionService>();
        services.AddSingleton<ISprintSpilloverService, SprintSpilloverService>();
        services.AddSingleton<ICanonicalStoryPointResolutionService, CanonicalStoryPointResolutionService>();
        services.AddSingleton<ISprintFactService, SprintFactService>();
        services.AddSingleton<IHierarchyRollupService, HierarchyRollupService>();
        services.AddSingleton<IEpicProgressService, EpicProgressService>();
        services.AddSingleton<IEpicAggregationService, EpicAggregationService>();
        services.AddSingleton<IProductAggregationService, ProductAggregationService>();
        services.AddSingleton<IPlanningQualityService, PlanningQualityService>();
        services.AddSingleton<ISnapshotComparisonService, SnapshotComparisonService>();
        services.AddSingleton<IPortfolioSnapshotValidationService, PortfolioSnapshotValidationService>();
        services.AddSingleton<IPortfolioSnapshotComparisonService, PortfolioSnapshotComparisonService>();
        services.AddSingleton<IPortfolioSnapshotFactory, PortfolioSnapshotFactory>();
        services.AddSingleton<IPortfolioSnapshotPersistenceMapper, PortfolioSnapshotPersistenceMapper>();
        services.AddSingleton<IInsightService, InsightService>();
        services.AddSingleton<IDeliveryProgressRollupService, DeliveryProgressRollupService>();
        services.AddSingleton<IPortfolioDeliverySummaryService, PortfolioDeliverySummaryService>();
        services.AddSingleton<ISprintDeliveryProjectionService, SprintDeliveryProjectionService>();
        services.AddSingleton<IPortfolioFlowSummaryService, PortfolioFlowSummaryService>();
        services.AddScoped<IPortfolioSnapshotCaptureDataService, PortfolioSnapshotCaptureDataService>();
        services.AddScoped<IPortfolioSnapshotPersistenceService, PortfolioSnapshotPersistenceService>();
        services.AddScoped<IPortfolioSnapshotSelectionService, PortfolioSnapshotSelectionService>();
        services.AddScoped<IPortfolioSnapshotCaptureOrchestrator, PortfolioSnapshotCaptureOrchestrator>();
        services.AddScoped<IPortfolioReadModelStateService, PortfolioReadModelStateService>();
        services.AddSingleton<FilterContextValidator>();
        services.AddScoped<PortfolioFilterResolutionService>();
        services.AddScoped<PullRequestFilterResolutionService>();
        services.AddScoped<IPortfolioReadModelMapper, PortfolioReadModelMapper>();
        services.AddScoped<IPortfolioTrendAnalysisService, PortfolioTrendAnalysisService>();
        services.AddSingleton<IPortfolioDecisionSignalService, PortfolioDecisionSignalService>();
        services.AddScoped<PortfolioProgressQueryService>();
        services.AddScoped<PortfolioSnapshotQueryService>();
        services.AddScoped<PortfolioComparisonQueryService>();
        services.AddScoped<PortfolioTrendQueryService>();
        services.AddScoped<PortfolioDecisionSignalQueryService>();
        services.AddSingleton<ICompletionForecastService, CompletionForecastService>();
        services.AddSingleton<IVelocityCalibrationService, VelocityCalibrationService>();
        services.AddSingleton<IEffortTrendForecastService, EffortTrendForecastService>();
        services.AddSingleton<IEffortDistributionService, EffortDistributionService>();
        services.AddSingleton<IEffortEstimationQualityService, EffortEstimationQualityService>();
        services.AddSingleton<IEffortEstimationSuggestionService, EffortEstimationSuggestionService>();
        services.AddSingleton<PoTool.Core.Domain.Cdc.Sprints.SprintExecutionMetricsCalculator>();
        services.AddSingleton<PoTool.Core.Domain.Cdc.Sprints.ISprintExecutionMetricsCalculator>(sp =>
            sp.GetRequiredService<PoTool.Core.Domain.Cdc.Sprints.SprintExecutionMetricsCalculator>());
        services.AddSingleton<PoTool.Core.Domain.Metrics.ISprintExecutionMetricsCalculator>(sp =>
            sp.GetRequiredService<PoTool.Core.Domain.Cdc.Sprints.SprintExecutionMetricsCalculator>());

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

        // Register Roadmap Snapshot service
        services.AddScoped<Api.Services.RoadmapSnapshotService>();

        // Register TFS throttling and request services (used by RealTfsClient)
        services.AddSingleton<TfsRequestThrottler>();
        services.AddScoped<TfsRequestSender>();
        // Always register HttpClientFactory for handlers that need it
        services.AddHttpClient();

        // Register TFS transport based on configuration (useMockClient already read above)
        if (!useMockClient)
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

        }
        services.AddScoped<ITfsAccessGateway>(provider =>
        {
            var runtimeMode = provider.GetRequiredService<TfsRuntimeMode>();
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("TfsRuntime");

            // Do not inject RealTfsClient directly. All production TFS access must flow through the gateway.
            ITfsClient client = runtimeMode.UseMockClient
                ? ActivatorUtilities.CreateInstance<MockTfsClient>(provider)
                : RealTfsClientFactory.Create(provider);

            TfsRuntimeModeGuard.EnsureExpectedClient(runtimeMode, client, logger, "ITfsAccessGateway registration");
            return ActivatorUtilities.CreateInstance<TfsAccessGateway>(provider, client);
        });
        services.AddScoped<ITfsClient>(provider => provider.GetRequiredService<ITfsAccessGateway>());
        services.AddScoped<ActivityEventIngestionService>();
        services.AddScoped<IActivityEventSource, LedgerActivityEventSource>();

        // Register Work Item Resolution Service (for Sprint Trend hierarchical resolution)
        services.AddSingleton<WorkItemResolutionService>();

        // Register Work Item Relationship Snapshot Service (for building relationship edges)
        services.AddSingleton<WorkItemRelationshipSnapshotService>();

        // Register scoped Sprint Trend Projection Service (for sprint metrics computation)
        services.AddScoped<PortfolioFlowProjectionService>();
        services.AddScoped<SprintTrendProjectionService>();

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
