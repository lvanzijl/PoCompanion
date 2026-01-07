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

// Register SignalR HubConnection
builder.Services.AddScoped<HubConnection>(sp =>
{
    var hubPath = builder.Configuration["SignalR:HubPath"] ?? "/hubs/workitems";
    // Ensure proper URL concatenation by using Uri
    var baseUri = new Uri(apiBaseUrl);
    var hubUri = new Uri(baseUri, hubPath);
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

builder.Services.AddScoped<IPipelinesClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var client = new PipelinesClient(httpClient);
    client.BaseUrl = apiBaseUrl;
    return client;
});

// Register client services
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<PullRequestService>();
builder.Services.AddScoped<PipelineService>();
builder.Services.AddScoped<IWorkItemSyncHubService, WorkItemSyncHubService>();
builder.Services.AddScoped<ITreeBuilderService, TreeBuilderService>();
builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<TfsConfigService>();
builder.Services.AddScoped<ModeIsolatedStateService>();
builder.Services.AddScoped<ErrorMessageService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IStartupOrchestratorService, StartupOrchestratorService>();

// Register browser-based storage services
builder.Services.AddScoped<IPreferencesService, BrowserPreferencesService>();
builder.Services.AddScoped<ISecureStorageService, BrowserSecureStorageService>();

// Register business logic services (now using API)
builder.Services.AddScoped<WorkItemFilteringService>();
builder.Services.AddScoped<WorkItemSelectionService>();
builder.Services.AddScoped<PullRequestMetricsService>();
builder.Services.AddScoped<BacklogHealthCalculationService>();
builder.Services.AddScoped<ReleasePlanningService>();

// Register clipboard, export and report services
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<BrowserNavigationService>();

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
