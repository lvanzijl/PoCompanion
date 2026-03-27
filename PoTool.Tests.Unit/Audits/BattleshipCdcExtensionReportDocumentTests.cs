namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class BattleshipCdcExtensionReportDocumentTests
{
    [TestMethod]
    public void BattleshipCdcExtension_ReportExistsWithRequiredSections()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "implementation", "battleship-cdc-extension-report.md");

        Assert.IsTrue(File.Exists(reportPath), "The battleship CDC extension report should exist under docs/implementation.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# Battleship CDC Extension Report");
        StringAssert.Contains(report, "## What was extended");
        StringAssert.Contains(report, "## Snapshot timeline");
        StringAssert.Contains(report, "## Covered CDC scenarios");
        StringAssert.Contains(report, "## How to run");
        StringAssert.Contains(report, "## What to verify");
        StringAssert.Contains(report, "Incident Response Control");
        StringAssert.Contains(report, "Crew Safety Operations");
        StringAssert.Contains(report, "Empty portfolio");
        StringAssert.Contains(report, "identical timestamp");
        StringAssert.Contains(report, "SnapshotId ordering");
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
