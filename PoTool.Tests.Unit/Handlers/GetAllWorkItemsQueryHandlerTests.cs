using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAllWorkItemsQueryHandlerTests
{
    private Mock<IWorkItemQuery> _workItemQuery = null!;
    private ProfileFilterService _profileFilterService = null!;
    private Mock<ILogger<GetAllWorkItemsQueryHandler>> _logger = null!;
    private GetAllWorkItemsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _workItemQuery = new Mock<IWorkItemQuery>();
        _logger = new Mock<ILogger<GetAllWorkItemsQueryHandler>>();

        var settingsRepository = new Mock<ISettingsRepository>();
        var profileRepository = new Mock<IProfileRepository>();
        var profileLogger = new Mock<ILogger<ProfileFilterService>>();
        _profileFilterService = new ProfileFilterService(
            settingsRepository.Object,
            profileRepository.Object,
            profileLogger.Object);

        _handler = new GetAllWorkItemsQueryHandler(
            _workItemQuery.Object,
            _profileFilterService,
            _logger.Object);
    }

    [TestMethod]
    public async Task Handle_LoadsListingFromIntentDrivenQueryBoundary()
    {
        var expected = new List<WorkItemDto>
        {
            CreateWorkItem(101, "Epic"),
            CreateWorkItem(202, "Epic")
        };

        _workItemQuery
            .Setup(query => query.GetWorkItemsForListingAsync(
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = (await _handler.Handle(new GetAllWorkItemsQuery(), CancellationToken.None)).ToList();

        Assert.HasCount(2, result);
        _workItemQuery.Verify(query => query.GetWorkItemsForListingAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
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
