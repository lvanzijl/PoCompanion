using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetValidationTriageSummaryQueryHandlerTests
{
    private Mock<IMediator> _mediator = null!;
    private GetValidationTriageSummaryQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mediator = new Mock<IMediator>();
        _handler = new GetValidationTriageSummaryQueryHandler(
            _mediator.Object,
            Mock.Of<ILogger<GetValidationTriageSummaryQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_SplitsRc2IntoEffCategory_WithoutPrefixInference()
    {
        var workItems = new[]
        {
            CreateWorkItem(1, new ValidationIssue("Warning", "Missing effort", "RC-2")),
            CreateWorkItem(2, new ValidationIssue("Warning", "Missing description", "RC-1")),
        };

        _mediator.Setup(m => m.Send(It.IsAny<GetAllWorkItemsWithValidationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var summary = await _handler.Handle(new GetValidationTriageSummaryQuery(null), CancellationToken.None);

        Assert.AreEqual(1, summary.MissingEffort.TotalItemCount);
        Assert.AreEqual("RC-2", summary.MissingEffort.TopRuleGroups[0].RuleId);
        Assert.AreEqual(1, summary.RefinementCompleteness.TotalItemCount);
        Assert.AreEqual("RC-1", summary.RefinementCompleteness.TopRuleGroups[0].RuleId);
    }

    private static WorkItemWithValidationDto CreateWorkItem(int id, params ValidationIssue[] issues)
    {
        return new WorkItemWithValidationDto(
            id,
            "Feature",
            $"Item {id}",
            null,
            "Area",
            "Iteration",
            "New",
            DateTimeOffset.UtcNow,
            null,
            null,
            issues.ToList());
    }
}
