using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Exceptions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;

namespace PoTool.Tests.Unit.Repositories;

/// <summary>
/// Tests for CacheStateRepository to ensure proper validation of ProductOwner existence.
/// </summary>
[TestClass]
public class CacheStateRepositoryTests
{
    private PoToolDbContext _context = null!;
    private CacheStateRepository _repository = null!;
    private ProfileRepository _profileRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory SQLite database for real query translation testing
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .Options;
        _context = new PoToolDbContext(options);
        // Important: Keep connection open for in-memory database to persist
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new CacheStateRepository(_context);
        _profileRepository = new ProfileRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Close connection to dispose of in-memory database
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    /// <summary>
    /// Verifies that GetOrCreateCacheStateAsync throws ProductOwnerNotFoundException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task GetOrCreateCacheStateAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.GetOrCreateCacheStateAsync(nonExistentProductOwnerId);
            Assert.Fail("Expected ProductOwnerNotFoundException was not thrown");
        }
        catch (ProductOwnerNotFoundException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
            StringAssert.Contains(ex.Message, nonExistentProductOwnerId.ToString());
        }
    }

    /// <summary>
    /// Verifies that GetOrCreateCacheStateAsync creates cache state successfully
    /// when ProductOwner exists.
    /// </summary>
    [TestMethod]
    public async Task GetOrCreateCacheStateAsync_CreatesSuccessfully_WhenProductOwnerExists()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());

        // Act
        var cacheState = await _repository.GetOrCreateCacheStateAsync(profile.Id);

        // Assert
        Assert.IsNotNull(cacheState);
        Assert.AreEqual(profile.Id, cacheState.ProductOwnerId);
        Assert.AreEqual(Shared.Settings.CacheSyncStatusDto.Idle, cacheState.SyncStatus);
    }

    /// <summary>
    /// Verifies that GetOrCreateCacheStateAsync returns existing cache state
    /// when called multiple times.
    /// </summary>
    [TestMethod]
    public async Task GetOrCreateCacheStateAsync_ReturnsExisting_WhenCalledMultipleTimes()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        var firstCall = await _repository.GetOrCreateCacheStateAsync(profile.Id);

        // Act
        var secondCall = await _repository.GetOrCreateCacheStateAsync(profile.Id);

        // Assert
        Assert.AreEqual(firstCall.ProductOwnerId, secondCall.ProductOwnerId);
        Assert.AreEqual(firstCall.SyncStatus, secondCall.SyncStatus);
    }

    /// <summary>
    /// Verifies that UpdateSyncStatusAsync throws ProductOwnerNotFoundException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task UpdateSyncStatusAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.UpdateSyncStatusAsync(
                nonExistentProductOwnerId,
                Shared.Settings.CacheSyncStatusDto.InProgress,
                "Test Stage",
                50);
            Assert.Fail("Expected ProductOwnerNotFoundException was not thrown");
        }
        catch (ProductOwnerNotFoundException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that MarkSyncSuccessAsync throws ProductOwnerNotFoundException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task MarkSyncSuccessAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.MarkSyncSuccessAsync(
                nonExistentProductOwnerId,
                100, 50, 25,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            Assert.Fail("Expected ProductOwnerNotFoundException was not thrown");
        }
        catch (ProductOwnerNotFoundException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that MarkSyncFailedAsync throws ProductOwnerNotFoundException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task MarkSyncFailedAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.MarkSyncFailedAsync(
                nonExistentProductOwnerId,
                "Test error message",
                "Test stage");
            Assert.Fail("Expected ProductOwnerNotFoundException was not thrown");
        }
        catch (ProductOwnerNotFoundException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    /// <summary>
    /// Verifies that ResetCacheStateAsync throws ProductOwnerNotFoundException
    /// when ProductOwner does not exist.
    /// </summary>
    [TestMethod]
    public async Task ResetCacheStateAsync_ThrowsException_WhenProductOwnerDoesNotExist()
    {
        // Arrange
        const int nonExistentProductOwnerId = 999;

        // Act & Assert
        try
        {
            await _repository.ResetCacheStateAsync(nonExistentProductOwnerId);
            Assert.Fail("Expected ProductOwnerNotFoundException was not thrown");
        }
        catch (ProductOwnerNotFoundException ex)
        {
            StringAssert.Contains(ex.Message, "ProductOwner does not exist");
        }
    }

    [TestMethod]
    public async Task ResetCacheStateAsync_ClearsRevisionCacheAndWatermark()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        var header = new RevisionHeaderEntity
        {
            WorkItemId = 100,
            RevisionNumber = 1,
            WorkItemType = "Feature",
            Title = "Sample",
            State = "New",
            IterationPath = "Iteration 1",
            AreaPath = "Area 1",
            ChangedDate = DateTimeOffset.UtcNow,
            IngestedAt = DateTimeOffset.UtcNow
        };

        _context.RevisionHeaders.Add(header);
        await _context.SaveChangesAsync();

        _context.RevisionFieldDeltas.Add(new RevisionFieldDeltaEntity
        {
            RevisionHeaderId = header.Id,
            FieldName = "System.Title",
            OldValue = "Old",
            NewValue = "New"
        });

        _context.RevisionRelationDeltas.Add(new RevisionRelationDeltaEntity
        {
            RevisionHeaderId = header.Id,
            ChangeType = RelationChangeType.Added,
            RelationType = "System.LinkTypes.Hierarchy-Forward",
            TargetWorkItemId = 200
        });

        _context.RevisionIngestionWatermarks.Add(new RevisionIngestionWatermarkEntity
        {
            ProductOwnerId = profile.Id,
            IsInitialBackfillComplete = false
        });

        await _context.SaveChangesAsync();

        // Act
        await _repository.ResetCacheStateAsync(profile.Id);

        // Assert
        Assert.AreEqual(0, await _context.RevisionHeaders.CountAsync());
        Assert.AreEqual(0, await _context.RevisionFieldDeltas.CountAsync());
        Assert.AreEqual(0, await _context.RevisionRelationDeltas.CountAsync());
        Assert.AreEqual(0, await _context.RevisionIngestionWatermarks.CountAsync());
    }

    /// <summary>
    /// Verifies that GetWatermarksAsync returns null values
    /// when ProductOwner does not have cache state.
    /// </summary>
    [TestMethod]
    public async Task GetWatermarksAsync_ReturnsNullValues_WhenCacheStateDoesNotExist()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());

        // Act
        var watermarks = await _repository.GetWatermarksAsync(profile.Id);

        // Assert
        Assert.IsNull(watermarks.WorkItem);
        Assert.IsNull(watermarks.PullRequest);
        Assert.IsNull(watermarks.Pipeline);
    }
}
