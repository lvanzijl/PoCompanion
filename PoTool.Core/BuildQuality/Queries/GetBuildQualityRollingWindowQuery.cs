using Mediator;
using PoTool.Shared.BuildQuality;

namespace PoTool.Core.BuildQuality.Queries;

/// <summary>
/// Retrieves rolling-window BuildQuality for the selected product owner scope.
/// </summary>
public sealed record GetBuildQualityRollingWindowQuery(
    int ProductOwnerId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc) : IQuery<BuildQualityPageDto>;
