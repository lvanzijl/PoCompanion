namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcInvariantTestsDocumentTests
{
    [TestMethod]
    public void CdcInvariantTests_ReportExistsWithCorrectedInvariantDefinitions()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "cdc_invariant_tests.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC invariant audit should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Invariant Tests");
        StringAssert.Contains(report, "## Corrected Invariants");
        StringAssert.Contains(report, "Incorrect: `CommittedSP = DeliveredSP + RemainingSP + SpilloverSP`");
        StringAssert.Contains(report, "RemainingSP = CommittedSP + AddedSP - RemovedSP - DeliveredSP");
        StringAssert.Contains(report, "CommittedSP >= DeliveredSP");
        StringAssert.Contains(report, "AddedSP >= DeliveredFromAddedSP");
        StringAssert.Contains(report, "SpilloverSP <= RemainingSP");
        StringAssert.Contains(report, "DeliveredFromAddedSP <= AddedSP");
        StringAssert.Contains(report, "Spillover is a subset of Remaining");
        StringAssert.Contains(report, "SprintFacts uses story points only");
        StringAssert.Contains(report, "EffortPlanning distribution totals equal the sum of work-item effort hours");
        StringAssert.Contains(report, "must not equate effort hours with story points");
        StringAssert.Contains(report, "docs/domain/cdc_reference.md");
        StringAssert.Contains(report, "docs/domain/cdc_domain_map.md");
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
