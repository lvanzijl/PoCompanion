using Microsoft.Extensions.Logging;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Core.Contracts;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Onboarding;
using PoTool.Shared.Pipelines;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingLiveLookupClient
{
    Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(
        TfsConnection connection,
        string? query,
        int top,
        int skip,
        CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(
        TfsConnection connection,
        string projectExternalId,
        string? query,
        int top,
        int skip,
        CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(
        TfsConnection connection,
        string projectExternalId,
        string? query,
        int top,
        int skip,
        CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(
        TfsConnection connection,
        string? query,
        string? projectExternalId,
        IReadOnlyCollection<string>? workItemTypes,
        int top,
        int skip,
        CancellationToken cancellationToken);

    Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(
        TfsConnection connection,
        string workItemExternalId,
        CancellationToken cancellationToken);
}

public sealed class OnboardingLiveLookupClient : IOnboardingLiveLookupClient
{
    private readonly IOnboardingScopedTfsClientFactory _clientFactory;
    private readonly IOnboardingObservability _observability;
    private readonly ILogger<OnboardingLiveLookupClient> _logger;

    public OnboardingLiveLookupClient(
        IOnboardingScopedTfsClientFactory clientFactory,
        IOnboardingObservability observability,
        ILogger<OnboardingLiveLookupClient> logger)
    {
        _clientFactory = clientFactory;
        _observability = observability;
        _logger = logger;
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(
        TfsConnection connection,
        string? query,
        int top,
        int skip,
        CancellationToken cancellationToken)
    {
        return await ExecuteLookupAsync(
            "projects",
            async () =>
            {
                await using var session = _clientFactory.CreateSession(connection);
                var projects = await session.Client.GetTfsProjectsAsync(connection.OrganizationUrl, cancellationToken);
                var results = projects
                    .Select(MapProject)
                    .Where(project => MatchesQuery(project.Name, query) || MatchesQuery(project.Description, query))
                    .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                    .Skip(skip)
                    .Take(top)
                    .ToArray();

                return OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(results);
            });
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(
        TfsConnection connection,
        string projectExternalId,
        string? query,
        int top,
        int skip,
        CancellationToken cancellationToken)
    {
        return await ExecuteLookupAsync(
            "teams",
            async () =>
            {
                var projectResolution = await ResolveProjectAsync(connection, projectExternalId, cancellationToken);
                if (!projectResolution.Succeeded)
                {
                    return OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Failure(projectResolution.Error!);
                }

                await using var session = _clientFactory.CreateSession(connection, projectResolution.Data!.Name, projectResolution.Data.Name);
                var teams = await session.Client.GetTfsTeamsAsync(cancellationToken);
                var results = teams
                    .Select(team => MapTeam(team, projectExternalId))
                    .Where(team => MatchesQuery(team.Name, query) || MatchesQuery(team.Description, query) || MatchesQuery(team.DefaultAreaPath, query))
                    .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                    .Skip(skip)
                    .Take(top)
                    .ToArray();

                return OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(results);
            });
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(
        TfsConnection connection,
        string projectExternalId,
        string? query,
        int top,
        int skip,
        CancellationToken cancellationToken)
    {
        return await ExecuteLookupAsync(
            "pipelines",
            async () =>
            {
                var projectResolution = await ResolveProjectAsync(connection, projectExternalId, cancellationToken);
                if (!projectResolution.Succeeded)
                {
                    return OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Failure(projectResolution.Error!);
                }

                await using var session = _clientFactory.CreateSession(connection, projectResolution.Data!.Name, projectResolution.Data.Name);
                var repositories = (await session.Client.GetGitRepositoriesAsync(cancellationToken)).ToArray();
                var results = new List<PipelineLookupResultDto>();

                foreach (var repository in repositories)
                {
                    var definitions = await session.Client.GetPipelineDefinitionsForRepositoryAsync(repository.Name, cancellationToken);
                    results.AddRange(definitions.Select(definition => MapPipeline(definition, projectExternalId)));
                }

                var filtered = results
                    .Where(pipeline =>
                        MatchesQuery(pipeline.Name, query) ||
                        MatchesQuery(pipeline.Folder, query) ||
                        MatchesQuery(pipeline.YamlPath, query) ||
                        MatchesQuery(pipeline.RepositoryName, query))
                    .OrderBy(pipeline => pipeline.Name, StringComparer.OrdinalIgnoreCase)
                    .Skip(skip)
                    .Take(top)
                    .ToArray();

                return OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(filtered);
            });
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(
        TfsConnection connection,
        string? query,
        string? projectExternalId,
        IReadOnlyCollection<string>? workItemTypes,
        int top,
        int skip,
        CancellationToken cancellationToken)
    {
        return await ExecuteLookupAsync(
            "work-items-search",
            async () =>
            {
                var projectResolutions = await ResolveProjectsForSearchAsync(connection, projectExternalId, cancellationToken);
                if (!projectResolutions.Succeeded)
                {
                    return OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>.Failure(projectResolutions.Error!);
                }

                var workItems = new Dictionary<string, WorkItemLookupResultDto>(StringComparer.OrdinalIgnoreCase);
                foreach (var project in projectResolutions.Data!)
                {
                    await using var session = _clientFactory.CreateSession(connection, project.Name, project.Name);
                    var liveWorkItems = await session.Client.GetWorkItemsAsync(project.Name, cancellationToken);
                    foreach (var workItem in liveWorkItems)
                    {
                        var mapped = MapWorkItem(workItem, project.ProjectExternalId);
                        if (MatchesQuery(mapped.Title, query) || MatchesQuery(mapped.WorkItemExternalId, query))
                        {
                            if (workItemTypes is null || workItemTypes.Count == 0 || ContainsIgnoreCase(workItemTypes, mapped.WorkItemType))
                            {
                                workItems[mapped.WorkItemExternalId] = mapped;
                            }
                        }
                    }
                }

                var filtered = workItems.Values
                    .OrderBy(workItem => workItem.Title, StringComparer.OrdinalIgnoreCase)
                    .Skip(skip)
                    .Take(top)
                    .ToArray();

                return OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>.Success(filtered);
            });
    }

    public async Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(
        TfsConnection connection,
        string workItemExternalId,
        CancellationToken cancellationToken)
    {
        return await ExecuteLookupAsync(
            "work-items-get",
            async () =>
            {
                if (!int.TryParse(workItemExternalId, out var parsedId))
                {
                    return OnboardingOperationResult<WorkItemLookupResultDto>.Failure(CreateValidationError(
                        "Work item external ID must be numeric.",
                        workItemExternalId));
                }

                var projectLookup = await GetProjectsAsync(connection, null, int.MaxValue, 0, cancellationToken);
                if (!projectLookup.Succeeded)
                {
                    return OnboardingOperationResult<WorkItemLookupResultDto>.Failure(projectLookup.Error!);
                }

                await using var session = _clientFactory.CreateSession(connection);
                var workItem = await session.Client.GetWorkItemByIdAsync(parsedId, cancellationToken);
                if (workItem is null)
                {
                    return OnboardingOperationResult<WorkItemLookupResultDto>.Failure(CreateNotFoundError(
                        $"Work item '{workItemExternalId}' was not found.",
                        workItemExternalId));
                }

                var projectExternalId = ResolveProjectExternalId(projectLookup.Data!, workItem.AreaPath);
                if (projectExternalId is null)
                {
                    return OnboardingOperationResult<WorkItemLookupResultDto>.Failure(CreateValidationError(
                        "Unable to resolve the work item project from the returned area path.",
                        workItem.AreaPath));
                }

                return OnboardingOperationResult<WorkItemLookupResultDto>.Success(MapWorkItem(workItem, projectExternalId));
            });
    }

    private async Task<OnboardingOperationResult<ProjectLookupResultDto>> ResolveProjectAsync(
        TfsConnection connection,
        string projectExternalId,
        CancellationToken cancellationToken)
    {
        var lookupResult = await GetProjectsAsync(connection, null, int.MaxValue, 0, cancellationToken);
        if (!lookupResult.Succeeded)
        {
            return OnboardingOperationResult<ProjectLookupResultDto>.Failure(lookupResult.Error!);
        }

        var project = lookupResult.Data!.FirstOrDefault(item =>
            item.ProjectExternalId.Equals(projectExternalId, StringComparison.OrdinalIgnoreCase));
        if (project is null)
        {
            return OnboardingOperationResult<ProjectLookupResultDto>.Failure(CreateNotFoundError(
                $"Project '{projectExternalId}' was not found.",
                projectExternalId));
        }

        return OnboardingOperationResult<ProjectLookupResultDto>.Success(project);
    }

    private async Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> ResolveProjectsForSearchAsync(
        TfsConnection connection,
        string? projectExternalId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectExternalId))
        {
            return await GetProjectsAsync(connection, null, int.MaxValue, 0, cancellationToken);
        }

        var projectResolution = await ResolveProjectAsync(connection, projectExternalId, cancellationToken);
        if (!projectResolution.Succeeded)
        {
            return OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Failure(projectResolution.Error!);
        }

        return OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(new[] { projectResolution.Data! });
    }

    private async Task<OnboardingOperationResult<T>> ExecuteLookupAsync<T>(
        string operation,
        Func<Task<OnboardingOperationResult<T>>> action)
    {
        _observability.LogLookupStarted(operation);
        try
        {
            var result = await action();
            _observability.RecordLookupCompleted(operation, result.Succeeded, result.Error?.Code);
            _observability.LogLookupCompleted(operation, result.Succeeded, result.Error?.Code);
            return result;
        }
        catch (TfsResourceNotFoundException ex)
        {
            var error = new OnboardingErrorDto(OnboardingErrorCode.NotFound, "The requested TFS resource was not found.", Sanitize(ex.Message), false);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
        catch (TfsAuthorizationException ex)
        {
            var error = new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "TFS denied the requested lookup.", Sanitize(ex.Message), false);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
        catch (TfsAuthenticationException ex)
        {
            var error = new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "TFS authentication failed for the requested lookup.", Sanitize(ex.Message), false);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
        catch (TimeoutException ex)
        {
            var error = new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "TFS did not respond before the configured timeout.", Sanitize(ex.Message), true);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
        catch (HttpRequestException ex)
        {
            var error = new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "TFS is currently unavailable.", Sanitize(ex.Message), true);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
        catch (TfsException ex)
        {
            var error = new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "TFS is currently unavailable.", Sanitize(ex.Message), true);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected onboarding lookup failure. Operation={Operation}", operation);
            var error = new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "TFS lookup failed unexpectedly.", Sanitize(ex.Message), true);
            _observability.RecordLookupCompleted(operation, false, error.Code);
            _observability.LogLookupCompleted(operation, false, error.Code);
            return OnboardingOperationResult<T>.Failure(error);
        }
    }

    private static ProjectLookupResultDto MapProject(TfsProjectDto project)
        => new(project.Id, project.Name, project.Description);

    private static TeamLookupResultDto MapTeam(TfsTeamDto team, string projectExternalId)
        => new(team.Id, projectExternalId, team.Name, team.Description, team.DefaultAreaPath);

    private static PipelineLookupResultDto MapPipeline(PipelineDefinitionDto definition, string projectExternalId)
        => new(
            definition.PipelineDefinitionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            projectExternalId,
            definition.Name,
            definition.Folder,
            definition.YamlPath,
            definition.RepoId,
            definition.RepoName);

    private static WorkItemLookupResultDto MapWorkItem(WorkItemDto workItem, string projectExternalId)
        => new(
            workItem.TfsId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            workItem.Title,
            workItem.Type,
            workItem.State,
            projectExternalId,
            workItem.AreaPath);

    private static bool MatchesQuery(string? value, string? query)
        => string.IsNullOrWhiteSpace(query) ||
           (!string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsIgnoreCase(IReadOnlyCollection<string> values, string candidate)
        => values.Any(value => value.Equals(candidate, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveProjectExternalId(
        IReadOnlyList<ProjectLookupResultDto> projects,
        string areaPath)
    {
        var areaRoot = areaPath.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(areaRoot))
        {
            return null;
        }

        return projects.FirstOrDefault(project => project.Name.Equals(areaRoot, StringComparison.OrdinalIgnoreCase))?.ProjectExternalId;
    }

    private static OnboardingErrorDto CreateNotFoundError(string message, string details)
        => new(OnboardingErrorCode.NotFound, message, details, false);

    private static OnboardingErrorDto CreateValidationError(string message, string details)
        => new(OnboardingErrorCode.ValidationFailed, message, details, false);

    private static string Sanitize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "No additional details." : value;
}
