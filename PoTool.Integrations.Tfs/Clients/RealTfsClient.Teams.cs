using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation - Projects, Teams, and Repositories
/// </summary>
public partial class RealTfsClient
{
    public async Task<IEnumerable<TfsProjectDto>> GetTfsProjectsAsync(string organizationUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
        {
            throw new ArgumentException("Organization URL cannot be empty", nameof(organizationUrl));
        }

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();
        var timeoutConfig = await _configService.GetConfigEntityAsync(cancellationToken) ?? new TfsConfigEntity();

        return await ExecuteWithRetryAsync<IEnumerable<TfsProjectDto>>(async () =>
        {
            // Build URL: {ServerUri}/_apis/projects?api-version=7.0
            var url = $"{organizationUrl.TrimEnd('/')}/_apis/projects?api-version=7.0";

            _logger.LogInformation("Retrieving TFS projects from URL='{Url}'", url);

            var projects = new List<TfsProjectDto>();
            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, timeoutConfig, pageUrl, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("value", out var valueArray))
                {
                    _logger.LogWarning("TFS projects response missing 'value' array");
                    break;
                }

                foreach (var project in valueArray.EnumerateArray())
                {
                    // Extract id (required)
                    if (!project.TryGetProperty("id", out var idProp))
                    {
                        _logger.LogWarning("TFS project missing 'id' field - skipping");
                        continue;
                    }
                    var projectId = idProp.GetString() ?? string.Empty;

                    // Extract name (required)
                    if (!project.TryGetProperty("name", out var nameProp))
                    {
                        _logger.LogWarning("TFS project '{Id}' missing 'name' field - skipping", projectId);
                        continue;
                    }
                    var projectName = nameProp.GetString() ?? string.Empty;

                    // Extract description (optional)
                    string? description = null;
                    if (project.TryGetProperty("description", out var descProp))
                    {
                        description = descProp.GetString();
                    }

                    projects.Add(new TfsProjectDto(projectId, projectName, description));
                }

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            _logger.LogInformation("Retrieved {Count} TFS projects", projects.Count);

            return projects;
        }, cancellationToken);
    }

    public async Task<IEnumerable<TfsTeamDto>> GetTfsTeamsAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);

        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync<IEnumerable<TfsTeamDto>>(async () =>
        {
            // Build URL: {ServerUri}/_apis/projects/{Project}/teams?api-version=7.0
            var encodedProject = Uri.EscapeDataString(config.Project);
            var url = $"{config.Url.TrimEnd('/')}/_apis/projects/{encodedProject}/teams?api-version={config.ApiVersion}";

            _logger.LogInformation("Retrieving TFS teams for Project='{Project}'", config.Project);

            var teams = new List<TfsTeamDto>();
            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("value", out var valueArray))
                {
                    _logger.LogWarning("TFS teams response missing 'value' array for Project='{Project}'", config.Project);
                    break;
                }

                foreach (var team in valueArray.EnumerateArray())
                {
                    // Extract id (required)
                    if (!team.TryGetProperty("id", out var idProp))
                    {
                        _logger.LogWarning("TFS team missing 'id' field - skipping");
                        continue;
                    }
                    var teamId = idProp.GetString() ?? string.Empty;

                    // Extract name (required)
                    if (!team.TryGetProperty("name", out var nameProp))
                    {
                        _logger.LogWarning("TFS team '{Id}' missing 'name' field - skipping", teamId);
                        continue;
                    }
                    var teamName = nameProp.GetString() ?? string.Empty;

                    // Extract description (optional)
                    string? description = null;
                    if (team.TryGetProperty("description", out var descProp))
                    {
                        description = descProp.GetString();
                    }

                    // Get team's default area path from team field values
                    // This requires a separate API call per team
                    var defaultAreaPath = await GetTeamDefaultAreaPathAsync(
                        httpClient,
                        config,
                        config.Project,
                        teamName,
                        cancellationToken);

                    var dto = new TfsTeamDto(
                        teamId,
                        teamName,
                        config.Project,
                        description,
                        defaultAreaPath
                    );

                    teams.Add(dto);

                    _logger.LogDebug(
                        "Mapped TFS team: Id={Id}, Name={Name}, DefaultAreaPath={AreaPath}",
                        teamId, teamName, defaultAreaPath);
                }

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            _logger.LogInformation(
                "Retrieved {Count} TFS teams for Project='{Project}'",
                teams.Count, config.Project);

            return teams;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<(string Name, string Id)>> GetGitRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);

        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync<IEnumerable<(string Name, string Id)>>(async () =>
        {
            _logger.LogInformation("Retrieving Git repositories for Project='{Project}'", config.Project);

            // Reuse existing internal method that fetches all repositories
            var repositories = await GetRepositoriesInternalAsync(config, httpClient, null, cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} Git repositories for Project='{Project}'",
                repositories.Count, config.Project);

            return repositories;
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieves the default area path for a team by querying team field values.
    /// </summary>
    private async Task<string> GetTeamDefaultAreaPathAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        string projectName,
        string teamName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build URL: {ServerUri}/{Project}/{Team}/_apis/work/teamsettings/teamfieldvalues?api-version=7.0
            var encodedProject = Uri.EscapeDataString(projectName);
            var encodedTeam = Uri.EscapeDataString(teamName);
            var url = $"{config.Url.TrimEnd('/')}/{encodedProject}/{encodedTeam}/_apis/work/teamsettings/teamfieldvalues?api-version={config.ApiVersion}";

            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
            
            // If team field values not available, fall back to project root area path
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Could not retrieve team field values for Team='{Team}', Status={Status}. Using project root area path.",
                    teamName, response.StatusCode);
                return projectName; // Default area path is the project name (root area path)
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            // The response contains defaultValue which is the default area path
            if (root.TryGetProperty("defaultValue", out var defaultValueProp))
            {
                var defaultValue = defaultValueProp.GetString();
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    return defaultValue;
                }
            }

            // If no defaultValue, check for values array (team may have multiple area paths)
            if (root.TryGetProperty("values", out var valuesArray) && valuesArray.GetArrayLength() > 0)
            {
                var firstValue = valuesArray[0];
                if (firstValue.TryGetProperty("value", out var valueProp))
                {
                    var value = valueProp.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            // Fall back to project root area path
            _logger.LogDebug(
                "No area path found in team field values for Team='{Team}', using fallback='{Fallback}'",
                teamName, projectName);
            return projectName; // Default area path is the project name (root area path)
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error retrieving team field values for Team='{Team}'. Using fallback.",
                teamName);
            return projectName; // Default area path is the project name (root area path)
        }
    }

    // ============================================
    // TEAM ITERATIONS (SPRINTS)
    // ============================================

    /// <inheritdoc />
    public async Task<IEnumerable<TeamIterationDto>> GetTeamIterationsAsync(
        string projectName,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);

        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build URL: {ServerUri}/{Project}/{TeamName}/_apis/work/teamsettings/iterations?api-version=7.0
            var encodedProject = Uri.EscapeDataString(projectName);
            var encodedTeam = Uri.EscapeDataString(teamName);
            var url = $"{config.Url.TrimEnd('/')}/{encodedProject}/{encodedTeam}/_apis/work/teamsettings/iterations?api-version={config.ApiVersion}";

            _logger.LogInformation(
                "Retrieving team iterations for Project='{Project}', Team='{Team}'",
                projectName, teamName);

            var iterations = new List<TeamIterationDto>();
            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("value", out var valueArray))
                {
                    _logger.LogWarning(
                        "Team iterations response missing 'value' array for Project='{Project}', Team='{Team}'",
                        projectName, teamName);
                    break;
                }

                foreach (var iteration in valueArray.EnumerateArray())
                {
                    // Extract id (may be null or missing)
                    string? iterationId = null;
                    if (iteration.TryGetProperty("id", out var idProp))
                    {
                        iterationId = idProp.GetString();
                    }

                    // Extract name (required)
                    if (!iteration.TryGetProperty("name", out var nameProp))
                    {
                        _logger.LogWarning("Team iteration missing 'name' field - skipping");
                        continue;
                    }
                    var name = nameProp.GetString() ?? string.Empty;

                    // Extract path (required)
                    if (!iteration.TryGetProperty("path", out var pathProp))
                    {
                        _logger.LogWarning(
                            "Team iteration '{Name}' missing 'path' field - skipping",
                            name);
                        continue;
                    }
                    var path = pathProp.GetString() ?? string.Empty;

                    // Extract attributes (optional) - contains startDate and finishDate
                    DateTimeOffset? startDate = null;
                    DateTimeOffset? finishDate = null;
                    if (iteration.TryGetProperty("attributes", out var attributes))
                    {
                        if (attributes.TryGetProperty("startDate", out var startProp))
                        {
                            if (DateTimeOffset.TryParse(startProp.GetString(), out var start))
                            {
                                startDate = start;
                            }
                        }

                        if (attributes.TryGetProperty("finishDate", out var finishProp))
                        {
                            if (DateTimeOffset.TryParse(finishProp.GetString(), out var finish))
                            {
                                finishDate = finish;
                            }
                        }
                    }

                    // Extract timeFrame (optional) - "past", "current", or "future"
                    string? timeFrame = null;
                    if (iteration.TryGetProperty("timeFrame", out var timeFrameProp))
                    {
                        timeFrame = timeFrameProp.GetString();
                    }

                    var dto = new TeamIterationDto(
                        iterationId,
                        name,
                        path,
                        startDate,
                        finishDate,
                        timeFrame
                    );

                    iterations.Add(dto);

                    _logger.LogDebug(
                        "Mapped team iteration: Path={Path}, Name={Name}, TimeFrame={TimeFrame}, Start={Start}, End={End}",
                        path, name, timeFrame ?? "(none)", startDate?.ToString("yyyy-MM-dd") ?? "(none)", finishDate?.ToString("yyyy-MM-dd") ?? "(none)");
                }

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            _logger.LogInformation(
                "Retrieved {Count} team iterations for Project='{Project}', Team='{Team}'",
                iterations.Count, projectName, teamName);

            return (IEnumerable<TeamIterationDto>)iterations;
        }, cancellationToken);
    }

    // ============================================
    // WORK ITEM TYPE DEFINITIONS
    // ============================================

    /// <summary>
    /// Retrieves work item type definitions from TFS, including valid states for each type.
    /// </summary>
    public async Task<IEnumerable<WorkItemTypeDefinitionDto>> GetWorkItemTypeDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // GET {ServerUri}/{Project}/_apis/wit/workitemtypes?api-version=7.0
            var url = ProjectUrl(config, "_apis/wit/workitemtypes");
            _logger.LogDebug("Fetching work item type definitions from: {Url}", url);

            var definitions = new List<WorkItemTypeDefinitionDto>();
            string? continuationToken = null;
            var pageUrl = url;

            do
            {
                var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Failed to get work item type definitions: {StatusCode}, Response: {Response}",
                        response.StatusCode, errorContent);
                    throw new TfsException(
                        $"Failed to retrieve work item type definitions: {response.StatusCode}");
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    _logger.LogWarning("Work item type definitions response missing 'value' array");
                    break;
                }

                foreach (var witElement in valueArray.EnumerateArray())
                {
                    if (!witElement.TryGetProperty("name", out var nameProp))
                    {
                        continue;
                    }

                    var typeName = nameProp.GetString();
                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        continue;
                    }

                    var states = new List<string>();

                    // Extract states array
                    // Each state is an object with 'name' and 'category' properties
                    if (witElement.TryGetProperty("states", out var statesArray))
                    {
                        foreach (var stateElement in statesArray.EnumerateArray())
                        {
                            // Extract state name
                            if (stateElement.TryGetProperty("name", out var nameElement))
                            {
                                var stateName = nameElement.GetString();
                                if (!string.IsNullOrWhiteSpace(stateName))
                                {
                                    states.Add(stateName);
                                    
                                    // Extract category if present (e.g., "Completed" for "Done")
                                    // Category information is logged for future enhancement possibilities
                                    if (stateElement.TryGetProperty("category", out var categoryElement))
                                    {
                                        var category = categoryElement.GetString();
                                        if (!string.IsNullOrWhiteSpace(category))
                                        {
                                            _logger.LogDebug(
                                                "Work item type '{Type}': State '{State}' has category '{Category}'",
                                                typeName, stateName, category);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    var dto = new WorkItemTypeDefinitionDto
                    {
                        TypeName = typeName,
                        States = states.AsReadOnly()
                    };

                    definitions.Add(dto);

                    _logger.LogDebug(
                        "Mapped work item type definition: Type={Type}, States={StateCount}",
                        typeName, states.Count);
                }

                continuationToken = GetContinuationToken(response, doc);
                pageUrl = AddContinuationToken(url, continuationToken);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            _logger.LogInformation(
                "Retrieved {Count} work item type definitions from project '{Project}'",
                definitions.Count, config.Project);

            return (IEnumerable<WorkItemTypeDefinitionDto>)definitions;
        }, cancellationToken);
    }
}
