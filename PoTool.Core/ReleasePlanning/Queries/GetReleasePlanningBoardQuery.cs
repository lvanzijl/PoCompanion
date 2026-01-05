using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Queries;

/// <summary>
/// Query to retrieve the complete Release Planning Board state.
/// </summary>
public sealed record GetReleasePlanningBoardQuery : IQuery<ReleasePlanningBoardDto>;
