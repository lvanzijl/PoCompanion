using Microsoft.Extensions.Logging;

namespace PoTool.Maui.Services;

/// <summary>
/// Helper service to initialize the API and wait for it to be ready.
/// </summary>
public class ApiInitializer
{
    private readonly ApiHostService _apiHost;
    private readonly ILogger<ApiInitializer> _logger;
    private readonly HttpClient _httpClient;

    public ApiInitializer(ApiHostService apiHost, ILogger<ApiInitializer> logger)
    {
        _apiHost = apiHost;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Initializes the API and waits for it to be healthy.
    /// </summary>
    /// <param name="maxRetries">Maximum number of health check retries.</param>
    /// <param name="retryDelayMs">Delay in milliseconds between retries.</param>
    /// <returns>True if API started successfully, false otherwise.</returns>
    public async Task<bool> InitializeAsync(int maxRetries = 10, int retryDelayMs = 500)
    {
        try
        {
            // Start the API
            await _apiHost.StartAsync();

            // Wait for API to be healthy
            _logger.LogInformation("Waiting for API to be ready at {BaseUrl}", _apiHost.BaseUrl);

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"{_apiHost.BaseUrl}/health");
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("API is healthy and ready");
                        return true;
                    }
                }
                catch (HttpRequestException)
                {
                    // API not ready yet, retry
                }

                if (i < maxRetries - 1)
                {
                    _logger.LogDebug("API not ready yet, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                        retryDelayMs, i + 1, maxRetries);
                    await Task.Delay(retryDelayMs);
                }
            }

            _logger.LogError("API failed to become healthy after {MaxRetries} attempts", maxRetries);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize API");
            return false;
        }
    }
}
