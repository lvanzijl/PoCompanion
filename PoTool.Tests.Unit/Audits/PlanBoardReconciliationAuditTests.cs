namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class PlanBoardReconciliationAuditTests
{
    [TestMethod]
    public void PlanBoard_ReconcileAction_IsExplicitAndConditional()
    {
        var repositoryRoot = GetRepositoryRoot();
        var pagePath = Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "PlanBoard.razor");
        var content = File.ReadAllText(pagePath);

        StringAssert.Contains(content, "@if (epic.CanReconcileProjection)");
        StringAssert.Contains(content, "Reporting maintenance");
        StringAssert.Contains(content, "Update reported dates in TFS");
        StringAssert.Contains(content, "Update reporting data");
        StringAssert.Contains(content, "ProductPlanningBoardClientService.ReconcileProjectionAsync");
    }

    [TestMethod]
    public void PlanBoard_ActionHierarchy_PromotesDefaultMoveAndKeepsAdvancedOverrides()
    {
        var repositoryRoot = GetRepositoryRoot();
        var pagePath = Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "PlanBoard.razor");
        var content = File.ReadAllText(pagePath);

        StringAssert.Contains(content, "Default move: this Epic changes first, and following work shifts with it automatically.");
        StringAssert.Contains(content, "OnClick=\"@(_ => ExecuteAdjustSpacingAsync(epic.EpicId))\"");
        StringAssert.Contains(content, "Advanced options");
        StringAssert.Contains(content, "Move only this Epic");
        StringAssert.Contains(content, "Move everything after this");
        StringAssert.Contains(content, "Change priority order");
        StringAssert.Contains(content, "Your plan");
        StringAssert.Contains(content, "Calculated schedule");
        StringAssert.Contains(content, "Quick move");
        StringAssert.Contains(content, "Exact move");
        StringAssert.Contains(content, "Apply exact move");
    }

    [TestMethod]
    public void PlanBoard_QuickMoveControls_StayOnDefaultEndpointAndSupportRepeatFlow()
    {
        var repositoryRoot = GetRepositoryRoot();
        var pagePath = Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "PlanBoard.razor");
        var content = File.ReadAllText(pagePath);

        StringAssert.Contains(content, "ExecuteQuickSpacingAsync(epic.EpicId, -2)");
        StringAssert.Contains(content, "ExecuteQuickSpacingAsync(epic.EpicId, -1)");
        StringAssert.Contains(content, "ExecuteQuickSpacingAsync(epic.EpicId, 1)");
        StringAssert.Contains(content, "ExecuteQuickSpacingAsync(epic.EpicId, 2)");
        StringAssert.Contains(content, "RepeatLastSpacingAsync(epic.EpicId)");
        StringAssert.Contains(content, "GetRepeatLastMoveLabel(lastSpacingDelta)");
        StringAssert.Contains(content, "ProductPlanningBoardClientService.AdjustSpacingBeforeAsync");
        StringAssert.Contains(content, "_latestImpactSummary");
        StringAssert.Contains(content, "GetEpicImpactMessages(epic.EpicId)");
        StringAssert.Contains(content, "_latestImpactSummary.Title");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
