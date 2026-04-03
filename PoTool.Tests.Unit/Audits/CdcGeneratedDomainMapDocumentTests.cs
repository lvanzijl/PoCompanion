using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class CdcGeneratedDomainMapDocumentTests
{
    private static readonly SliceDefinition[] SliceDefinitions =
    [
        new(
            "BacklogQuality",
            ServiceDirectories:
            [
                "PoTool.Core.Domain/BacklogQuality/Services"
            ],
            RepresentativeSymbols:
            [
                "BacklogQualityAnalyzer",
                "BacklogGraph",
                "BacklogQualityAnalysisResult"
            ]),
        new(
            "SprintFacts",
            ServiceDirectories:
            [
                "PoTool.Core.Domain/Domain/Cdc/Sprints"
            ],
            RepresentativeSymbols:
            [
                "SprintCommitment",
                "SprintFactResult",
                "SprintSpillover"
            ]),
        new(
            "PortfolioFlow",
            ServiceDirectories:
            [
                "PoTool.Core.Domain/Domain/Portfolio"
            ],
            RepresentativeSymbols:
            [
                "PortfolioFlowTrendRequest",
                "PortfolioFlowTrendResult",
                "PortfolioFlowSummaryResult"
            ]),
        new(
            "DeliveryTrends",
            ServiceDirectories:
            [
                "PoTool.Core.Domain/Domain/DeliveryTrends"
            ],
            RepresentativeSymbols:
            [
                "SprintDeliveryProjection",
                "ProgressionDelta",
                "PortfolioDeliverySummaryResult"
            ]),
        new(
            "Forecasting",
            ServiceDirectories:
            [
                "PoTool.Core.Domain/Domain/Forecasting"
            ],
            RepresentativeSymbols:
            [
                "DeliveryForecast",
                "CompletionProjection",
                "VelocityCalibration"
            ]),
        new(
            "EffortPlanning",
            ServiceDirectories:
            [
                "PoTool.Core.Domain/Domain/EffortPlanning"
            ],
            RepresentativeSymbols:
            [
                "EffortPlanningWorkItem",
                "EffortDistributionResult",
                "EffortEstimationSuggestionResult"
            ])
    ];

    [TestMethod]
    public void GeneratedCdcDomainMap_ReportExistsWithRequiredSectionsMetadataAndWarnings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "cdc-domain-map-generated.md");

        Assert.IsTrue(File.Exists(reportPath), "The generated CDC domain map document should exist under docs/architecture.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Domain Map — Generated");
        StringAssert.Contains(report, "## Generation Metadata");
        StringAssert.Contains(report, "## Mermaid Diagram");
        StringAssert.Contains(report, "## Simplified Architecture Diagram");
        StringAssert.Contains(report, "## Service Dependency Map");
        StringAssert.Contains(report, "## Drift Warning Against `docs/architecture/cdc-domain-map.md`");
        StringAssert.Contains(report, "Generation date: 2026-03-17");
        StringAssert.Contains(report, "Service count:");
        StringAssert.Contains(report, "PoTool.Core.Domain/Domain/Cdc/` currently exposes only the sprint slice");
        StringAssert.Contains(report, "Warning: the generated map does not exactly match the existing hand-maintained map.");

        foreach (var slice in SliceDefinitions.Select(static definition => definition.Name))
        {
            StringAssert.Contains(report, $"### {slice}");
        }

        StringAssert.Contains(report, "SprintFacts --> DeliveryTrends");
        StringAssert.Contains(report, "SprintFacts --> Forecasting");
        StringAssert.Contains(report, "PortfolioFlow --> DeliveryTrends");
        StringAssert.Contains(report, "The generated map uses the issue label `SprintFacts`");
        StringAssert.Contains(report, "The generated map collapses `Raw Work-Item Snapshots`, `Raw Work-Item History`, and `Sprint Metadata` into a single `External System` node.");
    }

    [TestMethod]
    public void GeneratedCdcDomainMap_ServiceCountAndDetectedInterfacesMatchCurrentSource()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "architecture", "cdc-domain-map-generated.md");
        var report = File.ReadAllText(reportPath);

        var detectedInterfaces = SliceDefinitions
            .SelectMany(definition => GetPublicInterfaces(repositoryRoot, definition.ServiceDirectories))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        foreach (var interfaceName in detectedInterfaces)
        {
            StringAssert.Contains(report, $"`{interfaceName}`");
        }

        foreach (var symbol in SliceDefinitions.SelectMany(static definition => definition.RepresentativeSymbols))
        {
            StringAssert.Contains(report, $"`{symbol}`");
        }

        Assert.AreEqual(
            detectedInterfaces.Count,
            ExtractServiceCount(report),
            "The generated document service count should match the current detected public service interfaces.");
    }

    private static IReadOnlyList<string> GetPublicInterfaces(string repositoryRoot, IReadOnlyList<string> relativeDirectories)
    {
        var interfaceNames = new List<string>();

        foreach (var relativeDirectory in relativeDirectories)
        {
            var fullDirectory = Path.Combine(repositoryRoot, relativeDirectory);
            if (!Directory.Exists(fullDirectory))
            {
                continue;
            }

            var filePaths = Directory
                .EnumerateFiles(fullDirectory, "*.cs", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal);

            foreach (var filePath in filePaths)
            {
                var content = File.ReadAllText(filePath);
                var matches = Regex.Matches(
                    content,
                    @"\bpublic\s+interface\s+([A-Za-z_][A-Za-z0-9_]*(?:<[^>\r\n]+>)?)",
                    RegexOptions.CultureInvariant);

                interfaceNames.AddRange(matches.Select(match => match.Groups[1].Value));
            }
        }

        return interfaceNames;
    }

    private static int ExtractServiceCount(string report)
    {
        var match = Regex.Match(
            report,
            @"Service count:\s*(\d+)\s+public service interfaces",
            RegexOptions.CultureInvariant);

        Assert.IsTrue(match.Success, "The generated document should declare a service count in the metadata section.");
        return int.Parse(match.Groups[1].Value);
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

    private sealed record SliceDefinition(
        string Name,
        IReadOnlyList<string> ServiceDirectories,
        IReadOnlyList<string> RepresentativeSymbols);
}
