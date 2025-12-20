using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Maui.Services;

namespace PoTool.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Add Blazor WebView services
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Create and register API host service
        const int apiPort = 5291;
        var apiHost = new ApiHostService(apiPort, 
            LoggerFactory.Create(config => config.AddConsole()).CreateLogger<ApiHostService>());
        
        builder.Services.AddSingleton(apiHost);
        builder.Services.AddSingleton<ApiInitializer>();

        // Configure HttpClient with API base address
        builder.Services.AddScoped(sp => new HttpClient 
        { 
            BaseAddress = new Uri(apiHost.BaseUrl) 
        });

        // Register SignalR HubConnection
        builder.Services.AddScoped<HubConnection>(sp =>
        {
            return new HubConnectionBuilder()
                .WithUrl($"{apiHost.BaseUrl}/hubs/workitems")
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
        builder.Services.AddScoped<PullRequestService>();
        builder.Services.AddScoped<IWorkItemSyncHubService, WorkItemSyncHubService>();
        builder.Services.AddScoped<ITreeBuilderService, TreeBuilderService>();
        builder.Services.AddScoped<ICorrelationIdService, CorrelationIdService>();
        builder.Services.AddScoped<SettingsService>();
        builder.Services.AddScoped<TfsConfigService>();
        builder.Services.AddScoped<ModeIsolatedStateService>();

        // Add MudBlazor services
        builder.Services.AddMudServices();

        var app = builder.Build();

        // Start API during app initialization
        var initializer = app.Services.GetRequiredService<ApiInitializer>();
        var initTask = Task.Run(async () => await initializer.InitializeAsync());
        initTask.Wait(); // Wait for API to be ready before returning

        if (!apiHost.IsRunning)
        {
            throw new InvalidOperationException("Failed to start API host. Application cannot start.");
        }

        return app;
    }
}
