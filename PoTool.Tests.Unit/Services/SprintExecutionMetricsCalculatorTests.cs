using PoTool.Core.Domain.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class SprintExecutionMetricsCalculatorTests
{
    private readonly SprintExecutionMetricsCalculator _calculator = new();

    [TestMethod]
    public void Calculate_ComputesCanonicalSprintExecutionFormulas()
    {
        var result = _calculator.Calculate(new SprintExecutionMetricsInput(
            CommittedSP: 15d,
            AddedSP: 3d,
            RemovedSP: 2d,
            DeliveredSP: 8d,
            DeliveredFromAddedSP: 3d,
            SpilloverSP: 8d));

        Assert.AreEqual(15d, result.CommittedSP);
        Assert.AreEqual(3d, result.AddedSP);
        Assert.AreEqual(2d, result.RemovedSP);
        Assert.AreEqual(8d, result.DeliveredSP);
        Assert.AreEqual(3d, result.DeliveredFromAddedSP);
        Assert.AreEqual(8d, result.SpilloverSP);
        Assert.AreEqual(5d / 18d, result.ChurnRate, 1e-9);
        Assert.AreEqual(8d / 13d, result.CommitmentCompletion, 1e-9);
        Assert.AreEqual(8d / 13d, result.SpilloverRate, 1e-9);
        Assert.AreEqual(1d, result.AddedDeliveryRate, 1e-9);
    }

    [TestMethod]
    public void Calculate_ReturnsZeroRates_WhenAllDenominatorsAreZero()
    {
        var result = _calculator.Calculate(new SprintExecutionMetricsInput(
            CommittedSP: 0d,
            AddedSP: 0d,
            RemovedSP: 0d,
            DeliveredSP: 0d,
            DeliveredFromAddedSP: 0d,
            SpilloverSP: 0d));

        Assert.AreEqual(0d, result.ChurnRate);
        Assert.AreEqual(0d, result.CommitmentCompletion);
        Assert.AreEqual(0d, result.SpilloverRate);
        Assert.AreEqual(0d, result.AddedDeliveryRate);
    }

    [TestMethod]
    public void Calculate_UsesRemovedScopeToReduceCompletionAndSpilloverDenominators()
    {
        var result = _calculator.Calculate(new SprintExecutionMetricsInput(
            CommittedSP: 13d,
            AddedSP: 0d,
            RemovedSP: 5d,
            DeliveredSP: 4d,
            DeliveredFromAddedSP: 0d,
            SpilloverSP: 2d));

        Assert.AreEqual(4d / 8d, result.CommitmentCompletion, 1e-9);
        Assert.AreEqual(2d / 8d, result.SpilloverRate, 1e-9);
    }

    [TestMethod]
    public void Calculate_UsesAddedScopeForChurnAndAddedDeliveryRates()
    {
        var result = _calculator.Calculate(new SprintExecutionMetricsInput(
            CommittedSP: 10d,
            AddedSP: 5d,
            RemovedSP: 1d,
            DeliveredSP: 7d,
            DeliveredFromAddedSP: 2d,
            SpilloverSP: 0d));

        Assert.AreEqual(6d / 15d, result.ChurnRate, 1e-9);
        Assert.AreEqual(2d / 5d, result.AddedDeliveryRate, 1e-9);
    }
}
