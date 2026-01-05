using Mediator;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Core.ReleasePlanning.Commands;

/// <summary>
/// Command to split an Epic into two Epics.
/// This creates a new Epic in TFS (the only TFS write operation on the board).
/// </summary>
/// <param name="OriginalEpicId">The TFS ID of the Epic to split.</param>
/// <param name="ExtractedEpicTitle">The title for the new (extracted) Epic.</param>
/// <param name="FeatureIdsForExtractedEpic">Feature TFS IDs to move to the extracted Epic.</param>
public sealed record SplitEpicCommand(
    int OriginalEpicId,
    string ExtractedEpicTitle,
    IReadOnlyList<int> FeatureIdsForExtractedEpic) : ICommand<EpicSplitResultDto>;
