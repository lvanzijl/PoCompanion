namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class ValidationRulesAnalysisDocumentTests
{
    [TestMethod]
    public void ValidationRulesAnalysis_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var mirrorPath = Path.Combine(repositoryRoot, "docs", "rules", "validation-rules.md");
        var authoritativePath = Path.Combine(repositoryRoot, ".github", "copilot-instructions.md");

        Assert.IsTrue(File.Exists(mirrorPath), "The validation rules mirror should exist under docs/rules.");
        Assert.IsTrue(File.Exists(authoritativePath), "The authoritative copilot instructions should exist under .github.");

        var mirror = File.ReadAllText(mirrorPath);
        var authoritative = File.ReadAllText(authoritativePath);

        StringAssert.Contains(mirror, "No semantic interpretation is allowed.");
        StringAssert.Contains(mirror, "Historical leakage");
        StringAssert.Contains(mirror, "../../.github/copilot-instructions.md");
        StringAssert.Contains(authoritative, "## 15. Validation and semantic rules (binding)");
        StringAssert.Contains(authoritative, "The current validation/integrity model includes structural integrity, refinement readiness, and implementation readiness rules.");
        StringAssert.Contains(authoritative, "The missing-effort rule is an alias of the canonical implementation-readiness effort rule and must not diverge semantically.");
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
