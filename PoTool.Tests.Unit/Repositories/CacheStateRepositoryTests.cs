using Microsoft.Data.Sqlite;
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

    [TestMethod]
    public async Task UpdateSyncStatusAsync_ReusesConcurrentCacheStateInsert_WhenInitialCreateRaces()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

        try
        {
            var baseOptions = new DbContextOptionsBuilder<PoToolDbContext>()
                .UseSqlite($"Data Source={databasePath}", sqliteOptions =>
                {
                    sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .Options;

            await using (var setupContext = new PoToolDbContext(baseOptions))
            {
                await setupContext.Database.EnsureCreatedAsync();
                var setupProfiles = new ProfileRepository(setupContext);
                await setupProfiles.CreateProfileAsync("Concurrent Owner", new List<int>());
            }

            var racingOptions = new DbContextOptionsBuilder<PoToolDbContext>()
                .UseSqlite($"Data Source={databasePath}", sqliteOptions =>
                {
                    sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .Options;

            await using var racingContext = new ConcurrentInsertPoToolDbContext(racingOptions, databasePath);
            var racingRepository = new CacheStateRepository(racingContext);

            await racingRepository.UpdateSyncStatusAsync(
                1,
                Shared.Settings.CacheSyncStatusDto.InProgress,
                "ComputeSprintTrends",
                0);

            await using var verificationContext = new PoToolDbContext(baseOptions);
            var cacheStates = await verificationContext.ProductOwnerCacheStates
                .Where(state => state.ProductOwnerId == 1)
                .ToListAsync();

            Assert.HasCount(1, cacheStates, "Concurrent create recovery must preserve a single cache-state row.");
            Assert.AreEqual(CacheSyncStatus.InProgress, cacheStates[0].SyncStatus);
            Assert.AreEqual("ComputeSprintTrends", cacheStates[0].CurrentSyncStage);
        }
        finally
        {
            File.Delete(databasePath);
        }
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
        Assert.IsNull(watermarks.PipelineFinish);
    }

    [TestMethod]
    public async Task GetWatermarksAsync_ReturnsFinishWatermark_WhenStoredSeparately()
    {
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        var pipelineStartWatermark = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var pipelineFinishWatermark = new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero);

        await _repository.MarkSyncSuccessAsync(
            profile.Id,
            10,
            5,
            3,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            pipelineStartWatermark,
            pipelineFinishWatermark);

        var watermarks = await _repository.GetWatermarksAsync(profile.Id);

        Assert.AreEqual(pipelineStartWatermark, watermarks.Pipeline);
        Assert.AreEqual(pipelineFinishWatermark, watermarks.PipelineFinish);
    }

    private sealed class ConcurrentInsertPoToolDbContext : PoToolDbContext
    {
        private readonly string _databasePath;
        private bool _hasInjectedConflict;

        public ConcurrentInsertPoToolDbContext(
            DbContextOptions<PoToolDbContext> options,
            string databasePath)
            : base(options)
        {
            _databasePath = databasePath;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (!_hasInjectedConflict &&
                ChangeTracker.Entries<ProductOwnerCacheStateEntity>().Any(entry => entry.State == EntityState.Added))
            {
                _hasInjectedConflict = true;

                var competingOptions = new DbContextOptionsBuilder<PoToolDbContext>()
                    .UseSqlite(new SqliteConnection($"Data Source={_databasePath}"), sqliteOptions =>
                    {
                        sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    })
                    .Options;

                await using var competingContext = new PoToolDbContext(competingOptions);
                await competingContext.Database.OpenConnectionAsync(cancellationToken);
                competingContext.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity
                {
                    ProductOwnerId = 1,
                    SyncStatus = CacheSyncStatus.Idle
                });
                await competingContext.SaveChangesAsync(cancellationToken);
                await competingContext.Database.CloseConnectionAsync();
            }

            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
