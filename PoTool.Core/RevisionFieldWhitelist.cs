namespace PoTool.Core;

/// <summary>
/// Single source of truth for the revision field whitelist.
/// These are the TFS work item fields stored in revision headers/deltas
/// and used for validation comparison.
/// </summary>
public static class RevisionFieldWhitelist
{
    /// <summary>
    /// The canonical set of fields requested from the TFS reporting API
    /// and stored in revision data. Used by both ingestion and validation.
    /// </summary>
    public static readonly IReadOnlyList<string> Fields = new[]
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.Reason",
        "System.IterationPath",
        "System.AreaPath",
        "System.CreatedDate",
        "System.ChangedDate",
        "System.ChangedBy",
        "Microsoft.VSTS.Common.ClosedDate",
        "Microsoft.VSTS.Scheduling.Effort",
        "System.Tags",
        "Microsoft.VSTS.Common.Severity"
    };
}
