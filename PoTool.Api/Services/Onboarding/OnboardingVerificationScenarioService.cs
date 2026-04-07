using Microsoft.Extensions.Options;
using PoTool.Api.Configuration;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public sealed class OnboardingVerificationOptions
{
    public string SelectedScenario { get; set; } = OnboardingVerificationScenarioNames.HappyBindingChain;
}

public static class OnboardingVerificationScenarioNames
{
    public const string HappyBindingChain = "happy-binding-chain";
    public const string MissingRoot = "missing-root";
    public const string TeamAssignment = "team-assignment";
    public const string PipelineAssignment = "pipeline-assignment";
    public const string PermissionDenied = "permission-denied";
    public const string StaleProject = "stale-project";
    public const string TfsUnavailable = "tfs-unavailable";
    public const string LookupEmpty = "lookup-empty";
}

public static class OnboardingVerificationFailureKeys
{
    public const string Projects = "projects";

    public static string Teams(string projectExternalId) => $"teams:{Normalize(projectExternalId)}";

    public static string Pipelines(string projectExternalId) => $"pipelines:{Normalize(projectExternalId)}";

    public static string WorkItemSearch(string? projectExternalId)
        => $"work-items-search:{Normalize(projectExternalId)}";

    public static string WorkItemGet(string workItemExternalId) => $"work-items-get:{Normalize(workItemExternalId)}";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "*"
            : value.Trim().ToLowerInvariant();
}

public sealed record OnboardingVerificationScenarioDefinition(
    string Name,
    OnboardingVerificationSeed Seed,
    OnboardingVerificationLookup Lookup,
    OnboardingVerificationFailureModes FailureModes,
    IReadOnlyList<string> ExpectedFinalGatePaths);

public sealed record OnboardingVerificationSeed(
    bool IncludeConnection,
    IReadOnlyList<string> ProjectExternalIds,
    IReadOnlyList<string> TeamExternalIds,
    IReadOnlyList<string> InvalidTeamExternalIds,
    IReadOnlyList<string> PipelineExternalIds,
    IReadOnlyList<string> InvalidPipelineExternalIds,
    IReadOnlyList<string> ProductRootExternalIds,
    IReadOnlyList<OnboardingVerificationBindingSeed> Bindings);

public sealed record OnboardingVerificationBindingSeed(
    string ProductRootExternalId,
    OnboardingProductSourceTypeDto SourceType,
    string SourceExternalId,
    bool Enabled = true);

public sealed record OnboardingVerificationLookup(
    IReadOnlyList<ProjectLookupResultDto> Projects,
    IReadOnlyList<TeamLookupResultDto> Teams,
    IReadOnlyList<PipelineLookupResultDto> Pipelines,
    IReadOnlyList<WorkItemLookupResultDto> WorkItems);

public sealed record OnboardingVerificationFailureModes(
    IReadOnlyDictionary<string, OnboardingErrorDto> ForcedFailures);

public interface IOnboardingVerificationScenarioService
{
    bool IsEnabled { get; }

    string? SelectedScenarioName { get; }

    IReadOnlyCollection<string> AvailableScenarioNames { get; }

    OnboardingVerificationScenarioDefinition? CurrentScenario { get; }

    bool TryGetProjects(string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>> result);

    bool TryGetTeams(string projectExternalId, string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>> result);

    bool TryGetPipelines(string projectExternalId, string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>> result);

    bool TrySearchWorkItems(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, out OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>> result);

    bool TryGetWorkItem(string workItemExternalId, out OnboardingOperationResult<WorkItemLookupResultDto> result);
}

public sealed class OnboardingVerificationScenarioService : IOnboardingVerificationScenarioService
{
    private const string ProjectExternalId = "battleship-systems-project";
    private const string ProjectName = "Battleship Systems";
    private const string TeamAlphaExternalId = "team-incident-response";
    private const string TeamBravoExternalId = "team-crew-safety";
    private const string PipelineAlphaExternalId = "9101";
    private const string PipelineBravoExternalId = "9102";
    private const string RootIncidentExternalId = "1001";
    private const string RootCrewExternalId = "1002";

    private static readonly IReadOnlyDictionary<string, OnboardingVerificationScenarioDefinition> Catalog =
        BuildCatalog();

    public static IOnboardingVerificationScenarioService Disabled { get; } = new DisabledOnboardingVerificationScenarioService();

    public OnboardingVerificationScenarioService(TfsRuntimeMode runtimeMode, IOptions<OnboardingVerificationOptions> options)
    {
        AvailableScenarioNames = Catalog.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (!runtimeMode.UseMockClient)
        {
            return;
        }

        IsEnabled = true;
        var requestedScenario = string.IsNullOrWhiteSpace(options.Value.SelectedScenario)
            ? OnboardingVerificationScenarioNames.HappyBindingChain
            : options.Value.SelectedScenario.Trim();

        if (!Catalog.TryGetValue(requestedScenario, out var scenario))
        {
            throw new InvalidOperationException(
                $"Unknown onboarding verification scenario '{requestedScenario}'. Supported scenarios: {string.Join(", ", AvailableScenarioNames)}.");
        }

        SelectedScenarioName = scenario.Name;
        CurrentScenario = scenario;
    }

    public bool IsEnabled { get; }

    public string? SelectedScenarioName { get; }

    public IReadOnlyCollection<string> AvailableScenarioNames { get; }

    public OnboardingVerificationScenarioDefinition? CurrentScenario { get; }

    public bool TryGetProjects(string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>> result)
    {
        if (!TryBegin(out result))
        {
            return false;
        }

        if (TryGetFailure(OnboardingVerificationFailureKeys.Projects, out var error))
        {
            result = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Failure(error);
            return true;
        }

        result = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(CurrentScenario!.Lookup.Projects
            .Where(project => MatchesQuery(project.Name, query) || MatchesQuery(project.Description, query))
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(skip, 0))
            .Take(NormalizeTop(top))
            .ToArray());
        return true;
    }

    public bool TryGetTeams(string projectExternalId, string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>> result)
    {
        if (!TryBegin(out result))
        {
            return false;
        }

        if (TryGetFailure(OnboardingVerificationFailureKeys.Teams(projectExternalId), out var error))
        {
            result = OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Failure(error);
            return true;
        }

        result = OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(CurrentScenario!.Lookup.Teams
            .Where(team => team.ProjectExternalId.Equals(projectExternalId, StringComparison.OrdinalIgnoreCase))
            .Where(team => MatchesQuery(team.Name, query) || MatchesQuery(team.Description, query) || MatchesQuery(team.DefaultAreaPath, query))
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(skip, 0))
            .Take(NormalizeTop(top))
            .ToArray());
        return true;
    }

    public bool TryGetPipelines(string projectExternalId, string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>> result)
    {
        if (!TryBegin(out result))
        {
            return false;
        }

        if (TryGetFailure(OnboardingVerificationFailureKeys.Pipelines(projectExternalId), out var error))
        {
            result = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Failure(error);
            return true;
        }

        result = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(CurrentScenario!.Lookup.Pipelines
            .Where(pipeline => pipeline.ProjectExternalId.Equals(projectExternalId, StringComparison.OrdinalIgnoreCase))
            .Where(pipeline =>
                MatchesQuery(pipeline.Name, query) ||
                MatchesQuery(pipeline.Folder, query) ||
                MatchesQuery(pipeline.YamlPath, query) ||
                MatchesQuery(pipeline.RepositoryName, query))
            .OrderBy(pipeline => pipeline.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(skip, 0))
            .Take(NormalizeTop(top))
            .ToArray());
        return true;
    }

    public bool TrySearchWorkItems(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, out OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>> result)
    {
        if (!TryBegin(out result))
        {
            return false;
        }

        if (TryGetFailure(OnboardingVerificationFailureKeys.WorkItemSearch(projectExternalId), out var error))
        {
            result = OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>.Failure(error);
            return true;
        }

        var filtered = CurrentScenario!.Lookup.WorkItems
            .Where(workItem => string.IsNullOrWhiteSpace(projectExternalId) || workItem.ProjectExternalId.Equals(projectExternalId, StringComparison.OrdinalIgnoreCase))
            .Where(workItem => MatchesQuery(workItem.Title, query) || MatchesQuery(workItem.WorkItemExternalId, query))
            .Where(workItem => workItemTypes is null || workItemTypes.Count == 0 || workItemTypes.Contains(workItem.WorkItemType, StringComparer.OrdinalIgnoreCase))
            .OrderBy(workItem => workItem.Title, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(skip, 0))
            .Take(NormalizeTop(top))
            .ToArray();

        result = OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>.Success(filtered);
        return true;
    }

    public bool TryGetWorkItem(string workItemExternalId, out OnboardingOperationResult<WorkItemLookupResultDto> result)
    {
        if (!TryBegin(out result))
        {
            return false;
        }

        if (TryGetFailure(OnboardingVerificationFailureKeys.WorkItemGet(workItemExternalId), out var error))
        {
            result = OnboardingOperationResult<WorkItemLookupResultDto>.Failure(error);
            return true;
        }

        var workItem = CurrentScenario!.Lookup.WorkItems.FirstOrDefault(item =>
            item.WorkItemExternalId.Equals(workItemExternalId, StringComparison.OrdinalIgnoreCase));
        result = workItem is null
            ? OnboardingOperationResult<WorkItemLookupResultDto>.Failure(new OnboardingErrorDto(
                OnboardingErrorCode.NotFound,
                $"Work item '{workItemExternalId}' was not found.",
                workItemExternalId,
                false))
            : OnboardingOperationResult<WorkItemLookupResultDto>.Success(workItem);
        return true;
    }

    private bool TryBegin<T>(out OnboardingOperationResult<T> result)
    {
        if (!IsEnabled || CurrentScenario is null)
        {
            result = null!;
            return false;
        }

        result = null!;
        return true;
    }

    private bool TryGetFailure(string key, out OnboardingErrorDto error)
    {
        if (CurrentScenario is not null && CurrentScenario.FailureModes.ForcedFailures.TryGetValue(key, out error!))
        {
            return true;
        }

        error = null!;
        return false;
    }

    private static int NormalizeTop(int top) => top <= 0 ? int.MaxValue : top;

    private static bool MatchesQuery(string? value, string? query)
        => string.IsNullOrWhiteSpace(query)
            || (!string.IsNullOrWhiteSpace(value) && value.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, OnboardingVerificationScenarioDefinition> BuildCatalog()
    {
        var projects = new[]
        {
            new ProjectLookupResultDto(ProjectExternalId, ProjectName, "Deterministic onboarding verification project")
        };

        var teams = new[]
        {
            new TeamLookupResultDto(TeamAlphaExternalId, ProjectExternalId, "Incident Response Team", "Primary binding team", $"{ProjectName}\\Incident Response"),
            new TeamLookupResultDto(TeamBravoExternalId, ProjectExternalId, "Crew Safety Team", "Secondary binding team", $"{ProjectName}\\Crew Safety")
        };

        var pipelines = new[]
        {
            new PipelineLookupResultDto(PipelineAlphaExternalId, ProjectExternalId, "Incident Backend CI", "\\Incident", "/azure-pipelines/incident-ci.yml", "repo-incident-backend", "Incident Backend"),
            new PipelineLookupResultDto(PipelineBravoExternalId, ProjectExternalId, "Crew Safety Deploy", "\\CrewSafety", "/azure-pipelines/crew-safety-deploy.yml", "repo-crew-safety", "Crew Safety")
        };

        var workItems = new[]
        {
            new WorkItemLookupResultDto(RootIncidentExternalId, "Incident Response Control", "Epic", "New", ProjectExternalId, $"{ProjectName}\\Incident Response"),
            new WorkItemLookupResultDto(RootCrewExternalId, "Crew Safety Operations", "Epic", "New", ProjectExternalId, $"{ProjectName}\\Crew Safety")
        };

        var emptyLookup = new OnboardingVerificationLookup(projects, Array.Empty<TeamLookupResultDto>(), Array.Empty<PipelineLookupResultDto>(), Array.Empty<WorkItemLookupResultDto>());
        var fullLookup = new OnboardingVerificationLookup(projects, teams, pipelines, workItems);

        return new Dictionary<string, OnboardingVerificationScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [OnboardingVerificationScenarioNames.HappyBindingChain] = new(
                OnboardingVerificationScenarioNames.HappyBindingChain,
                new OnboardingVerificationSeed(
                    IncludeConnection: true,
                    ProjectExternalIds: Array.Empty<string>(),
                    TeamExternalIds: Array.Empty<string>(),
                    InvalidTeamExternalIds: Array.Empty<string>(),
                    PipelineExternalIds: Array.Empty<string>(),
                    InvalidPipelineExternalIds: Array.Empty<string>(),
                    ProductRootExternalIds: Array.Empty<string>(),
                    Bindings: Array.Empty<OnboardingVerificationBindingSeed>()),
                fullLookup,
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)),
                ["project-link", "root-create", "project-binding", "team-binding"]),
            [OnboardingVerificationScenarioNames.MissingRoot] = new(
                OnboardingVerificationScenarioNames.MissingRoot,
                new OnboardingVerificationSeed(
                    IncludeConnection: true,
                    ProjectExternalIds: [ProjectExternalId],
                    TeamExternalIds: Array.Empty<string>(),
                    InvalidTeamExternalIds: Array.Empty<string>(),
                    PipelineExternalIds: Array.Empty<string>(),
                    InvalidPipelineExternalIds: Array.Empty<string>(),
                    ProductRootExternalIds: Array.Empty<string>(),
                    Bindings: Array.Empty<OnboardingVerificationBindingSeed>()),
                emptyLookup with { Projects = projects },
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)),
                ["root-create"]),
            [OnboardingVerificationScenarioNames.TeamAssignment] = new(
                OnboardingVerificationScenarioNames.TeamAssignment,
                new OnboardingVerificationSeed(
                    IncludeConnection: true,
                    ProjectExternalIds: [ProjectExternalId],
                    TeamExternalIds: [TeamBravoExternalId],
                    InvalidTeamExternalIds: [TeamAlphaExternalId],
                    PipelineExternalIds: Array.Empty<string>(),
                    InvalidPipelineExternalIds: Array.Empty<string>(),
                    ProductRootExternalIds: [RootIncidentExternalId],
                    Bindings:
                    [
                        new OnboardingVerificationBindingSeed(RootIncidentExternalId, OnboardingProductSourceTypeDto.Project, ProjectExternalId),
                        new OnboardingVerificationBindingSeed(RootIncidentExternalId, OnboardingProductSourceTypeDto.Team, TeamAlphaExternalId)
                    ]),
                fullLookup,
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)),
                ["team-assignment"]),
            [OnboardingVerificationScenarioNames.PipelineAssignment] = new(
                OnboardingVerificationScenarioNames.PipelineAssignment,
                new OnboardingVerificationSeed(
                    IncludeConnection: true,
                    ProjectExternalIds: [ProjectExternalId],
                    TeamExternalIds: Array.Empty<string>(),
                    InvalidTeamExternalIds: Array.Empty<string>(),
                    PipelineExternalIds: [PipelineBravoExternalId],
                    InvalidPipelineExternalIds: [PipelineAlphaExternalId],
                    ProductRootExternalIds: [RootIncidentExternalId],
                    Bindings:
                    [
                        new OnboardingVerificationBindingSeed(RootIncidentExternalId, OnboardingProductSourceTypeDto.Project, ProjectExternalId),
                        new OnboardingVerificationBindingSeed(RootIncidentExternalId, OnboardingProductSourceTypeDto.Pipeline, PipelineAlphaExternalId)
                    ]),
                fullLookup,
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)),
                ["pipeline-assignment"]),
            [OnboardingVerificationScenarioNames.PermissionDenied] = new(
                OnboardingVerificationScenarioNames.PermissionDenied,
                new OnboardingVerificationSeed(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OnboardingVerificationBindingSeed>()),
                fullLookup,
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)
                {
                    [OnboardingVerificationFailureKeys.Projects] = new(
                        OnboardingErrorCode.PermissionDenied,
                        "TFS denied the requested lookup.",
                        "verification-permission-denied",
                        false)
                }),
                ["permission-denied"]),
            [OnboardingVerificationScenarioNames.StaleProject] = new(
                OnboardingVerificationScenarioNames.StaleProject,
                new OnboardingVerificationSeed(
                    IncludeConnection: true,
                    ProjectExternalIds: [ProjectExternalId],
                    TeamExternalIds: Array.Empty<string>(),
                    InvalidTeamExternalIds: Array.Empty<string>(),
                    PipelineExternalIds: Array.Empty<string>(),
                    InvalidPipelineExternalIds: Array.Empty<string>(),
                    ProductRootExternalIds: Array.Empty<string>(),
                    Bindings: Array.Empty<OnboardingVerificationBindingSeed>()),
                fullLookup with { Projects = Array.Empty<ProjectLookupResultDto>() },
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)),
                ["stale-project"]),
            [OnboardingVerificationScenarioNames.TfsUnavailable] = new(
                OnboardingVerificationScenarioNames.TfsUnavailable,
                new OnboardingVerificationSeed(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OnboardingVerificationBindingSeed>()),
                fullLookup,
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)
                {
                    [OnboardingVerificationFailureKeys.Projects] = new(
                        OnboardingErrorCode.TfsUnavailable,
                        "TFS is currently unavailable.",
                        "verification-tfs-unavailable",
                        true)
                }),
                ["tfs-unavailable"]),
            [OnboardingVerificationScenarioNames.LookupEmpty] = new(
                OnboardingVerificationScenarioNames.LookupEmpty,
                new OnboardingVerificationSeed(
                    IncludeConnection: true,
                    ProjectExternalIds: [ProjectExternalId],
                    TeamExternalIds: Array.Empty<string>(),
                    InvalidTeamExternalIds: Array.Empty<string>(),
                    PipelineExternalIds: Array.Empty<string>(),
                    InvalidPipelineExternalIds: Array.Empty<string>(),
                    ProductRootExternalIds: [RootIncidentExternalId],
                    Bindings:
                    [
                        new OnboardingVerificationBindingSeed(RootIncidentExternalId, OnboardingProductSourceTypeDto.Project, ProjectExternalId)
                    ]),
                emptyLookup with { Projects = projects, WorkItems = workItems },
                new OnboardingVerificationFailureModes(new Dictionary<string, OnboardingErrorDto>(StringComparer.OrdinalIgnoreCase)),
                ["lookup-empty"]),
        };
    }

    private sealed class DisabledOnboardingVerificationScenarioService : IOnboardingVerificationScenarioService
    {
        public bool IsEnabled => false;

        public string? SelectedScenarioName => null;

        public IReadOnlyCollection<string> AvailableScenarioNames => Array.Empty<string>();

        public OnboardingVerificationScenarioDefinition? CurrentScenario => null;

        public bool TryGetProjects(string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>> result)
        {
            result = null!;
            return false;
        }

        public bool TryGetTeams(string projectExternalId, string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>> result)
        {
            result = null!;
            return false;
        }

        public bool TryGetPipelines(string projectExternalId, string? query, int top, int skip, out OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>> result)
        {
            result = null!;
            return false;
        }

        public bool TrySearchWorkItems(string? query, string? projectExternalId, IReadOnlyCollection<string>? workItemTypes, int top, int skip, out OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>> result)
        {
            result = null!;
            return false;
        }

        public bool TryGetWorkItem(string workItemExternalId, out OnboardingOperationResult<WorkItemLookupResultDto> result)
        {
            result = null!;
            return false;
        }
    }
}
