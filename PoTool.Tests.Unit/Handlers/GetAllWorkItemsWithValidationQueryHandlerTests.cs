using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Validators;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetAllWorkItemsWithValidationQueryHandlerTests
{
    private Mock<IWorkItemQuery> _mockQuery = null!;
    private Mock<IWorkItemValidator> _mockValidator = null!;
    private ProfileFilterService _profileFilterService = null!;
    private Mock<ILogger<GetAllWorkItemsWithValidationQueryHandler>> _mockLogger = null!;
    private GetAllWorkItemsWithValidationQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockQuery = new Mock<IWorkItemQuery>();
        _mockValidator = new Mock<IWorkItemValidator>();
        _mockLogger = new Mock<ILogger<GetAllWorkItemsWithValidationQueryHandler>>();

        var mockSettingsRepository = new Mock<ISettingsRepository>();
        var mockProfileRepository = new Mock<IProfileRepository>();
        var mockProfileFilterLogger = new Mock<ILogger<ProfileFilterService>>();
        _profileFilterService = new ProfileFilterService(
            mockSettingsRepository.Object,
            mockProfileRepository.Object,
            mockProfileFilterLogger.Object);

        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(new Dictionary<int, List<ValidationIssue>>());

        _handler = new GetAllWorkItemsWithValidationQueryHandler(
            _mockQuery.Object,
            _mockValidator.Object,
            _profileFilterService,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoProductIds_LoadsListingSource()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(200, "Epic", "Product B Root"),
            CreateWorkItem(300, "Epic", "Product C Root")
        };

        _mockQuery.Setup(p => p.GetWorkItemsForListingAsync(
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _handler.Handle(new GetAllWorkItemsWithValidationQuery(null), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.HasCount(3, result);
        _mockQuery.Verify(p => p.GetWorkItemsForListingAsync(null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithSpecificProductIds_PassesProductIdsToListingSource()
    {
        var workItemsForProductA = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(101, "Feature", "Feature 1")
        };

        _mockQuery.Setup(p => p.GetWorkItemsForListingAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItemsForProductA);

        var result = await _handler.Handle(new GetAllWorkItemsWithValidationQuery(new[] { 1 }), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
        _mockQuery.Verify(p => p.GetWorkItemsForListingAsync(
            It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_AttachesValidationResults()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(100, "Epic", "Product A Root"),
            CreateWorkItem(101, "Feature", "Feature 1")
        };

        var validationIssues = new Dictionary<int, List<ValidationIssue>>
        {
            { 101, new List<ValidationIssue> { new ValidationIssue("Warning", "Missing effort") } }
        };

        _mockQuery.Setup(p => p.GetWorkItemsForListingAsync(
                It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockValidator.Setup(v => v.ValidateWorkItems(It.IsAny<IEnumerable<WorkItemDto>>()))
            .Returns(validationIssues);

        var result = await _handler.Handle(new GetAllWorkItemsWithValidationQuery(new[] { 1 }), CancellationToken.None);

        Assert.IsNotNull(result);
        var resultList = result.ToList();
        Assert.HasCount(2, resultList);

        var itemWithIssue = resultList.First(wi => wi.TfsId == 101);
        Assert.HasCount(1, itemWithIssue.ValidationIssues);
        Assert.AreEqual("Missing effort", itemWithIssue.ValidationIssues[0].Message);

        var itemWithoutIssue = resultList.First(wi => wi.TfsId == 100);
        Assert.IsEmpty(itemWithoutIssue.ValidationIssues);
    }

    private static WorkItemDto CreateWorkItem(int tfsId, string type, string title)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: null,
            AreaPath: "Test",
            IterationPath: "Sprint 1",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            Tags: null
        );
    }
}
