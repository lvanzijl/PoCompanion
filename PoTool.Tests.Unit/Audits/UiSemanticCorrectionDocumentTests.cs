namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class UiSemanticCorrectionDocumentTests
{
    [TestMethod]
    public void UiSemanticRules_DocumentExistsWithRequiredRules()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "domain", "ui_semantic_rules.md");

        Assert.IsTrue(File.Exists(documentPath), "The UI semantic rules document should exist under docs/domain.");

        var document = File.ReadAllText(documentPath);

        StringAssert.Contains(document, "# UI Semantic Rules");
        StringAssert.Contains(document, "Story points are the planning and delivery metric.");
        StringAssert.Contains(document, "Effort hours are the engineering workload metric.");
        StringAssert.Contains(document, "Never display effort hours with a `pts` suffix.");
        StringAssert.Contains(document, "Portfolio flow uses canonical story-point scope.");
        StringAssert.Contains(document, "Portfolio flow UI must label stock, inflow, throughput, remaining scope, and completion with story-point semantics.");
    }

    [TestMethod]
    public void UiSemanticCorrectionAudit_DocumentExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "audits", "ui_semantic_correction.md");

        Assert.IsTrue(File.Exists(documentPath), "The UI semantic correction audit should exist under docs/audits.");

        var document = File.ReadAllText(documentPath);

        StringAssert.Contains(document, "# UI Semantic Correction Audit");
        StringAssert.Contains(document, "## Labels corrected");
        StringAssert.Contains(document, "## SP surfaces migrated");
        StringAssert.Contains(document, "## Effort-hour surfaces clarified");
        StringAssert.Contains(document, "## Remaining semantic debt");
        StringAssert.Contains(document, "ForecastPanel.razor");
        StringAssert.Contains(document, "SprintExecution.razor");
        StringAssert.Contains(document, "PortfolioDelivery.razor");
        StringAssert.Contains(document, "PortfolioProgressPage.razor");
        StringAssert.Contains(document, "canonical story-point PortfolioFlow metrics");
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
