using System.Globalization;

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
    /// Communication Workspace route.
    /// </summary>
    public const string CommunicationWorkspace = "/workspace/communication";

    #region Workspace Navigation Routes

    /// <summary>
    /// Health workspace route.
    /// </summary>
    public const string HealthWorkspace = "/home/health";

    /// <summary>
    /// Health overview route — canonical page for Build Quality overview content.
    /// </summary>
    public const string HealthOverview = "/home/health/overview";

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
    public const string PlanningWorkspace = "/home/planning";

    /// <summary>
    /// Home change overview route — pull request, pipeline, and work item changes since the last sync.
    /// </summary>
    public const string HomeChanges = "/home/changes";

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
    /// PR Delivery Insights route — PR classification by Feature/Epic hierarchy (Trends workspace).
    /// </summary>
    public const string PrDeliveryInsights = "/home/pr-delivery-insights";

    /// <summary>
    /// Pipeline Insights route — PO-first stability overview per product, sprint-scoped (read-only).
    /// </summary>
    public const string PipelineInsights = "/home/pipeline-insights";

    /// <summary>
    /// Dependency Overview route (read-only).
    /// </summary>
    public const string DependencyOverview = "/home/dependencies";

    /// <summary>
    /// Project-scoped planning overview route.
    /// </summary>
    public const string ProjectPlanningOverview = "/planning/{0}/overview";

    /// <summary>
    /// Product Roadmaps overview route — read-only roadmap view with horizontal product lanes.
    /// </summary>
    public const string ProductRoadmaps = "/planning/product-roadmaps";

    /// <summary>
    /// Project-scoped Product Roadmaps overview route.
    /// </summary>
    public const string ProjectProductRoadmaps = "/planning/{0}/product-roadmaps";

    /// <summary>
    /// Product Roadmap Editor route base — per-product roadmap editor with add/remove/reorder/edit capabilities.
    /// Append "/{productId}" to navigate to a specific product's editor.
    /// </summary>
    public const string ProductRoadmapEditor = "/planning/product-roadmaps/{0}";

    /// <summary>
    /// Plan Board route — operational sprint planning board for organizing PBIs and bugs into upcoming sprints.
    /// </summary>
    public const string PlanBoard = "/planning/plan-board";

    /// <summary>
    /// Project-scoped Plan Board route.
    /// </summary>
    public const string ProjectPlanBoard = "/planning/{0}/plan-board";

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
    /// Sprint Execution route — internal sprint diagnostics for POs, scrum masters, and engineering teams.
    /// </summary>
    public const string SprintExecution = "/home/delivery/execution";

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
    /// Backlog Health route — canonical Health subpage for product-scoped refinement maturity.
    /// </summary>
    public const string BacklogOverview = "/home/health/backlog-health";

    /// <summary>
    /// Legacy Backlog Health route kept for direct links and existing bookmarks.
    /// </summary>
    public const string BacklogOverviewLegacy = "/home/backlog-overview";

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

    /// <summary>
    /// Gets the per-product roadmap editor route for a specific product.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <returns>The product roadmap editor route.</returns>
    public static string GetProductRoadmapEditor(int productId)
    {
        return string.Format(CultureInfo.InvariantCulture, ProductRoadmapEditor, productId);
    }

    /// <summary>
    /// Gets the project-scoped product roadmaps route.
    /// </summary>
    public static string GetProjectProductRoadmaps(string projectAlias)
    {
        if (string.IsNullOrWhiteSpace(projectAlias))
        {
            return ProductRoadmaps;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            ProjectProductRoadmaps,
            Uri.EscapeDataString(projectAlias));
    }

    /// <summary>
    /// Gets the project-scoped planning overview route.
    /// </summary>
    public static string GetProjectPlanningOverview(string projectAlias)
    {
        if (string.IsNullOrWhiteSpace(projectAlias))
        {
            return PlanningWorkspace;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            ProjectPlanningOverview,
            Uri.EscapeDataString(projectAlias));
    }

    /// <summary>
    /// Gets the project-scoped plan board route.
    /// </summary>
    public static string GetProjectPlanBoard(string projectAlias)
    {
        if (string.IsNullOrWhiteSpace(projectAlias))
        {
            return PlanBoard;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            ProjectPlanBoard,
            Uri.EscapeDataString(projectAlias));
    }
}
