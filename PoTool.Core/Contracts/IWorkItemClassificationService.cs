using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Service for classifying work items based on area paths.
/// </summary>
public interface IWorkItemClassificationService
{
    /// <summary>
    /// Classifies a work item to a team based on its area path.
    /// Returns the most specific match if multiple teams match.
    /// </summary>
    /// <param name="workItemAreaPath">The area path of the work item</param>
    /// <param name="linkedTeams">The teams linked to the product</param>
    /// <returns>The matching team, or null if no team matches (unassigned)</returns>
    TeamDto? ClassifyWorkItem(string workItemAreaPath, IEnumerable<TeamDto> linkedTeams);
}
