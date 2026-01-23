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

    /// <summary>
    /// Temporary routes mapping to legacy pages until workspaces are implemented.
    /// These will be removed once all workspaces are complete.
    /// </summary>
    public static class LegacyTemporary
    {
        /// <summary>
        /// Legacy route for Overzien intent (until Product Workspace - Phase 3).
        /// </summary>
        public const string Overzien = "/product-home";

        /// <summary>
        /// Legacy route for Begrijpen intent (until Analysis Workspace - Phase 5).
        /// </summary>
        public const string Begrijpen = "/backlog-health";

        /// <summary>
        /// Legacy route for Plannen intent (until Planning Workspace - Phase 6).
        /// </summary>
        public const string Plannen = "/release-planning";

        /// <summary>
        /// Legacy route for Delen intent (until Communication Workspace - Phase 4).
        /// No direct equivalent exists, so we route to product home.
        /// </summary>
        public const string Delen = "/product-home";

        /// <summary>
        /// Gets the temporary legacy route for a given intent.
        /// Used during migration when workspaces are not yet implemented.
        /// </summary>
        /// <param name="intent">The navigation intent.</param>
        /// <returns>The legacy page route for the intent.</returns>
        public static string GetLegacyRouteForIntent(Intent intent)
        {
            return intent switch
            {
                Intent.Overzien => Overzien,
                Intent.Begrijpen => Begrijpen,
                Intent.Plannen => Plannen,
                Intent.Delen => Delen,
                _ => Overzien
            };
        }
    }
}
