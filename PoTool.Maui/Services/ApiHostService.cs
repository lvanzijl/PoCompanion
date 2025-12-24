#if WINDOWS
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoTool.Api.Configuration;

namespace PoTool.Maui.Services;

/// <summary>
/// Service that hosts the ASP.NET Core API in-process within the MAUI application.
/// Only available on Windows platform.
/// </summary>
public class ApiHostService : IDisposable
{
    private WebApplication? _app;
    private readonly int _port;
    private readonly ILogger<ApiHostService> _logger;
    private bool _disposed;

    public ApiHostService(int port, ILogger<ApiHostService> logger)
    {
        _port = port;
        _logger = logger;
    }

    /// <summary>
    /// Gets the base URL of the hosted API.
    /// </summary>
    public string BaseUrl => $"http://localhost:{_port}";

    /// <summary>
    /// Gets whether the API is currently running.
    /// </summary>
    public bool IsRunning => _app != null;

    /// <summary>
    /// Starts the API host asynchronously.
    /// </summary>
    public async Task StartAsync()
    {
        if (_app != null)
        {
            _logger.LogWarning("API host is already running");
            return;
        }

        try
        {
            _logger.LogInformation("Starting API host on {BaseUrl}", BaseUrl);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory
            });

            // Configure Kestrel to listen on localhost only
            builder.WebHost.UseUrls(BaseUrl);

            // Add all PoTool API services using the extension method
            builder.Services.AddPoToolApiServices(builder.Configuration, builder.Environment.IsDevelopment());

            // Build the application
            _app = builder.Build();

            // Configure the API middleware pipeline using the extension method
            _app.ConfigurePoToolApi(_app.Environment.IsDevelopment());

            // Start the API asynchronously
            await _app.StartAsync();

            _logger.LogInformation("API host started successfully on {BaseUrl}", BaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start API host");
            throw;
        }
    }

    /// <summary>
    /// Stops the API host gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        if (_app == null)
        {
            _logger.LogWarning("API host is not running");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping API host");
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
            _logger.LogInformation("API host stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping API host");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (_app != null)
            {
                StopAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing API host");
        }
        finally
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
#endif
