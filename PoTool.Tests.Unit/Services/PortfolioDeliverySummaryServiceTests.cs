using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioDeliverySummaryServiceTests
{
    [TestMethod]
    public void BuildSummary_ComputesPortfolioTotalsAndContributionShares()
    {
        var service = new PortfolioDeliverySummaryService();

        var result = service.BuildSummary(
            new PortfolioDeliverySummaryRequest(
                [
                    new PortfolioDeliveryProductProjectionInput(1, "Product A", 2, 8, 1, 2, 1, 20),
                    new PortfolioDeliveryProductProjectionInput(1, "Product A", 1, 4, 0, 1, 0, 5),
                    new PortfolioDeliveryProductProjectionInput(2, "Product B", 3, 3, 2, 2, 1, 10)
                ],
                [
                    new PortfolioFeatureContributionInput(100, "Feature A", "Epic A", 1, "Product A", 9, 13, 70),
                    new PortfolioFeatureContributionInput(101, "Feature B", null, 2, "Product B", 3, 8, 50),
                    new PortfolioFeatureContributionInput(102, "Feature C", null, 1, "Product A", 0, 5, 0)
                ],
                TopFeatureLimit: 2));

        Assert.AreEqual(15d, result.TotalDeliveredStoryPoints, 0.001d);
        Assert.AreEqual(6, result.TotalCompletedPbis);
        Assert.AreEqual(17.5d, result.AverageProgressPercent, 0.001d);
        Assert.AreEqual(3, result.TotalBugsCreated);
        Assert.AreEqual(5, result.TotalBugsWorked);
        Assert.AreEqual(2, result.TotalBugsClosed);
        Assert.AreEqual(2, result.TotalCompletedBugs);

        Assert.HasCount(2, result.ProductSummaries);
        Assert.AreEqual(1, result.ProductSummaries[0].ProductId);
        Assert.AreEqual(12d, result.ProductSummaries[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(80d, result.ProductSummaries[0].DeliveredSharePercent, 0.001d);
        Assert.AreEqual(25d, result.ProductSummaries[0].ProgressionDelta, 0.001d);
        Assert.AreEqual(2, result.ProductSummaries[1].ProductId);
        Assert.AreEqual(3d, result.ProductSummaries[1].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(20d, result.ProductSummaries[1].DeliveredSharePercent, 0.001d);

        Assert.HasCount(2, result.FeatureContributionSummaries);
        Assert.AreEqual(100, result.FeatureContributionSummaries[0].WorkItemId);
        Assert.AreEqual(9d, result.FeatureContributionSummaries[0].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(60d, result.FeatureContributionSummaries[0].DeliveredSharePercent, 0.001d);
        Assert.AreEqual(101, result.FeatureContributionSummaries[1].WorkItemId);
        Assert.AreEqual(3d, result.FeatureContributionSummaries[1].DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(20d, result.FeatureContributionSummaries[1].DeliveredSharePercent, 0.001d);
    }
}
