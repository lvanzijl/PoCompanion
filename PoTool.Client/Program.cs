using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor.Services;
using PoTool.Client;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Client.Storage;
using PoTool.Shared.Contracts;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Get API base URL from configuration (empty = same origin)
var apiBaseUrlConfig = builder.Configuration["ApiBaseUrl"];
var apiBaseUrl = string.IsNullOrEmpty(apiBaseUrlConfig)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseUrlConfig;

// Configure HttpClient with API base address (WASM-compatible, no HttpMessageHandler)
builder.Services.AddScoped(sp =>
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
    return client;
});

// Register SignalR HubConnection for cache synchronization progress
builder.Services.AddScoped<HubConnection>(sp =>
{
    var baseUri = new Uri(apiBaseUrl);
    var hubUri = new Uri(baseUri, "/hubs/cachesync");
    return new HubConnectionBuilder()
        .WithUrl(hubUri.ToString())
        .WithAutomaticReconnect()
        .Build();
});

// Register NSwag-generated API clients
// Note: Generated clients have a default BaseUrl, but we override it with the configured apiBaseUrl
builder.Services.AddScoped<IClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new PoTool.Client.ApiClient.Client(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IWorkItemsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new WorkItemsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<ISettingsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new SettingsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IProjectsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new ProjectsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IProfilesClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new ProfilesClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IPullRequestsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new PullRequestsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IFilteringClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new FilteringClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IHealthCalculationClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new HealthCalculationClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IMetricsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new MetricsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IBuildQualityClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new BuildQualityClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddBugTriageClient(apiBaseUrl);
builder.Services.AddTriageTagsClient(apiBaseUrl);

builder.Services.AddScoped<IOnboardingCrudClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new OnboardingCrudClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IOnboardingLookupClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new OnboardingLookupClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IOnboardingStatusClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new OnboardingStatusClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IPipelinesClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new PipelinesClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IStartupClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new StartupClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<ICacheSyncClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new CacheSyncClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IProductsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new ProductsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<ITeamsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new TeamsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<ISprintsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new SprintsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IReleasePlanningClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new ReleasePlanningClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

builder.Services.AddScoped<IRoadmapSnapshotsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new RoadmapSnapshotsClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

// Register client services
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<MetricsStateService>();
builder.Services.AddScoped<PullRequestStateService>();
builder.Services.AddScoped<PipelineStateService>();
builder.Services.AddScoped<WorkItemLoadCoordinatorService>();
builder.Services.AddScoped<PullRequestService>();
builder.Services.AddScoped<PipelineService>();
builder.Services.AddScoped<IBuildQualityService, BuildQualityService>();
builder.Services.AddScoped<ITreeBuilderService, TreeBuilderService>();
builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectIdentityMapper>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<SprintService>();
builder.Services.AddScoped<SprintDeliveryMetricsService>();
builder.Services.AddScoped<TfsConfigService>();
builder.Services.AddScoped<ConfigurationTransferService>();
builder.Services.AddScoped<ModeIsolatedStateService>();
builder.Services.AddScoped<FilterStateResolver>();
builder.Services.AddScoped<GlobalFilterContextResolver>();
builder.Services.AddScoped<GlobalFilterStore>();
builder.Services.AddScoped<GlobalFilterUiState>();
builder.Services.AddScoped<PageFilterExecutionGate>();
builder.Services.AddScoped<GlobalFilterRouteService>();
builder.Services.AddScoped<GlobalFilterCorrectionService>();
builder.Services.AddScoped<GlobalFilterDefaultsService>();
builder.Services.AddScoped<ErrorMessageService>();
builder.Services.AddScoped<StateClassificationService>();
builder.Services.AddScoped<WorkItemVisibilityService>();
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IOnboardingWizardState, OnboardingWizardState>();
builder.Services.AddScoped<IOnboardingWorkspaceService, OnboardingWorkspaceService>();
builder.Services.AddScoped<IOnboardingBindingReplacementLookupService, OnboardingBindingReplacementLookupService>();
builder.Services.AddScoped<OnboardingExecutionService>();
builder.Services.AddScoped<OnboardingWorkspaceViewModelFactory>();
builder.Services.AddScoped<IStartupOrchestratorService, StartupOrchestratorService>();
builder.Services.AddScoped<CacheSyncService>();
builder.Services.AddScoped<ICacheSyncService>(sp => sp.GetRequiredService<CacheSyncService>());
builder.Services.AddScoped<HomeProductBarMetricsService>();
builder.Services.AddScoped<ReleaseNotesService>();

// Register bug triage services
builder.Services.AddScoped<TriageTagService>();
builder.Services.AddScoped<BugTreeBuilderService>();

// Register Portfolio Progress Trend service (removed: PortfolioProgressTrendService — use IMetricsClient)

// Register Pipeline Sprint Trends service (removed: PipelineSprintTrendsService — use IPipelinesClient)

// Register PR Sprint Trends service (removed: PrSprintTrendsService — use IPullRequestsClient)

// Register browser-based storage services
builder.Services.AddScoped<IPreferencesService, BrowserPreferencesService>();
builder.Services.AddScoped<ISecureStorageService, BrowserSecureStorageService>();
builder.Services.AddScoped<DraftStorageService>();

// Register business logic services (now using API)
builder.Services.AddScoped<WorkItemFilteringService>();
builder.Services.AddScoped<WorkItemSelectionService>();
builder.Services.AddScoped<PullRequestMetricsService>();
builder.Services.AddScoped<BacklogHealthCalculationService>();
builder.Services.AddScoped<ReleasePlanningService>();
builder.Services.AddScoped<WorkspaceSignalService>();


// Register clipboard, export and report services
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<RoadmapReportingService>();
builder.Services.AddScoped<RoadmapSnapshotService>();
builder.Services.AddScoped<RoadmapAnalyticsService>();
builder.Services.AddScoped<BrowserNavigationService>();

// Home navigation services
builder.Services.AddScoped<IEpicOrderingService, EpicOrderingService>();

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
