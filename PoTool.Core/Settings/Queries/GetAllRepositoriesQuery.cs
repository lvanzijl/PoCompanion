using Mediator;
using PoTool.Shared.Settings;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get all repositories in the system.
/// </summary>
public sealed record GetAllRepositoriesQuery() : IQuery<IEnumerable<RepositoryDto>>;
