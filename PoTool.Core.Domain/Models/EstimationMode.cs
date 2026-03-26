namespace PoTool.Core.Domain.Models;

/// <summary>
/// Canonical CDC estimation mode contract.
/// </summary>
public enum EstimationMode
{
    StoryPoints = 0,
    EffortHours = 1,
    Mixed = 2,
    NoSpMode = 3
}
