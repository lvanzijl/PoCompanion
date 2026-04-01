using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Shared.Planning;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing projects via the API.
/// </summary>
public class ProjectService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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
        var projects = await _httpClient.GetFromJsonAsync<List<ProjectDto>>("api/projects", SerializerOptions, cancellationToken);
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
        return await response.Content.ReadFromJsonAsync<ProjectDto>(SerializerOptions, cancellationToken);
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
            SerializerOptions,
            cancellationToken);

        return products ?? [];
    }

    /// <summary>
    /// Gets the read-only planning summary for a project resolved by alias or internal identifier.
    /// </summary>
    public async Task<ProjectPlanningSummaryDto?> GetPlanningSummaryAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return null;
        }

        using var response = await _httpClient.GetAsync(
            $"api/projects/{Uri.EscapeDataString(aliasOrId)}/planning-summary",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectPlanningSummaryDto>(SerializerOptions, cancellationToken);
    }
}
