using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get a project by alias or internal identifier.
/// </summary>
public sealed record GetProjectByAliasQuery(string AliasOrId) : IQuery<ProjectDto?>;
