using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for classifying work items based on area paths.
/// </summary>
public class WorkItemClassificationService : IWorkItemClassificationService
{
    /// <inheritdoc />
    public TeamDto? ClassifyWorkItem(string workItemAreaPath, IEnumerable<TeamDto> linkedTeams)
    {
        if (string.IsNullOrWhiteSpace(workItemAreaPath) || linkedTeams == null || !linkedTeams.Any())
        {
            return null;
        }

        // Normalize work item area path
        var normalizedWorkItemPath = NormalizePath(workItemAreaPath);

        TeamDto? bestMatch = null;
        int bestMatchLength = -1;

        foreach (var team in linkedTeams)
        {
            var normalizedTeamPath = NormalizePath(team.TeamAreaPath);

            // Check if work item path starts with or equals team path
            if (normalizedWorkItemPath.Equals(normalizedTeamPath, StringComparison.OrdinalIgnoreCase) ||
                normalizedWorkItemPath.StartsWith(normalizedTeamPath + "\\", StringComparison.OrdinalIgnoreCase))
            {
                // Most specific match wins (longest path)
                if (normalizedTeamPath.Length > bestMatchLength)
                {
                    bestMatchLength = normalizedTeamPath.Length;
                    bestMatch = team;
                }
            }
        }

        return bestMatch;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        // Trim and ensure consistent separator (backslash)
        return path.Trim().TrimEnd('\\');
    }
}
