using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Queries;

/// <summary>
/// Query to retrieve all Features for a specific Epic.
/// Used in the Epic Split dialog to select which Features to move to the extracted Epic.
/// </summary>
/// <param name="EpicId">The TFS ID of the Epic.</param>
public sealed record GetEpicFeaturesQuery(int EpicId) : IQuery<IReadOnlyList<EpicFeatureDto>>;
