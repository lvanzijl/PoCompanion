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
        StringAssert.Contains(content, "Reconcile TFS projection");
        StringAssert.Contains(content, "ProductPlanningBoardClientService.ReconcileProjectionAsync");
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
