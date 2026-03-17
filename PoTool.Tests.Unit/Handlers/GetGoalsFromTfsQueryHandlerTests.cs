using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetGoalsFromTfsQueryHandlerTests
{
    private PoToolDbContext _dbContext = null!;
    private TfsConfigurationService _configService = null!;
    private Mock<ITfsClient> _tfsClientMock = null!;
    private Mock<ILogger<GetGoalsFromTfsQueryHandler>> _loggerMock = null!;
    private GetGoalsFromTfsQueryHandler _handler = null!;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new PoToolDbContext(options);

        var configLogger = new Mock<ILogger<TfsConfigurationService>>();
        var gateLogger = new Mock<ILogger<EfConcurrencyGate>>();
        var gate = new EfConcurrencyGate(gateLogger.Object);
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object, gate);

        await _configService.SaveConfigAsync(
            "https://dev.azure.com/testorg",
            "TestProject",
            "TestProject\\Team",
            true);

        _tfsClientMock = new Mock<ITfsClient>();
        _loggerMock = new Mock<ILogger<GetGoalsFromTfsQueryHandler>>();

        _handler = new GetGoalsFromTfsQueryHandler(
            _tfsClientMock.Object,
            _configService,
            CreateConfiguration(useMockClient: false),
            _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task Handle_WithNoGoals_ReturnsEmptyList()
    {
        _tfsClientMock
            .Setup(client => client.GetWorkItemsByTypeAsync(WorkItemType.Goal, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WorkItemDto>());

        var result = await _handler.Handle(new GetGoalsFromTfsQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithMultipleGoals_ReturnsAll()
    {
        _tfsClientMock
            .Setup(client => client.GetWorkItemsByTypeAsync(WorkItemType.Goal, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreateGoal(100, "Goal Alpha"),
                CreateGoal(200, "Goal Beta"),
                CreateGoal(300, "Goal Gamma")
            });

        var result = await _handler.Handle(new GetGoalsFromTfsQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var goalsList = result.ToList();
        Assert.HasCount(3, goalsList);
        Assert.AreEqual("Goal Alpha", goalsList[0].Title);
        Assert.AreEqual(100, goalsList[0].TfsId);
        Assert.AreEqual("Goal Beta", goalsList[1].Title);
        Assert.AreEqual(200, goalsList[1].TfsId);
        Assert.AreEqual("Goal Gamma", goalsList[2].Title);
        Assert.AreEqual(300, goalsList[2].TfsId);
    }

    [TestMethod]
    public async Task Handle_WhenMockModeUsesNonMockClient_ThrowsInvalidOperationException()
    {
        var handler = new GetGoalsFromTfsQueryHandler(
            _tfsClientMock.Object,
            _configService,
            CreateConfiguration(useMockClient: true),
            _loggerMock.Object);

        try
        {
            await handler.Handle(new GetGoalsFromTfsQuery(), CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException to be thrown.");
        }
        catch (InvalidOperationException)
        {
            // Expected guardrail exception.
        }

        _tfsClientMock.Verify(
            client => client.GetWorkItemsByTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_WhenNoConfiguration_ReturnsEmptyList()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Database.EnsureCreated();

        var result = await _handler.Handle(new GetGoalsFromTfsQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WhenTfsClientThrows_ReturnsEmptyList()
    {
        _tfsClientMock
            .Setup(client => client.GetWorkItemsByTypeAsync(WorkItemType.Goal, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Server error"));

        var result = await _handler.Handle(new GetGoalsFromTfsQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    private static IConfiguration CreateConfiguration(bool useMockClient)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TfsIntegration:UseMockClient"] = useMockClient.ToString()
            })
            .Build();
    }

    private static WorkItemDto CreateGoal(int id, string title)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: WorkItemType.Goal,
            Title: title,
            ParentTfsId: null,
            AreaPath: string.Empty,
            IterationPath: string.Empty,
            State: string.Empty,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
    }
}
