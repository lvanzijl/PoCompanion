namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class SnapshotComparisonValidationDocumentTests
{
    [TestMethod]
    public void SnapshotComparisonValidation_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "snapshot-comparison-validation.md");

        Assert.IsTrue(File.Exists(reportPath), "The snapshot comparison validation report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Snapshot Comparison Validation");
        StringAssert.Contains(report, "## Implementation summary");
        StringAssert.Contains(report, "## Single source confirmation");
        StringAssert.Contains(report, "## Verification results");
        StringAssert.Contains(report, "## Edge cases observed");
        StringAssert.Contains(report, "ISnapshotComparisonService");
        StringAssert.Contains(report, "SnapshotComparisonService");
        StringAssert.Contains(report, "SnapshotComparisonResult");
        StringAssert.Contains(report, "ProductSnapshot");
        StringAssert.Contains(report, "SnapshotComparisonServiceTests");
        StringAssert.Contains(report, "ServiceCollectionTests");
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
