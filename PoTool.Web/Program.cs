using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor.Services;
using PoTool.Api.Configuration;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

var builder = WebApplication.CreateBuilder(args);

// Add API services
builder.Services.AddPoToolApiServices(builder.Configuration, builder.Environment.IsDevelopment());

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient with API base address
var baseUrl = builder.Configuration["BaseUrl"] ?? "http://localhost:5291";
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(baseUrl) 
});

// Register SignalR HubConnection
builder.Services.AddScoped<HubConnection>(sp =>
{
    return new HubConnectionBuilder()
        .WithUrl($"{baseUrl}/hubs/workitems")
        .WithAutomaticReconnect()
        .Build();
});

// Register API clients
builder.Services.AddScoped<IWorkItemsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new WorkItemsClient(httpClient);
});

// Register client services
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<IWorkItemSyncHubService, WorkItemSyncHubService>();
builder.Services.AddScoped<ITreeBuilderService, TreeBuilderService>();
builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<TfsConfigService>();
builder.Services.AddScoped<ModeIsolatedStateService>();

// Add MudBlazor services
builder.Services.AddMudServices();

var app = builder.Build();

// Configure API middleware
app.ConfigurePoToolApi(app.Environment.IsDevelopment());

// Configure Blazor middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<PoTool.Client.App>()
    .AddInteractiveServerRenderMode();

app.Run();
