using Microsoft.Extensions.DependencyInjection;

namespace PoTool.Client.ApiClient;

public static class BugTriageClientServiceCollectionExtensions
{
    public static IServiceCollection AddBugTriageClient(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddScoped<IBugTriageClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var client = new BugTriageClient(httpClient);
            client.BaseUrl = apiBaseUrl;
            return client;
        });

        return services;
    }
}
