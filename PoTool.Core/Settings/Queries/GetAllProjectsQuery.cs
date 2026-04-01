using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all projects.
/// </summary>
public sealed record GetAllProjectsQuery() : IQuery<IEnumerable<ProjectDto>>;
