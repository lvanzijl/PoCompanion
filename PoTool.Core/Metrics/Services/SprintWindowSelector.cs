using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Services;

/// <summary>
/// Service for selecting sprint windows based on dates for Health workspace displays.
/// Replaces lexicographic iteration path sorting with proper date-based selection.
/// </summary>
public class SprintWindowSelector
{
    /// <summary>
    /// Gets sprints for Backlog Health Analysis section: current + 2 future sprints.
    /// Returns up to 3 sprints total, excluding past sprints.
    /// </summary>
    /// <param name="allSprints">All available sprints with dates</param>
    /// <param name="today">Reference date for determining current/past/future</param>
    /// <returns>List containing current sprint (if exists) + up to 2 future sprints, chronologically ordered</returns>
    public IReadOnlyList<SprintMetricsDto> GetBacklogHealthSprints(
        IEnumerable<SprintMetricsDto> allSprints,
        DateTimeOffset today)
    {
        var sprintsList = allSprints.ToList();
        
        // Filter out sprints without dates (can't determine current/past/future)
        var sprintsWithDates = sprintsList
            .Where(s => s.StartDate.HasValue && s.EndDate.HasValue)
            .ToList();
        
        if (sprintsWithDates.Count == 0)
        {
            // Fallback: return empty or log warning
            return Array.Empty<SprintMetricsDto>();
        }
        
        var result = new List<SprintMetricsDto>();
        
        // Find current sprint: StartDate <= today < EndDate
        var currentSprint = sprintsWithDates
            .FirstOrDefault(s => s.StartDate!.Value <= today && s.EndDate!.Value > today);
        
        // If no current sprint, use fallback logic
        if (currentSprint == null)
        {
            // Try to use earliest future sprint as "current"
            currentSprint = sprintsWithDates
                .Where(s => s.StartDate!.Value > today)
                .OrderBy(s => s.StartDate!.Value)
                .FirstOrDefault();
            
            // If still no current, use latest past sprint as "current"
            if (currentSprint == null)
            {
                currentSprint = sprintsWithDates
                    .Where(s => s.EndDate!.Value <= today)
                    .OrderByDescending(s => s.EndDate!.Value)
                    .FirstOrDefault();
            }
        }
        
        // Add current sprint if found
        if (currentSprint != null)
        {
            result.Add(currentSprint);
        }
        
        // Get future sprints: StartDate > today, ordered ascending
        // Exclude current sprint if it's a future sprint being used as current
        var futureSprints = sprintsWithDates
            .Where(s => s.StartDate!.Value > today && s != currentSprint)
            .OrderBy(s => s.StartDate!.Value)
            .Take(2) // Next 2 future sprints
            .ToList();
        
        result.AddRange(futureSprints);
        
        return result;
    }
    
    /// <summary>
    /// Gets sprints for Issue Comparison Across Iterations section: 3 past + current + 2 future sprints.
    /// Returns up to 6 sprints total, chronologically ordered.
    /// </summary>
    /// <param name="allSprints">All available sprints with dates</param>
    /// <param name="today">Reference date for determining current/past/future</param>
    /// <returns>List containing up to 3 past + current + up to 2 future sprints, chronologically ordered</returns>
    public IReadOnlyList<SprintMetricsDto> GetIssueComparisonSprints(
        IEnumerable<SprintMetricsDto> allSprints,
        DateTimeOffset today)
    {
        var sprintsList = allSprints.ToList();
        
        // Filter out sprints without dates
        var sprintsWithDates = sprintsList
            .Where(s => s.StartDate.HasValue && s.EndDate.HasValue)
            .ToList();
        
        if (sprintsWithDates.Count == 0)
        {
            return Array.Empty<SprintMetricsDto>();
        }
        
        var result = new List<SprintMetricsDto>();
        
        // Get past sprints: EndDate <= today, ordered descending (most recent first), take 3
        var pastSprints = sprintsWithDates
            .Where(s => s.EndDate!.Value <= today)
            .OrderByDescending(s => s.EndDate!.Value)
            .Take(3)
            .Reverse() // Reverse to get chronological order (oldest to newest)
            .ToList();
        
        result.AddRange(pastSprints);
        
        // Find current sprint: StartDate <= today < EndDate
        var currentSprint = sprintsWithDates
            .FirstOrDefault(s => s.StartDate!.Value <= today && s.EndDate!.Value > today);
        
        // If no current sprint, use fallback logic
        if (currentSprint == null)
        {
            // Try to use earliest future sprint as "current"
            currentSprint = sprintsWithDates
                .Where(s => s.StartDate!.Value > today)
                .OrderBy(s => s.StartDate!.Value)
                .FirstOrDefault();
            
            // If still no current, use latest past sprint as "current"
            // (but only if we haven't already included it in pastSprints)
            if (currentSprint == null)
            {
                var latestPast = sprintsWithDates
                    .Where(s => s.EndDate!.Value <= today)
                    .OrderByDescending(s => s.EndDate!.Value)
                    .FirstOrDefault();
                
                if (latestPast != null && !result.Contains(latestPast))
                {
                    currentSprint = latestPast;
                }
            }
        }
        
        // Add current sprint if found
        if (currentSprint != null)
        {
            result.Add(currentSprint);
        }
        
        // Get future sprints: StartDate > today, ordered ascending, take 2
        // Exclude current sprint if it's a future sprint being used as current
        var futureSprints = sprintsWithDates
            .Where(s => s.StartDate!.Value > today && s != currentSprint)
            .OrderBy(s => s.StartDate!.Value)
            .Take(2)
            .ToList();
        
        result.AddRange(futureSprints);
        
        return result;
    }
}
