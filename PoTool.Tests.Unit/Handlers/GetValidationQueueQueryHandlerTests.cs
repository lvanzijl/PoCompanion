using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetValidationQueueQueryHandlerTests
{
    private Mock<IMediator> _mediator = null!;
    private GetValidationQueueQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mediator = new Mock<IMediator>();
        _handler = new GetValidationQueueQueryHandler(
            _mediator.Object,
            Mock.Of<ILogger<GetValidationQueueQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_GroupsRc2OnlyInEffAliasQueue()
    {
        var workItems = new[]
        {
            CreateWorkItem(1, new ValidationIssue("Warning", "Missing effort", "RC-2")),
            CreateWorkItem(2, new ValidationIssue("Warning", "Missing description", "RC-1")),
        };

        _mediator.Setup(m => m.Send(It.IsAny<GetAllWorkItemsWithValidationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var effQueue = await _handler.Handle(new GetValidationQueueQuery("EFF", null), CancellationToken.None);
        var rcQueue = await _handler.Handle(new GetValidationQueueQuery("RC", null), CancellationToken.None);

        Assert.AreEqual(1, effQueue.TotalItemCount);
        Assert.HasCount(1, effQueue.RuleGroups);
        Assert.AreEqual("RC-2", effQueue.RuleGroups[0].RuleId);

        Assert.AreEqual(1, rcQueue.TotalItemCount);
        Assert.HasCount(1, rcQueue.RuleGroups);
        Assert.AreEqual("RC-1", rcQueue.RuleGroups[0].RuleId);
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
