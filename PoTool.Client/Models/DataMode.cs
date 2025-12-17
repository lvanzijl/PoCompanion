namespace PoTool.Client.Models;

/// <summary>
/// Represents the data source mode for the application.
/// NOTE: This is intentionally duplicated from Core.Settings.DataMode
/// as the Client layer cannot reference Core directly per architecture rules.
/// This serves as the API contract boundary. Keep in sync with Core definition.
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
