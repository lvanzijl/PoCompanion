using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class ProjectIdentityMapper
{
    private readonly ProjectService _projectService;
    private readonly Dictionary<string, ProjectDto?> _projectsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliasByProjectId = new(StringComparer.OrdinalIgnoreCase);

    public ProjectIdentityMapper(ProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task<ProjectDto?> ResolveProjectAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aliasOrId))
        {
            return null;
        }

        var normalized = aliasOrId.Trim();
        if (_projectsByKey.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var project = await _projectService.GetProjectAsync(normalized, cancellationToken);
        Cache(project, normalized);
        return project;
    }

    public async Task<string?> ResolveProjectIdAsync(string aliasOrId, CancellationToken cancellationToken = default)
        => (await ResolveProjectAsync(aliasOrId, cancellationToken))?.Id;

    public async Task<string?> ResolveProjectAliasAsync(string projectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        if (_aliasByProjectId.TryGetValue(projectId.Trim(), out var cachedAlias))
        {
            return cachedAlias;
        }

        var project = await ResolveProjectAsync(projectId, cancellationToken);
        return project?.Alias;
    }

    private void Cache(ProjectDto? project, string lookupKey)
    {
        _projectsByKey[lookupKey] = project;
        if (project is null)
        {
            return;
        }

        _projectsByKey[project.Id] = project;
        _projectsByKey[project.Alias] = project;
        _aliasByProjectId[project.Id] = project.Alias;
    }
}
