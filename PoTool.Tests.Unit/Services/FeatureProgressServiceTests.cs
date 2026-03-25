using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class FeatureProgressServiceTests
{
    private static readonly IFeatureProgressService Service = new FeatureProgressService();

    [TestMethod]
    public void Compute_UsesCalculatedProgress_WhenOverrideIsMissing()
    {
        var result = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 2,
            CompletedPbiCount: 1,
            TotalStoryPoints: 10,
            CompletedStoryPoints: 4,
            Override: null));

        Assert.AreEqual(40d, result.CalculatedProgress!.Value, 0.001d);
        Assert.IsNull(result.Override);
        Assert.AreEqual(40d, result.EffectiveProgress!.Value, 0.001d);
        Assert.IsEmpty(result.ValidationSignals);
    }

    [TestMethod]
    public void Compute_AppliesOverrideAndClampRules()
    {
        var overrideResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 2,
            CompletedPbiCount: 1,
            TotalStoryPoints: 10,
            CompletedStoryPoints: 4,
            Override: 70));
        var highClampResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 2,
            CompletedPbiCount: 1,
            TotalStoryPoints: 10,
            CompletedStoryPoints: 4,
            Override: 150));
        var lowClampResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 2,
            CompletedPbiCount: 1,
            TotalStoryPoints: 10,
            CompletedStoryPoints: 4,
            Override: -10));

        Assert.AreEqual(70d, overrideResult.EffectiveProgress!.Value, 0.001d);
        Assert.AreEqual(100d, highClampResult.EffectiveProgress!.Value, 0.001d);
        CollectionAssert.Contains(highClampResult.ValidationSignals.ToList(), FeatureProgressValidationSignals.OverrideOutOfRange);
        Assert.AreEqual(0d, lowClampResult.EffectiveProgress!.Value, 0.001d);
        CollectionAssert.Contains(lowClampResult.ValidationSignals.ToList(), FeatureProgressValidationSignals.OverrideOutOfRange);
    }

    [TestMethod]
    public void Compute_EmitsWrongScaleWarningWithoutChangingRawOverrideValue()
    {
        var result = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 2,
            CompletedPbiCount: 1,
            TotalStoryPoints: 10,
            CompletedStoryPoints: 4,
            Override: 0.5));

        Assert.AreEqual(40d, result.CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(0.5d, result.Override!.Value, 0.001d);
        Assert.AreEqual(0.5d, result.EffectiveProgress!.Value, 0.001d);
        CollectionAssert.Contains(result.ValidationSignals.ToList(), FeatureProgressValidationSignals.OverrideLikelyWrongScale);
    }

    [TestMethod]
    public void Compute_ReturnsNullCalculatedProgress_WhenNoPbisOrNoStoryPointsExist()
    {
        var noPbisResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 0,
            CompletedPbiCount: 0,
            TotalStoryPoints: 0,
            CompletedStoryPoints: 0,
            Override: null));
        var noStoryPointsResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 2,
            CompletedPbiCount: 1,
            TotalStoryPoints: 0,
            CompletedStoryPoints: 0,
            Override: null));
        var countModeResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.Count,
            TotalPbiCount: 5,
            CompletedPbiCount: 2,
            TotalStoryPoints: 0,
            CompletedStoryPoints: 0,
            Override: null));
        var overrideOnlyResult = Service.Compute(new FeatureProgressCalculationRequest(
            FeatureProgressMode.StoryPoints,
            TotalPbiCount: 0,
            CompletedPbiCount: 0,
            TotalStoryPoints: 0,
            CompletedStoryPoints: 0,
            Override: 35));

        Assert.IsNull(noPbisResult.CalculatedProgress);
        Assert.IsNull(noPbisResult.EffectiveProgress);
        Assert.IsNull(noStoryPointsResult.CalculatedProgress);
        Assert.IsNull(noStoryPointsResult.EffectiveProgress);
        Assert.AreEqual(40d, countModeResult.CalculatedProgress!.Value, 0.001d);
        Assert.AreEqual(35d, overrideOnlyResult.EffectiveProgress!.Value, 0.001d);
    }
}
