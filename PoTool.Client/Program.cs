using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PoTool.Client;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient to point to the API
// When hosted by the API server, use the host's base address
// When running standalone (dev), use the configured API address
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

// Register API clients
builder.Services.AddScoped<IWorkItemsClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new WorkItemsClient(httpClient);
});

// Register services
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<IWorkItemSyncHubService, WorkItemSyncHubService>();
builder.Services.AddScoped<ITreeBuilderService, TreeBuilderService>();
builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
