namespace PoTool.Client.Models;

/// <summary>
/// Constants for workspace routes used in intent-driven navigation.
/// Centralizes route definitions to avoid duplication and reduce typo risk.
/// </summary>
public static class WorkspaceRoutes
{
    /// <summary>
    /// Home page route - main workspace navigation entry point.
    /// </summary>
    public const string Home = "/home";
    
    /// <summary>
    /// Sync gate route - ensures cache is ready before navigating to home.
    /// </summary>
    public const string SyncGate = "/sync-gate";
    
    /// <summary>
    /// Legacy landing page route for classic intent-based navigation.
    /// </summary>
    public const string Legacy = "/legacy";

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

    #region Workspace Navigation Routes

    /// <summary>
    /// Health (Now) workspace route.
    /// </summary>
    public const string HealthWorkspace = "/home/health";

    /// <summary>
    /// Overview/Trends (Past) workspace route.
    /// </summary>
    public const string TrendsWorkspace = "/home/trends";

    /// <summary>
    /// Delivery workspace route — inspection of delivered work per sprint or aggregated.
    /// </summary>
    public const string DeliveryWorkspace = "/home/delivery";

    /// <summary>
    /// Planning (Future) workspace route.
    /// </summary>
    public const string PlanningWorkspace2 = "/home/planning";

    /// <summary>
    /// Bug Overview route.
    /// </summary>
    public const string BugOverview = "/home/bugs";

    /// <summary>
    /// Bug Detail route.
    /// </summary>
    public const string BugDetail = "/home/bugs/detail";

    /// <summary>
    /// Bug Triage route - for triaging and categorizing bugs.
    /// </summary>
    public const string BugTriage = "/bugs-triage";

    /// <summary>
    /// PR Insights route - shows metrics dashboard with Team/Product selectors.
    /// </summary>
    public const string PrOverview = "/home/pull-requests";

    /// <summary>
    /// Pipeline Overview route (read-only).
    /// </summary>
    public const string PipelineOverview = "/home/pipelines";

    /// <summary>
    /// Pipeline Insights route — PO-first stability overview per product, sprint-scoped (read-only).
    /// </summary>
    public const string PipelineInsights = "/home/pipeline-insights";

    /// <summary>
    /// Plan Board route.
    /// </summary>
    public const string PlanBoard = "/home/plan-board";

    /// <summary>
    /// Dependency Overview route (read-only).
    /// </summary>
    public const string DependencyOverview = "/home/dependencies";

    /// <summary>
    /// Sprint Trend route - shows sprint-based revision metrics.
    /// Legacy route kept for backward compatibility; canonical route is SprintDelivery.
    /// </summary>
    public const string SprintTrend = "/home/sprint-trend";

    /// <summary>
    /// Sprint Delivery route — canonical Delivery workspace entry for sprint inspection.
    /// </summary>
    public const string SprintDelivery = "/home/delivery/sprint";

    /// <summary>
    /// Portfolio Delivery route — aggregated delivery view across products (placeholder).
    /// </summary>
    public const string PortfolioDelivery = "/home/delivery/portfolio";

    /// <summary>
    /// Sprint Trend activity detail route.
    /// </summary>
    public const string SprintTrendActivity = "/home/sprint-trend/activity";

    /// <summary>
    /// Sprint Delivery activity detail route.
    /// </summary>
    public const string SprintDeliveryActivity = "/home/delivery/sprint/activity";

    /// <summary>
    /// Validation Triage route — grouped view of validation issues per category (SI/RR/RC/EFF).
    /// Primary Health destination for validation work.
    /// </summary>
    public const string ValidationTriage = "/home/validation-triage";

    /// <summary>
    /// Validation Queue route — fix-card list for a selected validation category.
    /// </summary>
    public const string ValidationQueue = "/home/validation-queue";

    /// <summary>
    /// Validation Fix Session route — guided per-item fix flow.
    /// </summary>
    public const string ValidationFix = "/home/validation-fix";

    /// <summary>
    /// Backlog Overview route — product-scoped refinement maturity view (Backlog State Model).
    /// </summary>
    public const string BacklogOverview = "/home/backlog-overview";

    /// <summary>
    /// Portfolio Progress Trend route — strategic, product-level progress insight over a sprint range.
    /// </summary>
    public const string PortfolioProgress = "/home/portfolio-progress";

    /// <summary>
    /// Delivery Trends route — temporal delivery patterns across multiple sprints (Trends workspace).
    /// </summary>
    public const string DeliveryTrends = "/home/trends/delivery";
    
    // Legacy Landing route constant for backward compatibility - alias to the new Legacy constant
    public const string Landing = Legacy;

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
            _ => Legacy
        };
    }
}
