namespace PoTool.Shared.PullRequests;

/// <summary>
/// Status of a reviewer based on their response time.
/// </summary>
public enum ReviewerStatus
{
    Fast = 0,       // Average response < 4 hours
    Normal = 1,     // Average response 4-24 hours
    Slow = 2,       // Average response 24-48 hours
    Bottleneck = 3  // Average response > 48 hours
}
