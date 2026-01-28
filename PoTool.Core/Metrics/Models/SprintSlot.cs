using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Models;

/// <summary>
/// Represents a sprint slot in a window selection, which can be either a real sprint or a placeholder.
/// Used by SprintWindowSelector to maintain fixed-size windows with placeholders for missing future sprints.
/// </summary>
public sealed class SprintSlot
{
    /// <summary>
    /// The real sprint data, if this is a real sprint. Null if this is a placeholder.
    /// </summary>
    public SprintMetricsDto? Sprint { get; init; }
    
    /// <summary>
    /// Indicates whether this is a placeholder slot (no real sprint data available).
    /// </summary>
    public bool IsPlaceholder { get; init; }
    
    /// <summary>
    /// Message to display for placeholder slots (e.g., "newer sprints aren't available").
    /// Null for real sprints.
    /// </summary>
    public string? PlaceholderMessage { get; init; }
    
    /// <summary>
    /// The start date for ordering. For placeholders, this is a synthetic date after the last known sprint.
    /// </summary>
    public DateTimeOffset StartDate { get; init; }
    
    /// <summary>
    /// The end date for ordering. For placeholders, this is a synthetic date.
    /// </summary>
    public DateTimeOffset EndDate { get; init; }
    
    /// <summary>
    /// The display name. For real sprints, this comes from SprintMetricsDto.SprintName.
    /// For placeholders, this is "[undefined]".
    /// </summary>
    public string DisplayName { get; init; }
    
    /// <summary>
    /// The iteration path. For real sprints, this comes from SprintMetricsDto.IterationPath.
    /// For placeholders, this is "[undefined]".
    /// </summary>
    public string IterationPath { get; init; }
    
    /// <summary>
    /// Creates a real sprint slot from SprintMetricsDto.
    /// </summary>
    public static SprintSlot FromSprint(SprintMetricsDto sprint)
    {
        if (!sprint.StartDate.HasValue || !sprint.EndDate.HasValue)
        {
            throw new ArgumentException("Sprint must have StartDate and EndDate to create a slot", nameof(sprint));
        }
        
        return new SprintSlot
        {
            Sprint = sprint,
            IsPlaceholder = false,
            PlaceholderMessage = null,
            StartDate = sprint.StartDate.Value,
            EndDate = sprint.EndDate.Value,
            DisplayName = sprint.SprintName,
            IterationPath = sprint.IterationPath
        };
    }
    
    /// <summary>
    /// Creates a placeholder sprint slot for a missing future sprint.
    /// </summary>
    /// <param name="startDate">Synthetic start date for ordering (typically after the last known sprint)</param>
    /// <param name="endDate">Synthetic end date for ordering</param>
    /// <param name="message">Message to display (e.g., "newer sprints aren't available")</param>
    public static SprintSlot CreatePlaceholder(DateTimeOffset startDate, DateTimeOffset endDate, string message)
    {
        return new SprintSlot
        {
            Sprint = null,
            IsPlaceholder = true,
            PlaceholderMessage = message,
            StartDate = startDate,
            EndDate = endDate,
            DisplayName = "[undefined]",
            IterationPath = "[undefined]"
        };
    }
    
    // Private constructor to enforce factory methods
    private SprintSlot() 
    {
        DisplayName = string.Empty;
        IterationPath = string.Empty;
    }
}
