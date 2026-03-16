using Moq;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Services;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class BacklogQualityAnalysisServiceTests
{
    [TestMethod]
    public async Task AnalyzeAsync_UsesConfiguredStateClassificationsForCanonicalFindings()
    {
        var stateClassificationService = new Mock<IWorkItemStateClassificationService>();
        stateClassificationService.Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetStateClassificationsResponse
            {
                ProjectName = "Demo",
                IsDefault = false,
                Classifications =
                [
                    new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Done", Classification = StateClassification.Done },
                    new WorkItemStateClassificationDto { WorkItemType = "Task", StateName = "Active", Classification = StateClassification.InProgress }
                ]
            });

        var service = new BacklogQualityAnalysisService(stateClassificationService.Object, new BacklogQualityAnalyzer());
        var workItems = new[]
        {
            new WorkItemDto(1, "Epic", "Epic 1", null, "Area", "Sprint 1", "Done", DateTimeOffset.UtcNow, null, "Valid epic description", null),
            new WorkItemDto(2, "Task", "Task 2", 1, "Area", "Sprint 1", "Active", DateTimeOffset.UtcNow, null, "Valid task description", null)
        };

        var result = await service.AnalyzeAsync(workItems, CancellationToken.None);

        Assert.HasCount(1, result.IntegrityFindings);
        Assert.AreEqual("SI-1", result.IntegrityFindings[0].Rule.RuleId);
    }
}
