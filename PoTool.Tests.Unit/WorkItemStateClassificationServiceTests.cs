using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit;

/// <summary>
/// Tests for WorkItemStateClassificationService, focusing on caching behavior.
/// </summary>
[TestClass]
public class WorkItemStateClassificationServiceTests
{
    private TfsConfigurationService _configService = null!;
    private Mock<ILogger<WorkItemStateClassificationService>> _loggerMock = null!;
    private Mock<ILogger<TfsConfigurationService>> _configLoggerMock = null!;
    private PoToolDbContext _dbContext = null!;
    private WorkItemStateClassificationService _service = null!;

    [TestInitialize]
    public async Task Setup()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PoToolDbContext(options);

        // Setup concurrency gate mock
        var gateMock = new Mock<IEfConcurrencyGate>();
        gateMock.Setup(g => g.ExecuteAsync(It.IsAny<Func<Task<TfsConfigEntity?>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<TfsConfigEntity?>>, CancellationToken>((func, ct) => func());
        
        // Setup loggers
        _configLoggerMock = new Mock<ILogger<TfsConfigurationService>>();
        _loggerMock = new Mock<ILogger<WorkItemStateClassificationService>>();

        // Create real TfsConfigurationService with test data
        _configService = new TfsConfigurationService(_dbContext, _configLoggerMock.Object, gateMock.Object);
        
        // Add test configuration to database
        var configEntity = new TfsConfigEntity
        {
            Id = 1,
            Project = "TestProject",
            Url = "https://test.com",
            UseDefaultCredentials = true,
            DefaultAreaPath = "TestProject",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _dbContext.TfsConfigs.AddAsync(configEntity);
        await _dbContext.SaveChangesAsync();

        _service = new WorkItemStateClassificationService(_dbContext, _configService, _loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task GetClassificationsAsync_FirstCall_LogsAndQueriesDatabase()
    {
        // Arrange
        await _dbContext.WorkItemStateClassifications.AddAsync(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "TestProject",
            WorkItemType = "Task",
            StateName = "Done",
            Classification = (int)StateClassification.Done,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetClassificationsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("TestProject", result.ProjectName);
        Assert.HasCount(1, result.Classifications);
        
        // Verify logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetClassificationsAsync_SecondCall_ReturnsCachedResultWithoutLogging()
    {
        // Arrange
        await _dbContext.WorkItemStateClassifications.AddAsync(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "TestProject",
            WorkItemType = "Task",
            StateName = "Done",
            Classification = (int)StateClassification.Done,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - First call
        var result1 = await _service.GetClassificationsAsync();
        
        // Reset the logger mock to clear call counts
        _loggerMock.Reset();
        
        // Act - Second call
        var result2 = await _service.GetClassificationsAsync();

        // Assert
        Assert.IsNotNull(result2);
        Assert.AreEqual("TestProject", result2.ProjectName);
        Assert.HasCount(1, result2.Classifications);
        Assert.AreSame(result1, result2, "Should return the same cached instance");
        
        // Verify NO logging occurred on second call
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetClassificationsAsync_RepeatedCalls_UsesCache()
    {
        // Arrange - Add test data
        await _dbContext.WorkItemStateClassifications.AddAsync(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "TestProject",
            WorkItemType = "Task",
            StateName = "Done",
            Classification = (int)StateClassification.Done,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - Make 10 calls
        var results = new List<GetStateClassificationsResponse>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(await _service.GetClassificationsAsync());
        }

        // Assert
        Assert.HasCount(10, results);
        
        // All results should be the same cached instance
        var firstResult = results[0];
        Assert.IsTrue(results.All(r => ReferenceEquals(r, firstResult)), "All results should be the same cached instance");
        
        // Verify logging only occurred once (on first call)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SaveClassificationsAsync_InvalidatesCache()
    {
        // Arrange - First call to populate cache
        var result1 = await _service.GetClassificationsAsync();
        Assert.IsNotNull(result1);
        
        // Reset logger to clear counts
        _loggerMock.Reset();

        // Act - Save new classifications
        var saveRequest = new SaveStateClassificationsRequest
        {
            ProjectName = "TestProject",
            Classifications = new List<WorkItemStateClassificationDto>
            {
                new() { WorkItemType = "Bug", StateName = "New", Classification = StateClassification.New }
            }
        };
        var saveResult = await _service.SaveClassificationsAsync(saveRequest);
        Assert.IsTrue(saveResult);

        // Act - Get classifications again after save
        var result2 = await _service.GetClassificationsAsync();

        // Assert
        Assert.IsNotNull(result2);
        Assert.AreNotSame(result1, result2, "Cache should have been invalidated");
        Assert.HasCount(1, result2.Classifications);
        Assert.AreEqual("Bug", result2.Classifications[0].WorkItemType);
        
        // Verify logging occurred again (cache was invalidated)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetClassificationAsync_UsesCache()
    {
        // Arrange
        await _dbContext.WorkItemStateClassifications.AddAsync(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "TestProject",
            WorkItemType = "Task",
            StateName = "Done",
            Classification = (int)StateClassification.Done,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - Make multiple calls
        var result1 = await _service.GetClassificationAsync("Task", "Done");
        
        _loggerMock.Reset();
        
        var result2 = await _service.GetClassificationAsync("Task", "Done");
        var result3 = await _service.GetClassificationAsync("Task", "Done");

        // Assert
        Assert.AreEqual(StateClassification.Done, result1);
        Assert.AreEqual(StateClassification.Done, result2);
        Assert.AreEqual(StateClassification.Done, result3);
        
        // Verify no logging on subsequent calls
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task IsDoneStateAsync_UsesCache()
    {
        // Arrange
        await _dbContext.WorkItemStateClassifications.AddAsync(new WorkItemStateClassificationEntity
        {
            TfsProjectName = "TestProject",
            WorkItemType = "Task",
            StateName = "Done",
            Classification = (int)StateClassification.Done,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - Make multiple calls
        var result1 = await _service.IsDoneStateAsync("Task", "Done");
        
        _loggerMock.Reset();
        
        var result2 = await _service.IsDoneStateAsync("Task", "Done");
        var result3 = await _service.IsDoneStateAsync("Task", "Done");

        // Assert
        Assert.IsTrue(result1);
        Assert.IsTrue(result2);
        Assert.IsTrue(result3);
        
        // Verify no logging on subsequent calls
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetClassificationsAsync_NoCustomClassifications_ReturnsDefaults()
    {
        // Arrange - Empty database

        // Act
        var result = await _service.GetClassificationsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("TestProject", result.ProjectName);
        Assert.IsTrue(result.IsDefault);
        Assert.IsNotEmpty(result.Classifications, "Should have default classifications");
    }

    [TestMethod]
    public async Task GetClassificationsAsync_DefaultsAreCached()
    {
        // Arrange - Empty database

        // Act - First call
        var result1 = await _service.GetClassificationsAsync();
        
        _loggerMock.Reset();
        
        // Act - Second call
        var result2 = await _service.GetClassificationsAsync();

        // Assert
        Assert.AreSame(result1, result2, "Default classifications should also be cached");
        
        // Verify no logging on second call
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting state classifications")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
