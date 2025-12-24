using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using PoTool.Client.ApiClient;
using PoTool.Client.Handlers;
using PoTool.Client.Services;
using PoTool.Core.Contracts;
#if WINDOWS
using PoTool.Maui.Services;
#endif

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

        // Configure API base URL based on platform
        string apiBaseUrl;
        
#if WINDOWS
        // Create and register API host service for Windows
        const int apiPort = 5291;
        var apiHost = new ApiHostService(apiPort, 
            LoggerFactory.Create(config => config.AddConsole()).CreateLogger<ApiHostService>());
        
        builder.Services.AddSingleton(apiHost);
        builder.Services.AddSingleton<ApiInitializer>();
        apiBaseUrl = apiHost.BaseUrl;
#else
        // For mobile platforms, use external API URL
        // TODO: This should be configured via settings
        apiBaseUrl = "https://your-api-url.com"; // Replace with actual API URL
#endif

        // Register PAT header handler
        builder.Services.AddTransient<PatHeaderHandler>();

        // Configure HttpClient with API base address and PAT header handler
        builder.Services.AddHttpClient("PoToolApi", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
        })
        .AddHttpMessageHandler<PatHeaderHandler>();

        // Register primary HttpClient for backward compatibility
        builder.Services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return factory.CreateClient("PoToolApi");
        });

        // Register SignalR HubConnection
        builder.Services.AddScoped<HubConnection>(sp =>
        {
            return new HubConnectionBuilder()
                .WithUrl($"{apiBaseUrl}/hubs/workitems")
                .WithAutomaticReconnect()
                .Build();
        });

        // Register NSwag-generated API clients
        builder.Services.AddScoped<IClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            return new Client(httpClient);
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
        builder.Services.AddSingleton<IPreferencesService, MauiPreferencesService>();
        builder.Services.AddSingleton<ISecureStorageService, MauiSecureStorageService>();
        builder.Services.AddScoped<IOnboardingService, OnboardingService>();
        
        // Register business logic services
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

        var app = builder.Build();

#if WINDOWS
        // Start API during app initialization (Windows only)
        var initializer = app.Services.GetRequiredService<ApiInitializer>();
        var initTask = Task.Run(async () => await initializer.InitializeAsync());
        initTask.Wait(); // Wait for API to be ready before returning

        var apiHostService = app.Services.GetRequiredService<ApiHostService>();
        if (!apiHostService.IsRunning)
        {
            throw new InvalidOperationException("Failed to start API host. Application cannot start.");
        }
#endif

        return app;
    }
}
