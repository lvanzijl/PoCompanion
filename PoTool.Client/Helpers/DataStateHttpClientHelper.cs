using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Client.Models;
using PoTool.Shared.DataState;

namespace PoTool.Client.Helpers;

/// <summary>
/// Shared helper for reading cache-backed DataState envelopes from HTTP endpoints.
/// </summary>
public static class DataStateHttpClientHelper
{
    /// <summary>
    /// Reads a cache-backed endpoint and converts the shared envelope into a client-safe result.
    /// </summary>
    public static async Task<CacheBackedClientResult<T>> GetDataStateAsync<T>(
        HttpClient httpClient,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        try
        {
            using var response = await httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CacheBackedClientResult<T>.Unavailable(
                    $"The endpoint returned HTTP {(int)response.StatusCode} while loading cache-backed data.");
            }

            var envelope = await response.Content.ReadFromJsonAsync<DataStateResponseDto<T>>(
                JsonHelper.CaseInsensitiveOptions,
                cancellationToken);

            if (envelope is null)
            {
                return CacheBackedClientResult<T>.Failed("The cache-backed endpoint returned an empty response.");
            }

            return envelope.State switch
            {
                DataStateDto.Available when envelope.Data is not null
                    => CacheBackedClientResult<T>.Success(envelope.Data),
                DataStateDto.Available
                    => CacheBackedClientResult<T>.Failed("The cache-backed endpoint reported success without payload data.", envelope.RetryAfterSeconds),
                DataStateDto.Empty
                    => CacheBackedClientResult<T>.Empty(envelope.Reason, envelope.RetryAfterSeconds),
                DataStateDto.NotReady
                    => CacheBackedClientResult<T>.NotReady(envelope.Reason ?? DataStateContract.CacheNotReadyTitle, envelope.RetryAfterSeconds),
                DataStateDto.Failed
                    => CacheBackedClientResult<T>.Failed(envelope.Reason ?? "The cache-backed endpoint reported a failure.", envelope.RetryAfterSeconds),
                _ => CacheBackedClientResult<T>.Failed("The cache-backed endpoint returned an unknown data state.", envelope.RetryAfterSeconds)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return CacheBackedClientResult<T>.Unavailable(
                $"The cache-backed endpoint could not be reached: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return CacheBackedClientResult<T>.Failed(
                $"The cache-backed endpoint returned malformed JSON: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return CacheBackedClientResult<T>.Failed(
                $"The cache-backed endpoint returned an unsupported payload: {ex.Message}");
        }
    }
}
