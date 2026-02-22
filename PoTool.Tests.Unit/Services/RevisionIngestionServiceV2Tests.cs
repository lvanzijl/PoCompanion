using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class RevisionIngestionServiceV2Tests
{
    private static readonly int[] DefaultDescendantWorkItemIds = { 10, 11, 42, 99 };

    [TestMethod]
    public async Task IngestRevisionsAsync_NormalProgression_TerminatesWhenTokenBecomesNull()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1), CreateRevision(11, 1) }, "A"),
            new ReportingRevisionsResult(new[] { CreateRevision(42, 1), CreateRevision(99, 1) }, "B"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null)
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");
        Assert.IsGreaterThan(0, result.RevisionsIngested, "Expected persisted > 0");
        Assert.AreEqual(3, stubClient.ReportingCalls, "Expected 3 page calls (2 data + 1 terminal)");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_EmptyPageWithToken_FailsAfterBoundedRetries()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "A"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "B"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "C"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "D")
        };

        var options = new RevisionIngestionV2Options
        {
            RevisionIngestionMode = "V2",
            V2EnableWindowing = false,
            V2MaxEmptyPageRetries = 2
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient, v2Options: options);
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsFalse(result.Success, "Expected failure due to EmptyPageWithToken stall");
        StringAssert.Contains(result.ErrorMessage, "EmptyPageWithToken");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_TokenRepetition_FailsImmediately()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "A"),
            new ReportingRevisionsResult(new[] { CreateRevision(11, 1) }, "A")
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient,
            v2Options: new RevisionIngestionV2Options
            {
                RevisionIngestionMode = "V2",
                V2EnableWindowing = false
            });
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsFalse(result.Success, "Expected failure due to RepeatedToken");
        StringAssert.Contains(result.ErrorMessage, "RepeatedToken");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_CheckpointResume_SavesCheckpointAfterPages()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "A"),
            new ReportingRevisionsResult(new[] { CreateRevision(11, 1) }, null)
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient,
            v2Options: new RevisionIngestionV2Options
            {
                RevisionIngestionMode = "V2",
                V2EnableWindowing = false
            });
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");

        // Verify checkpoint was saved
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var watermark = await context.RevisionIngestionWatermarks
            .FirstOrDefaultAsync(w => w.ProductOwnerId == 1);

        Assert.IsNotNull(watermark, "Expected watermark checkpoint to be saved");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_NoWindowingMode_UsesOpenEndedWindow()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, null)
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient,
            v2Options: new RevisionIngestionV2Options
            {
                RevisionIngestionMode = "V2",
                V2EnableWindowing = false
            });
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");
        Assert.AreEqual(1, stubClient.ReportingCalls);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_NeverFabricatesTokens()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "ServerToken1"),
            new ReportingRevisionsResult(new[] { CreateRevision(11, 1) }, "ServerToken2"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null)
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient,
            v2Options: new RevisionIngestionV2Options
            {
                RevisionIngestionMode = "V2",
                V2EnableWindowing = false
            });
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        // Verify: first call has null token, subsequent calls use exact server tokens
        CollectionAssert.AreEqual(
            new string?[] { null, "ServerToken1", "ServerToken2" },
            stubClient.RequestedContinuationTokens,
            "V2 must only use tokens returned by the server, never fabricated values");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_FiltersRevisionsToAllowedScope()
    {
        // Include revisions for work item 999 which is NOT in the allowed scope
        var results = new[]
        {
            new ReportingRevisionsResult(
                new[] { CreateRevision(10, 1), CreateRevision(999, 1) },
                null)
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient,
            v2Options: new RevisionIngestionV2Options
            {
                RevisionIngestionMode = "V2",
                V2EnableWindowing = false
            });
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);

        // Verify only 1 revision persisted (10), not 999
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var headers = await context.RevisionHeaders.ToListAsync();
        Assert.HasCount(1, headers);
        Assert.AreEqual(10, headers[0].WorkItemId);
    }

    [TestMethod]
    public void HashToken_ReturnsNull_ForNullInput()
    {
        Assert.IsNull(RevisionIngestionServiceV2.HashToken(null));
    }

    [TestMethod]
    public void HashToken_ReturnsDeterministicHash_ForSameInput()
    {
        var hash1 = RevisionIngestionServiceV2.HashToken("test-token");
        var hash2 = RevisionIngestionServiceV2.HashToken("test-token");

        Assert.IsNotNull(hash1);
        Assert.AreEqual(hash1, hash2);
        Assert.AreEqual(12, hash1.Length);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private sealed class NullDisposable : IDisposable
        {
            public static NullDisposable Instance { get; } = new();
            public void Dispose() { }
        }

        public StaticOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string> listener) => NullDisposable.Instance;
    }

    private sealed class StubRevisionSource : IWorkItemRevisionSource
    {
        private readonly Queue<ReportingRevisionsResult> _results;

        public StubRevisionSource(IEnumerable<ReportingRevisionsResult> results)
        {
            _results = new Queue<ReportingRevisionsResult>(results);
        }

        public int ReportingCalls { get; private set; }
        public List<string?> RequestedContinuationTokens { get; } = new();

        public Task<ReportingRevisionsResult> GetRevisionsAsync(
            DateTimeOffset? startDateTime = null,
            string? continuationToken = null,
            IReadOnlyCollection<int>? scopedWorkItemIds = null,
            ReportingExpandMode expandMode = ReportingExpandMode.None,
            DateTimeOffset? endDateTime = null,
            CancellationToken cancellationToken = default)
        {
            ReportingCalls++;
            RequestedContinuationTokens.Add(continuationToken);
            return Task.FromResult(_results.Dequeue());
        }

        public Task<ReportingRevisionsResult> GetRevisionsForScopeAsync(
            IReadOnlyCollection<int> scopedWorkItemIds,
            DateTimeOffset? startDateTime = null,
            string? continuationToken = null,
            ReportingExpandMode expandMode = ReportingExpandMode.None,
            DateTimeOffset? endDateTime = null,
            CancellationToken cancellationToken = default)
        {
            return GetRevisionsAsync(startDateTime, continuationToken, scopedWorkItemIds, expandMode, endDateTime, cancellationToken);
        }

        public Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
            int workItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<WorkItemRevision>)Array.Empty<WorkItemRevision>());
        }
    }

    private static ServiceProvider BuildServiceProvider(
        IWorkItemRevisionSource revisionClient,
        RevisionIngestionV2Options? v2Options = null,
        int backlogRootId = 1,
        IReadOnlyCollection<int>? descendantWorkItemIds = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<RevisionIngestionPersistenceOptimizationOptions>();
        services.AddSingleton<IOptionsMonitor<RevisionIngestionV2Options>>(
            new StaticOptionsMonitor<RevisionIngestionV2Options>(
                v2Options ?? new RevisionIngestionV2Options
                {
                    RevisionIngestionMode = "V2",
                    V2EnableWindowing = false
                }));

        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        services.AddSingleton(connection);
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<ITfsClient>(
            CreateTfsClient(backlogRootId, descendantWorkItemIds ?? DefaultDescendantWorkItemIds));
        services.AddSingleton<IWorkItemRevisionSource>(revisionClient);
        services.AddSingleton<RevisionIngestionServiceV2>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        context.Database.EnsureCreated();
        context.Profiles.Add(new ProfileEntity
        {
            Id = 1,
            Name = "Test Owner",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });
        context.Products.Add(new ProductEntity
        {
            ProductOwnerId = 1,
            Name = "Test Product",
            BacklogRootWorkItemId = backlogRootId,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });
        context.WorkItems.Add(new WorkItemEntity
        {
            TfsId = backlogRootId,
            ParentTfsId = null,
            Type = "Feature",
            Title = $"Root {backlogRootId}",
            AreaPath = "Area",
            IterationPath = "Iteration",
            State = "Active",
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            CreatedDate = DateTimeOffset.UtcNow.AddDays(-2)
        });
        context.SaveChanges();
        return provider;
    }

    private static ITfsClient CreateTfsClient(
        int backlogRootId,
        IReadOnlyCollection<int> descendantWorkItemIds)
    {
        var workItems = CreateHierarchyWorkItems(backlogRootId, descendantWorkItemIds);
        var client = new Mock<ITfsClient>();
        client.Setup(tfs => tfs.GetWorkItemsByRootIdsAsync(
                It.IsAny<int[]>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<Action<int, int, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        client.Setup(tfs => tfs.GetWorkItemsByRootIdsWithDetailedProgressAsync(
                It.IsAny<int[]>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        return client.Object;
    }

    private static IReadOnlyList<WorkItemDto> CreateHierarchyWorkItems(
        int backlogRootId,
        IReadOnlyCollection<int> descendantWorkItemIds)
    {
        var now = DateTimeOffset.UtcNow;
        var workItems = new List<WorkItemDto>
        {
            new(backlogRootId, "Feature", $"Root {backlogRootId}", null, "Area", "Iteration", "Active", now, null, null)
        };

        foreach (var descendantId in descendantWorkItemIds.Distinct())
        {
            if (descendantId == backlogRootId) continue;
            workItems.Add(new WorkItemDto(
                descendantId, "Feature", $"Item {descendantId}", backlogRootId, "Area", "Iteration", "Active", now, null, null));
        }

        return workItems;
    }

    private static WorkItemRevision CreateRevision(int workItemId, int revisionNumber, DateTimeOffset? changedDate = null)
    {
        return new WorkItemRevision
        {
            WorkItemId = workItemId,
            RevisionNumber = revisionNumber,
            WorkItemType = "Bug",
            Title = "Test",
            State = "New",
            IterationPath = "Iteration 1",
            AreaPath = "Area 1",
            ChangedDate = changedDate ?? DateTimeOffset.UtcNow
        };
    }
}
