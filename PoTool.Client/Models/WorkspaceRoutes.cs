namespace PoTool.Client.Models;

/// <summary>
/// Constants for workspace routes used in intent-driven navigation.
/// Centralizes route definitions to avoid duplication and reduce typo risk.
/// </summary>
public static class WorkspaceRoutes
{
    /// <summary>
    /// Landing page route for intent selection.
    /// </summary>
    public const string Landing = "/landing";

    /// <summary>
    /// Profile selection route.
    /// </summary>
    public const string Profiles = "/profiles";

    /// <summary>
    /// Product Workspace route.
    /// </summary>
    public const string ProductWorkspace = "/workspace/product";

    /// <summary>
    /// Team Workspace route.
    /// </summary>
    public const string TeamWorkspace = "/workspace/team";

    /// <summary>
    /// Analysis Workspace route.
    /// </summary>
    public const string AnalysisWorkspace = "/workspace/analysis";

    /// <summary>
    /// Planning Workspace route.
    /// </summary>
    public const string PlanningWorkspace = "/workspace/planning";

    /// <summary>
    /// Communication Workspace route.
    /// </summary>
    public const string CommunicationWorkspace = "/workspace/communication";

    /// <summary>
    /// Gets the target workspace route for a given intent.
    /// </summary>
    /// <param name="intent">The navigation intent.</param>
    /// <param name="scopeLevel">The current scope level.</param>
    /// <returns>The workspace route for the intent.</returns>
    public static string GetRouteForIntent(Intent intent, ScopeLevel scopeLevel = ScopeLevel.Portfolio)
    {
        return intent switch
        {
            Intent.Overzien when scopeLevel == ScopeLevel.Team => TeamWorkspace,
            Intent.Overzien => ProductWorkspace,
            Intent.Begrijpen => AnalysisWorkspace,
            Intent.Plannen => PlanningWorkspace,
            Intent.Delen => CommunicationWorkspace,
            _ => Landing
        };
    }
}
