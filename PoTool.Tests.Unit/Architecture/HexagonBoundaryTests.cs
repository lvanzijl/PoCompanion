using System.Text.RegularExpressions;
using PoTool.Core.Domain.Cdc.Sprints;

namespace PoTool.Tests.Unit.Architecture;

[TestClass]
public sealed class HexagonBoundaryTests
{
    private static readonly string[] ForbiddenCoreAssemblyReferences =
    {
        "PoTool.Api",
        "PoTool.Client",
        "PoTool.Infrastructure"
    };

    private static readonly string[] ForbiddenCdcNamespaceTokens =
    {
        "using PoTool.Api",
        "using PoTool.Client",
        "using PoTool.Infrastructure",
        "using PoTool.Integrations"
    };

    private static readonly HandlerRule[] HandlerRules =
    {
        new(
            "PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs",
            requiredAnchors:
            [
                "IBacklogQualityAnalysisService",
                "_backlogQualityAnalysisService.AnalyzeAsync"
            ],
            forbiddenTokens:
            [
                "IHierarchicalWorkItemValidator"
            ]),
        new(
            "PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs",
            requiredAnchors:
            [
                "IBacklogQualityAnalysisService",
                "_backlogQualityAnalysisService.AnalyzeAsync"
            ],
            forbiddenTokens:
            [
                "IHierarchicalWorkItemValidator"
            ]),
        new(
            "PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs",
            requiredAnchors:
            [
                "ISprintFactService",
                "_sprintFactService.BuildSprintFactResult"
            ],
            forbiddenTokens:
            [
                "ResolveSprintStoryPoints(",
                "SumStoryPoints(",
                "SumDeliveredStoryPoints("
            ]),
        new(
            "PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs",
            requiredAnchors:
            [
                "ISprintFactService",
                "_sprintFactService.BuildSprintFactResult"
            ],
            forbiddenTokens:
            [
                "SumStoryPoints(",
                "SumDeliveredStoryPoints(",
                "CalculateCommitmentCompletion(",
                "CalculateChurnRate(",
                "CalculateSpilloverRate(",
                "CalculateAddedDeliveryRate("
            ]),
        new(
            "PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs",
            requiredAnchors:
            [
                "IPortfolioFlowSummaryService",
                "_portfolioFlowSummaryService.BuildTrend"
            ],
            forbiddenTokens:
            [
                "ComputeSummary("
            ],
            forbiddenPatterns:
            [
                @"\bCompletionPercent\s*=\s*[^;\r\n]*(?:\+|-|\*|/)",
                @"\bNetFlowStoryPoints\s*=\s*[^;\r\n]*(?:\+|-|\*|/)",
                @"\bCumulativeNetFlow\s*=\s*[^;\r\n]*(?:\+|-|\*|/)"
            ]),
        new(
            "PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs",
            requiredAnchors:
            [
                "IPortfolioDeliverySummaryService",
                "_portfolioDeliverySummaryService.BuildSummary"
            ],
            forbiddenPatterns:
            [
                @"\bAverageProgressPercent\s*=\s*[^;\r\n]*(?:\+|-|\*|/)",
                @"\bEffortShare\s*=\s*[^;\r\n]*(?:\+|-|\*|/)"
            ])
    };

    [TestMethod]
    public void HexagonBoundary_AuditDocumentExistsWithRequiredSectionsAndAnchors()
    {
        var repositoryRoot = GetRepositoryRoot();
        var auditPath = Path.Combine(repositoryRoot, "docs", "audits", "hexagon_boundary_enforcement.md");

        Assert.IsTrue(File.Exists(auditPath), "The hexagon boundary enforcement audit should exist under docs/audits.");

        var audit = File.ReadAllText(auditPath);
        StringAssert.Contains(audit, "# Hexagon Boundary Enforcement");
        StringAssert.Contains(audit, "## Architecture rules");
        StringAssert.Contains(audit, "## Allowed dependencies");
        StringAssert.Contains(audit, "## Forbidden dependencies");
        StringAssert.Contains(audit, "## Enforcement tests");
        StringAssert.Contains(audit, "docs/domain/cdc_reference.md");
        StringAssert.Contains(audit, "docs/domain/cdc_domain_map.md");
        StringAssert.Contains(audit, "docs/audits/application_handler_cleanup.md");
        StringAssert.Contains(audit, "docs/audits/application_simplification_audit.md");
        StringAssert.Contains(audit, "ISprintFactService");
        StringAssert.Contains(audit, "IPortfolioFlowSummaryService");
        StringAssert.Contains(audit, "IBacklogQualityAnalysisService");
        StringAssert.Contains(audit, "SumStoryPoints");
        StringAssert.Contains(audit, "CompletionPercent");
        StringAssert.Contains(audit, "NetFlowStoryPoints");
        StringAssert.Contains(audit, "PoTool.Tests.Unit/Architecture/HexagonBoundaryTests.cs");
    }

    [TestMethod]
    public void HexagonBoundary_CoreDomainAssemblyDoesNotReferenceOuterLayers()
    {
        var referencedAssemblyNames = typeof(ISprintFactService).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var forbiddenReference in ForbiddenCoreAssemblyReferences)
        {
            Assert.DoesNotContain(
                forbiddenReference,
                referencedAssemblyNames,
                $"PoTool.Core.Domain must not reference {forbiddenReference}. CDC/domain must remain the inward semantic owner.");
        }
    }

    [TestMethod]
    public void HexagonBoundary_CdcSourceFilesDoNotUseForbiddenOuterLayerNamespaces()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = new List<string>();

        foreach (var filePath in EnumerateSourceFiles(Path.Combine(repositoryRoot, "PoTool.Core.Domain")))
        {
            var relativePath = NormalizeRelativePath(repositoryRoot, filePath);
            var content = File.ReadAllText(filePath);

            foreach (var forbiddenNamespaceToken in ForbiddenCdcNamespaceTokens)
            {
                if (content.Contains(forbiddenNamespaceToken, StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath} -> {forbiddenNamespaceToken}");
                }
            }
        }

        Assert.IsFalse(
            violations.Any(),
            "CDC/domain source files must not depend on API, client, or infrastructure namespaces. Violations: "
            + string.Join("; ", violations));
    }

    [TestMethod]
    public void HexagonBoundary_CdcBackedHandlersReferenceCanonicalServices()
    {
        var repositoryRoot = GetRepositoryRoot();

        foreach (var rule in HandlerRules)
        {
            var handler = ReadRepositoryFile(repositoryRoot, rule.RelativePath);

            foreach (var requiredAnchor in rule.RequiredAnchors)
            {
                StringAssert.Contains(
                    handler,
                    requiredAnchor,
                    $"{rule.RelativePath} should stay on the CDC boundary by delegating through {requiredAnchor}.");
            }
        }
    }

    [TestMethod]
    public void HexagonBoundary_CdcBackedHandlersDoNotIntroduceDomainMath()
    {
        var repositoryRoot = GetRepositoryRoot();
        var violations = new List<string>();

        foreach (var rule in HandlerRules)
        {
            var handler = ReadRepositoryFile(repositoryRoot, rule.RelativePath);

            foreach (var forbiddenToken in rule.ForbiddenTokens)
            {
                if (handler.Contains(forbiddenToken, StringComparison.Ordinal))
                {
                    violations.Add($"{rule.RelativePath} contains forbidden token '{forbiddenToken}'");
                }
            }

            foreach (var forbiddenPattern in rule.ForbiddenPatterns)
            {
                if (Regex.IsMatch(handler, forbiddenPattern, RegexOptions.CultureInvariant))
                {
                    violations.Add($"{rule.RelativePath} matches forbidden pattern '{forbiddenPattern}'");
                }
            }
        }

        Assert.IsFalse(
            violations.Any(),
            "Handlers must not own story point rollups, completion percent calculations, or flow calculations. "
            + "Move the offending logic into CDC ownership (for example ISprintFactService, "
            + "IBacklogQualityAnalysisService, IPortfolioFlowSummaryService, or IPortfolioDeliverySummaryService). "
            + "Violations: "
            + string.Join("; ", violations));
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
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

    private static string ReadRepositoryFile(string repositoryRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
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

    private sealed class HandlerRule
    {
        public HandlerRule(
            string relativePath,
            IReadOnlyList<string> requiredAnchors,
            IReadOnlyList<string>? forbiddenTokens = null,
            IReadOnlyList<string>? forbiddenPatterns = null)
        {
            RelativePath = relativePath;
            RequiredAnchors = requiredAnchors;
            ForbiddenTokens = forbiddenTokens ?? Array.Empty<string>();
            ForbiddenPatterns = forbiddenPatterns ?? Array.Empty<string>();
        }

        public string RelativePath { get; }

        public IReadOnlyList<string> RequiredAnchors { get; }

        public IReadOnlyList<string> ForbiddenTokens { get; }

        public IReadOnlyList<string> ForbiddenPatterns { get; }
    }
}
