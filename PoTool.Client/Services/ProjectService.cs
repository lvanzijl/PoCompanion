using System.Net;
using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Planning;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing projects via the API.
/// </summary>
public class ProjectService
{
    private readonly IProjectsClient _projectsClient;
    private readonly HttpClient _httpClient;

    public ProjectService(IProjectsClient projectsClient, HttpClient httpClient)
    {
        _projectsClient = projectsClient;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets all projects in the system.
    /// </summary>
    public async Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return (await _projectsClient.GetProjectsAsync(cancellationToken)).ToList();
        }
        catch (ApiException ex)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(ex);
        }
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

        try
        {
            return await _projectsClient.GetProjectAsync(aliasOrId, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (ApiException ex)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(ex);
        }
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

        try
        {
            return (await _projectsClient.GetProjectProductsAsync(aliasOrId, cancellationToken)).ToList();
        }
        catch (ApiException ex)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(ex);
        }
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
