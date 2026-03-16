namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcCoverageAuditDocumentTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> AllowedProductionFilesByHelper =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["SprintCommitmentLookup"] = new[]
            {
                "PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs",
                "PoTool.Core.Domain/Domain/Sprints/SprintCommitmentLookup.cs"
            },
            ["SprintSpilloverLookup"] = new[]
            {
                "PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs",
                "PoTool.Core.Domain/Domain/Sprints/SprintSpilloverLookup.cs"
            },
            ["FirstDoneDeliveryLookup"] = new[]
            {
                "PoTool.Core.Domain/Domain/Cdc/Sprints/SprintCdcServices.cs",
                "PoTool.Core.Domain/Domain/Sprints/FirstDoneDeliveryLookup.cs"
            }
        };

    [TestMethod]
    public void CdcCoverageAudit_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "audits", "cdc_coverage_audit.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC coverage audit report should exist under docs/audits.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Coverage Audit");
        StringAssert.Contains(report, "## Phase 1 — Direct helper usage scan");
        StringAssert.Contains(report, "## Phase 2 — API layer verification");
        StringAssert.Contains(report, "## Phase 3 — Projection verification");
        StringAssert.Contains(report, "## Phase 4 — Coverage conclusion");
        StringAssert.Contains(report, "SprintCommitmentLookup");
        StringAssert.Contains(report, "SprintSpilloverLookup");
        StringAssert.Contains(report, "FirstDoneDeliveryLookup");
        StringAssert.Contains(report, "ISprintCommitmentService");
        StringAssert.Contains(report, "ISprintScopeChangeService");
        StringAssert.Contains(report, "ISprintCompletionService");
        StringAssert.Contains(report, "ISprintSpilloverService");
        StringAssert.Contains(report, "SprintTrendProjectionService");
        StringAssert.Contains(report, "PortfolioFlowProjectionService");
        StringAssert.Contains(report, "SprintCdcServices.cs");
    }

    [TestMethod]
    public void CdcCoverageAudit_LegacySprintHelpersAreConfinedToAllowedProductionFiles()
    {
        var repositoryRoot = GetRepositoryRoot();
        var productionFiles = EnumerateProductionFiles(repositoryRoot).ToList();
        var violations = new List<string>();

        foreach (var filePath in productionFiles)
        {
            var relativePath = NormalizeRelativePath(repositoryRoot, filePath);
            var content = File.ReadAllText(filePath);

            foreach (var helper in AllowedProductionFilesByHelper)
            {
                if (!content.Contains(helper.Key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!helper.Value.Contains(relativePath, StringComparer.Ordinal))
                {
                    violations.Add($"{helper.Key} referenced by {relativePath}");
                }
            }
        }

        Assert.IsEmpty(
            violations,
            "Legacy sprint helpers should be confined to the CDC adapter and their own definition files. Violations: "
            + string.Join("; ", violations));
    }

    [TestMethod]
    public void CdcCoverageAudit_HandlersAndProjectionsUseCdcInterfacesInsteadOfLegacyHelpers()
    {
        var repositoryRoot = GetRepositoryRoot();
        var sprintMetricsHandler = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs");
        var sprintExecutionHandler = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs");
        var sprintTrendProjection = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        var portfolioFlowProjection = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Services/PortfolioFlowProjectionService.cs");

        StringAssert.Contains(sprintMetricsHandler, "ISprintCommitmentService");
        StringAssert.Contains(sprintMetricsHandler, "ISprintScopeChangeService");
        StringAssert.Contains(sprintMetricsHandler, "ISprintCompletionService");

        StringAssert.Contains(sprintExecutionHandler, "ISprintCommitmentService");
        StringAssert.Contains(sprintExecutionHandler, "ISprintScopeChangeService");
        StringAssert.Contains(sprintExecutionHandler, "ISprintCompletionService");
        StringAssert.Contains(sprintExecutionHandler, "ISprintSpilloverService");

        StringAssert.Contains(sprintTrendProjection, "ISprintCommitmentService");
        StringAssert.Contains(sprintTrendProjection, "ISprintCompletionService");
        StringAssert.Contains(sprintTrendProjection, "ISprintSpilloverService");
        StringAssert.Contains(sprintTrendProjection, "_sprintCommitmentService.BuildCommittedWorkItemIds");
        StringAssert.Contains(sprintTrendProjection, "_sprintCompletionService.BuildFirstDoneByWorkItem");
        StringAssert.Contains(sprintTrendProjection, "_sprintSpilloverService.GetNextSprintPath");

        StringAssert.Contains(portfolioFlowProjection, "ISprintCompletionService");
        StringAssert.Contains(portfolioFlowProjection, "_sprintCompletionService.BuildFirstDoneByWorkItem");

        AssertNoLegacyHelperReference(sprintMetricsHandler, nameof(sprintMetricsHandler));
        AssertNoLegacyHelperReference(sprintExecutionHandler, nameof(sprintExecutionHandler));
        AssertNoLegacyHelperReference(sprintTrendProjection, nameof(sprintTrendProjection));
        AssertNoLegacyHelperReference(portfolioFlowProjection, nameof(portfolioFlowProjection));
    }

    private static IEnumerable<string> EnumerateProductionFiles(string repositoryRoot)
    {
        foreach (var root in new[]
                 {
                     Path.Combine(repositoryRoot, "PoTool.Api"),
                     Path.Combine(repositoryRoot, "PoTool.Core.Domain")
                 })
        {
            foreach (var filePath in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var normalizedPath = filePath.Replace('\\', '/');
                if (normalizedPath.Contains("/bin/", StringComparison.Ordinal)
                    || normalizedPath.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return filePath;
            }
        }
    }

    private static void AssertNoLegacyHelperReference(string content, string fileLabel)
    {
        Assert.IsFalse(content.Contains("SprintCommitmentLookup", StringComparison.Ordinal), $"{fileLabel} should not reference SprintCommitmentLookup directly.");
        Assert.IsFalse(content.Contains("SprintSpilloverLookup", StringComparison.Ordinal), $"{fileLabel} should not reference SprintSpilloverLookup directly.");
        Assert.IsFalse(content.Contains("FirstDoneDeliveryLookup", StringComparison.Ordinal), $"{fileLabel} should not reference FirstDoneDeliveryLookup directly.");
    }

    private static string ReadRepositoryFile(string repositoryRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string NormalizeRelativePath(string repositoryRoot, string filePath)
    {
        return Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/');
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
