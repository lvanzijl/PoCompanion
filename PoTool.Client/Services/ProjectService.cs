using System.Net;
using System.Net.Http.Json;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Planning;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing projects via the API.
/// </summary>
public class ProjectService
{
    private readonly HttpClient _httpClient;

    public ProjectService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets all projects in the system.
    /// </summary>
    public async Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _httpClient.GetFromJsonAsync<List<ProjectDto>>("api/projects", JsonHelper.CaseInsensitiveOptions, cancellationToken);
        return projects ?? [];
    }

    /// <summary>
    /// Gets a project by alias or internal identifier.
    /// </summary>
    public async Task<ProjectDto?> GetProjectAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return null;
        }

        using var response = await _httpClient.GetAsync(
            $"api/projects/{Uri.EscapeDataString(aliasOrId)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(JsonHelper.CaseInsensitiveOptions, cancellationToken);
    }

    /// <summary>
    /// Gets products for a project resolved by alias or internal identifier.
    /// </summary>
    public async Task<IReadOnlyList<ProductDto>> GetProjectProductsAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return [];
        }

        var products = await _httpClient.GetFromJsonAsync<List<ProductDto>>(
            $"api/projects/{Uri.EscapeDataString(aliasOrId)}/products",
            JsonHelper.CaseInsensitiveOptions,
            cancellationToken);

        return products ?? [];
    }

    /// <summary>
    /// Gets the read-only planning summary for a project resolved by alias or internal identifier.
    /// </summary>
    public async Task<CacheBackedClientResult<ProjectPlanningSummaryDto>> GetPlanningSummaryAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return CacheBackedClientResult<ProjectPlanningSummaryDto>.Empty("No project was selected.");
        }

        return await DataStateHttpClientHelper.GetDataStateAsync<ProjectPlanningSummaryDto>(
            _httpClient,
            $"api/projects/{Uri.EscapeDataString(aliasOrId)}/planning-summary",
            cancellationToken);
    }
}
