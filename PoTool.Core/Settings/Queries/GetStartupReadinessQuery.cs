using Mediator;

namespace PoTool.Core.Settings.Queries;

/// <summary>
/// Query to retrieve the startup readiness state.
/// Used by the Startup Orchestrator to determine where to route the user.
/// </summary>
public sealed record GetStartupReadinessQuery : IQuery<StartupReadinessDto>;
