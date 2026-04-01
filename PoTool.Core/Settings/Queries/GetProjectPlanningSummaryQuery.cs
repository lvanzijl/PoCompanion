using Mediator;
using PoTool.Shared.Planning;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to get a read-only planning summary for a project resolved by alias or internal identifier.
/// </summary>
public sealed record GetProjectPlanningSummaryQuery(string AliasOrId) : IQuery<ProjectPlanningSummaryDto?>;
