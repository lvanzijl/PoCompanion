using System.Text.RegularExpressions;

namespace PoTool.Tests.Unit.Architecture;

[TestClass]
public sealed class BuildQualityArchitectureGuardTests
{
    private const string ClientRoot = "PoTool.Client";

    private static readonly ArchitectureGuardRule NoThresholdLeakageRule = new(
        "No threshold leakage",
        "BuildQuality semantics must stay out of the Blazor client. Threshold constants and minimum evidence rules belong to the backend/shared contract, not PoTool.Client.",
        [
            new ArchitectureGuardPattern("threshold literal 0.90", @"(?<![\d.])0\.90(?![\d.])"),
            new ArchitectureGuardPattern("threshold literal 0.70", @"(?<![\d.])0\.70(?![\d.])"),
            new ArchitectureGuardPattern("minimum build evidence token", @"\bMinimumBuilds\b"),
            new ArchitectureGuardPattern("minimum test evidence token", @"\bMinimumTests\b")
        ]);

    private static readonly ArchitectureGuardRule NoAggregationRule = new(
        "No aggregation",
        "BuildQuality aggregation must not be recomputed in PoTool.Client. Failed and partially succeeded build totals must arrive precomputed from the contract.",
        [
            new ArchitectureGuardPattern("failed build aggregation token", @"FailedBuilds\s*\+"),
            new ArchitectureGuardPattern("partial success aggregation token", @"\+\s*PartiallySucceededBuilds")
        ]);

    private static readonly ArchitectureGuardRule NoStateDerivationRule = new(
        "No state derivation",
        "BuildQuality state derivation helpers must not exist in PoTool.Client. The UI must only render backend-provided semantics instead of deriving quality or visual states.",
        [
            new ArchitectureGuardPattern("overall state helper", @"\bGetOverallState\b"),
            new ArchitectureGuardPattern("confidence state helper", @"\bGetConfidenceState\b"),
            new ArchitectureGuardPattern("dimension state helper", @"\bGetDimensionState\b"),
            new ArchitectureGuardPattern("quality state token", @"\bQualityState\b"),
            new ArchitectureGuardPattern("visual state token", @"\bVisualState\b")
        ]);

    private static readonly ArchitectureGuardRule NoConfidenceLogicRule = new(
        "No confidence logic",
        "Confidence comparisons must not exist in PoTool.Client. Confidence semantics belong to backend/shared BuildQuality logic, not the presentation layer.",
        [
            new ArchitectureGuardPattern("confidence less-than comparison", @"\bConfidence[^\S\r\n]*<[^\S\r\n]*(?:[\w""'\(])"),
            new ArchitectureGuardPattern("confidence greater-than-or-equal comparison", @"\bConfidence[^\S\r\n]*>=[^\S\r\n]*(?:[\w""'\(])"),
            new ArchitectureGuardPattern("confidence equality comparison", @"\bConfidence[^\S\r\n]*==[^\S\r\n]*(?:[\w""'\(])")
        ]);

    private static readonly ArchitectureGuardRule NoUnknownInferenceRule = new(
        "No Unknown inference",
        "PoTool.Client must not infer BuildQuality Unknown labels from null values. Unknown must only come from explicit backend flags and reasons.",
        [
            new ArchitectureGuardPattern(
                "value == null paired with \"Unknown\"",
                @"value\s*==\s*null[\s\S]{0,160}""Unknown""",
                RegexOptions.Singleline),
            new ArchitectureGuardPattern(
                "!value.HasValue paired with \"Unknown\"",
                @"!value\.HasValue[\s\S]{0,160}""Unknown""",
                RegexOptions.Singleline)
        ]);

    private static readonly ArchitectureGuardRule NoChartDriftRule = new(
        "No chart drift reintroduction",
        "BuildQuality chart state carrier fields must not return to PoTool.Client. Chart visuals must render directly from the backend contract without dormant state labels or stroke colors.",
        [
            new ArchitectureGuardPattern("QualityStateLabel field", @"\bQualityStateLabel\b"),
            new ArchitectureGuardPattern("QualityStrokeColor field", @"\bQualityStrokeColor\b")
        ]);

    [TestMethod]
    public void BuildQualityArchitectureGuard_NoThresholdLeakageInClient()
    {
        AssertRule(NoThresholdLeakageRule);
    }

    [TestMethod]
    public void BuildQualityArchitectureGuard_NoAggregationInClient()
    {
        AssertRule(NoAggregationRule);
    }

    [TestMethod]
    public void BuildQualityArchitectureGuard_NoStateDerivationInClient()
    {
        AssertRule(NoStateDerivationRule);
    }

    [TestMethod]
    public void BuildQualityArchitectureGuard_NoConfidenceLogicInClient()
    {
        AssertRule(NoConfidenceLogicRule);
    }

    [TestMethod]
    public void BuildQualityArchitectureGuard_NoUnknownInferenceInClient()
    {
        AssertRule(NoUnknownInferenceRule);
    }

    [TestMethod]
    public void BuildQualityArchitectureGuard_NoChartDriftReintroductionInClient()
    {
        AssertRule(NoChartDriftRule);
    }

    private static void AssertRule(ArchitectureGuardRule rule)
    {
        var violations = ArchitectureGuardScanner.FindViolations(ClientRoot, rule);

        Assert.IsFalse(
            violations.Any(),
            ArchitectureGuardScanner.FormatFailureMessage(rule, violations));
    }
}
