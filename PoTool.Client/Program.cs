using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor.Services;
using PoTool.Client;
using PoTool.Client.ApiClient;
using PoTool.Client.Handlers;
using PoTool.Client.Services;
using PoTool.Client.Storage;
using PoTool.Shared.Contracts;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Get API base URL - in Blazor WebAssembly served by ASP.NET Core, this is the same origin
var apiBaseUrl = builder.HostEnvironment.BaseAddress;

// Register PAT header handler
builder.Services.AddTransient<PatHeaderHandler>();

// Configure HttpClient with API base address and PAT header handler
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<PatHeaderHandler>();
    var client = new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
    return client;
});

// Register SignalR HubConnection
builder.Services.AddScoped<HubConnection>(sp =>
{
    return new HubConnectionBuilder()
        .WithUrl($"{apiBaseUrl}hubs/workitems")
        .WithAutomaticReconnect()
        .Build();
});

// Register NSwag-generated API clients
builder.Services.AddScoped<IClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new PoTool.Client.ApiClient.Client(httpClient);
});

builder.Services.AddScoped<IWorkItemsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new WorkItemsClient(httpClient);
});

builder.Services.AddScoped<ISettingsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new SettingsClient(httpClient);
});

builder.Services.AddScoped<IProfilesClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new ProfilesClient(httpClient);
});

builder.Services.AddScoped<IPullRequestsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new PullRequestsClient(httpClient);
});

builder.Services.AddScoped<IFilteringClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new FilteringClient(httpClient);
});

builder.Services.AddScoped<IHealthCalculationClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new HealthCalculationClient(httpClient);
});

// Register client services
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<PullRequestService>();
builder.Services.AddScoped<IWorkItemSyncHubService, WorkItemSyncHubService>();
builder.Services.AddScoped<ITreeBuilderService, TreeBuilderService>();
builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<TfsConfigService>();
builder.Services.AddScoped<ModeIsolatedStateService>();
builder.Services.AddScoped<ErrorMessageService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();

// Register browser-based storage services
builder.Services.AddScoped<IPreferencesService, BrowserPreferencesService>();
builder.Services.AddScoped<ISecureStorageService, BrowserSecureStorageService>();

// Register business logic services (now using API)
builder.Services.AddScoped<WorkItemFilteringService>();
builder.Services.AddScoped<WorkItemSelectionService>();
builder.Services.AddScoped<PullRequestMetricsService>();
builder.Services.AddScoped<BacklogHealthCalculationService>();

// Register clipboard, export and report services
builder.Services.AddScoped<IClipboardService, ClipboardService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<BrowserNavigationService>();

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
