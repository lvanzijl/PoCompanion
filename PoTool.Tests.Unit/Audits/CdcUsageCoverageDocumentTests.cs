namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CdcUsageCoverageDocumentTests
{
    [TestMethod]
    public void CdcUsageCoverage_ReportExistsWithRequiredSectionsAndFindings()
    {
        var repositoryRoot = GetRepositoryRoot();
        var reportPath = Path.Combine(repositoryRoot, "docs", "analysis", "cdc-usage-coverage.md");

        Assert.IsTrue(File.Exists(reportPath), "The CDC usage coverage audit report should exist under docs/analysis.");

        var report = File.ReadAllText(reportPath);

        StringAssert.Contains(report, "# CDC Usage Coverage Audit");
        StringAssert.Contains(report, "## Handler Inventory");
        StringAssert.Contains(report, "## CDC-Compliant Handlers");
        StringAssert.Contains(report, "## CDC Bypass Findings");
        StringAssert.Contains(report, "## Compatibility Paths");
        StringAssert.Contains(report, "## Migration Opportunities");
        StringAssert.Contains(report, "GetBacklogHealthQueryHandler");
        StringAssert.Contains(report, "GetMultiIterationBacklogHealthQueryHandler");
        StringAssert.Contains(report, "GetSprintMetricsQueryHandler");
        StringAssert.Contains(report, "GetSprintExecutionQueryHandler");
        StringAssert.Contains(report, "GetSprintTrendMetricsQueryHandler");
        StringAssert.Contains(report, "GetEpicCompletionForecastQueryHandler");
        StringAssert.Contains(report, "GetCapacityCalibrationQueryHandler");
        StringAssert.Contains(report, "GetPortfolioProgressTrendQueryHandler");
        StringAssert.Contains(report, "GetPortfolioDeliveryQueryHandler");
        StringAssert.Contains(report, "GetEffortImbalanceQueryHandler");
        StringAssert.Contains(report, "GetEffortConcentrationRiskQueryHandler");
        StringAssert.Contains(report, "GetEffortDistributionTrendQueryHandler");
        StringAssert.Contains(report, "GetEffortDistributionQueryHandler");
        StringAssert.Contains(report, "GetEffortEstimationQualityQueryHandler");
        StringAssert.Contains(report, "GetEffortEstimationSuggestionsQueryHandler");
        StringAssert.Contains(report, "CDC compliant");
        StringAssert.Contains(report, "CDC bypass");
        StringAssert.Contains(report, "legacy compatibility path");
        StringAssert.Contains(report, "unavoidable adapter logic");
        StringAssert.Contains(report, "PortfolioFlowProjectionService");
        StringAssert.Contains(report, "SprintTrendProjectionService");
        StringAssert.Contains(report, "EffortDiagnosticsAnalyzer");
    }

    [TestMethod]
    public void CdcUsageCoverage_AuditClaimsMatchCurrentHandlerAnchors()
    {
        var repositoryRoot = GetRepositoryRoot();

        var backlogHealth = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetBacklogHealthQueryHandler.cs");
        var multiBacklogHealth = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetMultiIterationBacklogHealthQueryHandler.cs");
        var sprintMetrics = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetSprintMetricsQueryHandler.cs");
        var sprintExecution = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetSprintExecutionQueryHandler.cs");
        var sprintTrend = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetSprintTrendMetricsQueryHandler.cs");
        var epicForecast = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEpicCompletionForecastQueryHandler.cs");
        var capacityCalibration = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetCapacityCalibrationQueryHandler.cs");
        var portfolioProgress = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetPortfolioProgressTrendQueryHandler.cs");
        var portfolioDelivery = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetPortfolioDeliveryQueryHandler.cs");
        var effortImbalance = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortImbalanceQueryHandler.cs");
        var effortConcentration = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortConcentrationRiskQueryHandler.cs");
        var effortDistributionTrend = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortDistributionTrendQueryHandler.cs");
        var effortDistribution = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortDistributionQueryHandler.cs");
        var effortQuality = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortEstimationQualityQueryHandler.cs");
        var effortSuggestions = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Handlers/Metrics/GetEffortEstimationSuggestionsQueryHandler.cs");

        StringAssert.Contains(backlogHealth, "_backlogQualityAnalysisService.AnalyzeAsync");
        StringAssert.Contains(backlogHealth, "BacklogHealthDtoFactory.Create");
        StringAssert.Contains(multiBacklogHealth, "_backlogQualityAnalysisService.AnalyzeAsync");
        StringAssert.Contains(multiBacklogHealth, "CalculateIterationHealth");

        StringAssert.Contains(sprintMetrics, "ISprintCommitmentService");
        StringAssert.Contains(sprintMetrics, "_sprintCommitmentService.BuildCommittedWorkItemIds");
        StringAssert.Contains(sprintMetrics, "_sprintCompletionService.BuildFirstDoneByWorkItem");
        StringAssert.Contains(sprintMetrics, "ISprintFactService");
        StringAssert.Contains(sprintMetrics, "_sprintFactService.BuildSprintFactResult");

        StringAssert.Contains(sprintExecution, "ISprintExecutionMetricsCalculator");
        StringAssert.Contains(sprintExecution, "_sprintSpilloverService.BuildSpilloverWorkItemIds");
        StringAssert.Contains(sprintExecution, "ISprintFactService");
        StringAssert.Contains(sprintExecution, "_sprintFactService.BuildSprintFactResult");

        StringAssert.Contains(sprintTrend, "ComputeFeatureProgressAsync");
        StringAssert.Contains(sprintTrend, "ComputeEpicProgressAsync");
        StringAssert.Contains(epicForecast, "_completionForecastService.Forecast");
        StringAssert.Contains(epicForecast, "new GetSprintMetricsQuery(");
        StringAssert.Contains(epicForecast, "new SprintEffectiveFilter(");
        StringAssert.Contains(capacityCalibration, "_context.SprintMetricsProjections");
        StringAssert.Contains(capacityCalibration, "_velocityCalibrationService.Calibrate");

        StringAssert.Contains(portfolioProgress, "_context.PortfolioFlowProjections");
        StringAssert.Contains(portfolioProgress, "_portfolioFlowSummaryService.BuildTrend");
        StringAssert.Contains(portfolioDelivery, "_context.SprintMetricsProjections");
        StringAssert.Contains(portfolioDelivery, "_portfolioDeliverySummaryService.BuildSummary");

        StringAssert.Contains(effortImbalance, "Analyzer.AnalyzeImbalance");
        StringAssert.Contains(effortConcentration, "Analyzer.AnalyzeConcentration");
        StringAssert.Contains(effortDistributionTrend, "_effortTrendForecastService.Analyze");
        StringAssert.Contains(effortDistribution, "_effortDistributionService.Analyze");
        StringAssert.Contains(effortQuality, "_effortEstimationQualityService.Analyze");
        StringAssert.Contains(effortSuggestions, "_effortEstimationSuggestionService.GenerateSuggestion");
    }

    [TestMethod]
    public void CdcUsageCoverage_ServiceAnchorsMatchCurrentCdcBoundaries()
    {
        var repositoryRoot = GetRepositoryRoot();

        var sprintTrendProjection = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Services/SprintTrendProjectionService.cs");
        var portfolioFlowProjection = ReadRepositoryFile(repositoryRoot, "PoTool.Api/Services/PortfolioFlowProjectionService.cs");
        var completionForecastService = ReadRepositoryFile(repositoryRoot, "PoTool.Core.Domain/Domain/Forecasting/Services/CompletionForecastService.cs");
        var velocityCalibrationService = ReadRepositoryFile(repositoryRoot, "PoTool.Core.Domain/Domain/Forecasting/Services/VelocityCalibrationService.cs");
        var effortTrendForecastService = ReadRepositoryFile(repositoryRoot, "PoTool.Core.Domain/Domain/Forecasting/Services/EffortTrendForecastService.cs");
        var effortDiagnosticsAnalyzer = ReadRepositoryFile(repositoryRoot, "PoTool.Core/Metrics/EffortDiagnostics/EffortDiagnosticsAnalyzer.cs");

        StringAssert.Contains(sprintTrendProjection, "_sprintCommitmentService.BuildCommittedWorkItemIds");
        StringAssert.Contains(sprintTrendProjection, "_sprintCompletionService.BuildFirstDoneByWorkItem");
        StringAssert.Contains(sprintTrendProjection, "_sprintSpilloverService.GetNextSprintPath");
        StringAssert.Contains(portfolioFlowProjection, "_sprintCompletionService.BuildFirstDoneByWorkItem");
        StringAssert.Contains(portfolioFlowProjection, "StockStoryPoints");
        StringAssert.Contains(portfolioFlowProjection, "ThroughputStoryPoints");

        StringAssert.Contains(completionForecastService, "historicalSprints.Average");
        StringAssert.Contains(velocityCalibrationService, "PercentileMath.LinearInterpolation");
        StringAssert.Contains(effortTrendForecastService, "CalculateLinearRegressionSlope");
        StringAssert.Contains(effortDiagnosticsAnalyzer, "DomainImbalanceCanonicalRules");
        StringAssert.Contains(effortDiagnosticsAnalyzer, "DomainConcentrationCanonicalRules");
    }

    private static string ReadRepositoryFile(string repositoryRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
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
