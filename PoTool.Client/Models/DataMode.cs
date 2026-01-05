namespace PoTool.Client.Models;

/// <summary>
/// Represents the data source mode for the application.
/// </summary>
public enum DataMode
{
    /// <summary>
    /// Uses static mock data for testing and development.
    /// </summary>
    Mock = 0,

    /// <summary>
    /// Uses live data from TFS/Azure DevOps.
    /// </summary>
    Tfs = 1
}
