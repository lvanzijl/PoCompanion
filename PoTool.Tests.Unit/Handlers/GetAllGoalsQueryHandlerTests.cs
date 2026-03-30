using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAllGoalsQueryHandlerTests
{
    private Mock<IWorkItemQuery> _workItemQuery = null!;
    private ProfileFilterService _profileFilterService = null!;
    private Mock<ILogger<GetAllGoalsQueryHandler>> _logger = null!;
    private GetAllGoalsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _workItemQuery = new Mock<IWorkItemQuery>();
        _logger = new Mock<ILogger<GetAllGoalsQueryHandler>>();

        var settingsRepository = new Mock<ISettingsRepository>();
        var profileRepository = new Mock<IProfileRepository>();
        var profileLogger = new Mock<ILogger<ProfileFilterService>>();
        _profileFilterService = new ProfileFilterService(
            settingsRepository.Object,
            profileRepository.Object,
            profileLogger.Object);

        _handler = new GetAllGoalsQueryHandler(
            _workItemQuery.Object,
            _profileFilterService,
            _logger.Object);
    }

    [TestMethod]
    public async Task Handle_UsesGoalListingSource()
    {
        _workItemQuery
            .Setup(query => query.GetGoalsForListingAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateWorkItem(100, WorkItemType.Goal),
                CreateWorkItem(102, WorkItemType.Goal)
            ]);

        var result = (await _handler.Handle(new GetAllGoalsQuery(), CancellationToken.None)).ToList();

        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(item => item.Type == WorkItemType.Goal));
    }

    private static WorkItemDto CreateWorkItem(int tfsId, string type) =>
        new(
            TfsId: tfsId,
            Type: type,
            Title: $"Item {tfsId}",
            ParentTfsId: null,
            AreaPath: "Area",
            IterationPath: "Iteration",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
}
