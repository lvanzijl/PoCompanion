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

    #region Beta Navigation Routes

    /// <summary>
    /// Beta Home route - entry point for new workspace-based navigation.
    /// </summary>
    public const string BetaHome = "/beta";

    /// <summary>
    /// Beta Health (Now) workspace route.
    /// </summary>
    public const string BetaHealthWorkspace = "/beta/health";

    /// <summary>
    /// Beta Overview/Trends (Past) workspace route.
    /// </summary>
    public const string BetaTrendsWorkspace = "/beta/trends";

    /// <summary>
    /// Beta Planning (Future) workspace route.
    /// </summary>
    public const string BetaPlanningWorkspace = "/beta/planning";

    /// <summary>
    /// Beta Bug Overview route.
    /// </summary>
    public const string BetaBugOverview = "/beta/bugs";

    /// <summary>
    /// Beta Bug Detail route.
    /// </summary>
    public const string BetaBugDetail = "/beta/bugs/detail";

    /// <summary>
    /// Beta PR Insights route - shows metrics dashboard with Team/Product selectors.
    /// </summary>
    public const string BetaPrOverview = "/beta/pull-requests";

    /// <summary>
    /// Beta Pipeline Overview route (read-only).
    /// </summary>
    public const string BetaPipelineOverview = "/beta/pipelines";

    /// <summary>
    /// Beta Plan Board route.
    /// </summary>
    public const string BetaPlanBoard = "/beta/plan-board";

    /// <summary>
    /// Beta Dependency Overview route (read-only).
    /// </summary>
    public const string BetaDependencyOverview = "/beta/dependencies";

    #endregion

    /// <summary>
    /// Work Item Explorer route.
    /// </summary>
    public const string WorkItems = "/workitems";

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
