using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class EffortDiagnosticsCdcExtractionAuditTests
{
    private static readonly string[] StableMathSymbols =
    {
        "DeviationFromMean(",
        "ShareOfTotal(",
        "HHI(",
        "CoefficientOfVariation("
    };

    [TestMethod]
    public void StableEffortDiagnostics_NoLegacyWrapperFileRemains()
    {
        var repositoryRoot = GetRepositoryRoot();
        var legacyWrapperPath = Path.Combine(repositoryRoot, "PoTool.Core", "Metrics", "EffortDiagnosticsStatistics.cs");
        var stableHelpersPath = Path.Combine(repositoryRoot, "PoTool.Core", "Metrics", "EffortDiagnostics", "EffortDiagnosticsStatistics.cs");

        Assert.IsFalse(File.Exists(legacyWrapperPath), "The legacy wrapper outside the CDC slice should be removed.");
        Assert.IsTrue(File.Exists(stableHelpersPath), "The stable CDC helper file should remain in PoTool.Core/Metrics/EffortDiagnostics.");
    }

    [TestMethod]
    public void StableEffortDiagnostics_HandlersStayAtOrchestrationAndDtoMappingLevel()
    {
        var repositoryRoot = GetRepositoryRoot();
        var imbalanceHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "Metrics", "GetEffortImbalanceQueryHandler.cs");
        var concentrationHandlerPath = Path.Combine(repositoryRoot, "PoTool.Api", "Handlers", "Metrics", "GetEffortConcentrationRiskQueryHandler.cs");

        var imbalanceHandler = File.ReadAllText(imbalanceHandlerPath);
        var concentrationHandler = File.ReadAllText(concentrationHandlerPath);

        StringAssert.Contains(imbalanceHandler, "Analyzer.AnalyzeImbalance(");
        StringAssert.Contains(concentrationHandler, "Analyzer.AnalyzeConcentration(");

        foreach (var symbol in StableMathSymbols)
        {
            Assert.IsFalse(imbalanceHandler.Contains(symbol, StringComparison.Ordinal), $"The imbalance handler should not contain '{symbol}'.");
            Assert.IsFalse(concentrationHandler.Contains(symbol, StringComparison.Ordinal), $"The concentration handler should not contain '{symbol}'.");
        }

        Assert.IsFalse(Regex.IsMatch(imbalanceHandler, @"\bMath\."), "The imbalance handler should not contain direct math helper calls.");
        Assert.IsFalse(Regex.IsMatch(concentrationHandler, @"\bMath\."), "The concentration handler should not contain direct math helper calls.");
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
