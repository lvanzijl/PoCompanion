using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.BuildQuality;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Typed client service for BuildQuality API consumption.
/// </summary>
public sealed class BuildQualityService : IBuildQualityService
{
    private readonly HttpClient _httpClient;

    public BuildQualityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CanonicalClientResponse<BuildQualityPageDto>> GetRollingWindowAsync(
        int productOwnerId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/buildquality/rolling?productOwnerId={productOwnerId}" +
                       $"&windowStartUtc={Uri.EscapeDataString(windowStartUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}" +
                       $"&windowEndUtc={Uri.EscapeDataString(windowEndUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}";

        var envelope = await GetAsync<DeliveryQueryResponseDto<BuildQualityPageDto>>(endpoint, cancellationToken);
        return CanonicalClientResponseFactory.Create(envelope);
    }

    public async Task<CanonicalClientResponse<DeliveryBuildQualityDto>> GetSprintAsync(
        int productOwnerId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/buildquality/sprint?productOwnerId={productOwnerId}&sprintId={sprintId}";
        var envelope = await GetAsync<DeliveryQueryResponseDto<DeliveryBuildQualityDto>>(endpoint, cancellationToken);
        return CanonicalClientResponseFactory.Create(envelope);
    }

    public Task<PipelineBuildQualityDto> GetPipelineAsync(
        int productOwnerId,
        int sprintId,
        int? pipelineDefinitionId = null,
        int? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"productOwnerId={productOwnerId}",
            $"sprintId={sprintId}"
        };

        if (pipelineDefinitionId.HasValue)
        {
            query.Add($"pipelineDefinitionId={pipelineDefinitionId.Value}");
        }

        if (repositoryId.HasValue)
        {
            query.Add($"repositoryId={repositoryId.Value}");
        }

        return GetAsync<PipelineBuildQualityDto>($"api/buildquality/pipeline?{string.Join("&", query)}", cancellationToken);
    }

    private async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonHelper.CaseInsensitiveOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Received an empty BuildQuality response for {endpoint}.");
    }
}
