using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using PoTool.Shared.BugTriage;

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

    public static IServiceCollection AddTriageTagsClient(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddScoped<ITriageTagsClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var client = new TriageTagsClient(httpClient)
            {
                BaseUrl = apiBaseUrl
            };
            return new GeneratedTriageTagsClient(client);
        });

        return services;
    }

    private sealed class GeneratedTriageTagsClient : ITriageTagsClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ITriageTagsClient _innerClient;

        public GeneratedTriageTagsClient(ITriageTagsClient innerClient)
        {
            _innerClient = innerClient;
        }

        public Task<ICollection<TriageTagDto>> GetAllTagsAsync()
            => _innerClient.GetAllTagsAsync();

        public Task<ICollection<TriageTagDto>> GetAllTagsAsync(CancellationToken cancellationToken)
            => _innerClient.GetAllTagsAsync(cancellationToken);

        public Task<ICollection<TriageTagDto>> GetEnabledTagsAsync()
            => _innerClient.GetEnabledTagsAsync();

        public Task<ICollection<TriageTagDto>> GetEnabledTagsAsync(CancellationToken cancellationToken)
            => _innerClient.GetEnabledTagsAsync(cancellationToken);

        public Task<TriageTagOperationResponse> CreateTagAsync(CreateTriageTagRequest request)
            => CreateTagAsync(request, CancellationToken.None);

        public async Task<TriageTagOperationResponse> CreateTagAsync(CreateTriageTagRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await _innerClient.CreateTagAsync(request, cancellationToken);
            }
            catch (ApiException ex)
            {
                return ReadOperationResponseOrDefault(ex);
            }
        }

        public Task<TriageTagOperationResponse> UpdateTagAsync(int id, UpdateTriageTagRequest request)
            => UpdateTagAsync(id, request, CancellationToken.None);

        public async Task<TriageTagOperationResponse> UpdateTagAsync(int id, UpdateTriageTagRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await _innerClient.UpdateTagAsync(id, request, cancellationToken);
            }
            catch (ApiException ex)
            {
                return ReadOperationResponseOrDefault(ex);
            }
        }

        public Task<TriageTagOperationResponse> DeleteTagAsync(int id)
            => DeleteTagAsync(id, CancellationToken.None);

        public async Task<TriageTagOperationResponse> DeleteTagAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                return await _innerClient.DeleteTagAsync(id, cancellationToken);
            }
            catch (ApiException ex)
            {
                return ReadOperationResponseOrDefault(ex);
            }
        }

        public Task<TriageTagOperationResponse> ReorderTagsAsync(IEnumerable<int> tagIds)
            => ReorderTagsAsync(tagIds, CancellationToken.None);

        public async Task<TriageTagOperationResponse> ReorderTagsAsync(IEnumerable<int> tagIds, CancellationToken cancellationToken)
        {
            try
            {
                return await _innerClient.ReorderTagsAsync(tagIds, cancellationToken);
            }
            catch (ApiException ex)
            {
                return ReadOperationResponseOrDefault(ex);
            }
        }

        private static TriageTagOperationResponse ReadOperationResponseOrDefault(ApiException exception)
        {
            if (!string.IsNullOrWhiteSpace(exception.Response))
            {
                try
                {
                    var response = JsonSerializer.Deserialize<TriageTagOperationResponse>(exception.Response, JsonOptions);
                    if (response is not null)
                    {
                        return response;
                    }
                }
                catch (JsonException)
                {
                }
            }

            return new TriageTagOperationResponse(false, "Unknown error");
        }
    }
}
