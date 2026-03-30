namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class UiSemanticCorrectionDocumentTests
{
    [TestMethod]
    public void UiSemanticRules_DocumentExistsWithRequiredRules()
    {
        var repositoryRoot = GetRepositoryRoot();
        var mirrorPath = Path.Combine(repositoryRoot, "docs", "rules", "ui-semantic-rules.md");
        var authoritativePath = Path.Combine(repositoryRoot, ".github", "copilot-instructions.md");

        Assert.IsTrue(File.Exists(mirrorPath), "The UI semantic rules mirror should exist under docs/rules.");
        Assert.IsTrue(File.Exists(authoritativePath), "The authoritative copilot instructions should exist under .github.");

        var mirror = File.ReadAllText(mirrorPath);
        var authoritative = File.ReadAllText(authoritativePath);

        Assert.AreEqual("# \n", mirror, "The docs/rules mirror should contain the exact Batch 2 template.");
        StringAssert.Contains(authoritative, "### 15.3 UI semantics");
        StringAssert.Contains(authoritative, "Story points represent planning and delivery scope.");
        StringAssert.Contains(authoritative, "Effort hours represent engineering workload.");
        StringAssert.Contains(authoritative, "Never label effort hours as points.");
        StringAssert.Contains(authoritative, "Portfolio-flow and related analytics surfaces must use canonical story-point semantics in both computation and labels.");
    }

    [TestMethod]
    public void UiSemanticCorrectionAudit_DocumentExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentPath = Path.Combine(repositoryRoot, "docs", "analysis", "ui_semantic_correction.md");

        Assert.IsTrue(File.Exists(documentPath), "The UI semantic correction audit should exist under docs/analysis.");

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
