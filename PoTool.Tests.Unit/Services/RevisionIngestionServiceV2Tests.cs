using Microsoft.AspNetCore.DataProtection;
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
using PoTool.Integrations.Tfs.Clients;
using PoTool.Integrations.Tfs.Diagnostics;
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
            V2MaxConsecutiveEmptyPages = 2
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

    [TestMethod]
    public async Task IngestRevisionsAsync_ResumesFromCheckpointToken()
    {
        // Simulate a previous run that saved checkpoint with token "A" at window start
        var windowStart = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Second run: page fetched with resume token "A" returns data + token "B", then terminal page
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(42, 1) }, "B"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null)
        };

        var stubClient = new StubRevisionSource(results);
        var backfillProvider = new StubBackfillStartProvider(windowStart);
        using var provider = BuildServiceProvider(stubClient,
            v2Options: new RevisionIngestionV2Options
            {
                RevisionIngestionMode = "V2",
                V2EnableWindowing = false
            },
            backfillStartProvider: backfillProvider);

        // Pre-seed a watermark with an in-progress checkpoint
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            context.RevisionIngestionWatermarks.Add(new RevisionIngestionWatermarkEntity
            {
                ProductOwnerId = 1,
                ContinuationToken = "A",
                LastSyncStartDateTime = windowStart,
                LastRunOutcome = "V2_InProgress",
                IsInitialBackfillComplete = false
            });
            await context.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();
        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");

        // Verify V2 resumed from token "A" (first call should use token "A", not null)
        Assert.AreEqual("A", stubClient.RequestedContinuationTokens[0],
            "First call should use the checkpoint resume token 'A'");
        Assert.AreEqual("B", stubClient.RequestedContinuationTokens[1],
            "Second call should use the server-provided token 'B'");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_ClearsCheckpointAfterWindowCompletes()
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

        await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        // Verify checkpoint is cleared (outcome = V2_Completed, token = null)
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var watermark = await context.RevisionIngestionWatermarks
            .FirstOrDefaultAsync(w => w.ProductOwnerId == 1);

        Assert.IsNotNull(watermark);
        Assert.IsNull(watermark.ContinuationToken, "Token should be cleared after window completes");
        Assert.AreEqual("V2_Completed", watermark.LastRunOutcome);
    }

    [TestMethod]
    public async Task Dispatcher_RoutesToV2_WhenModeIsV2()
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
            },
            registerDispatcher: true);

        var dispatcher = provider.GetRequiredService<IRevisionIngestionService>();
        var result = await dispatcher.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");
        // V2 was called (stub was consumed by V2)
        Assert.AreEqual(1, stubClient.ReportingCalls,
            "Expected V2 to have called the revision source");
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
        public List<DateTimeOffset?> RequestedStartDateTimes { get; } = new();

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
            RequestedStartDateTimes.Add(startDateTime);
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
        IReadOnlyCollection<int>? descendantWorkItemIds = null,
        bool registerDispatcher = false,
        IBackfillStartProvider? backfillStartProvider = null,
        TimeProvider? timeProvider = null)
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
        services.AddSingleton<IBackfillStartProvider>(
            backfillStartProvider ?? new StubBackfillStartProvider(null));
        if (timeProvider != null)
        {
            services.AddSingleton(timeProvider);
        }
        services.AddSingleton<RevisionIngestionServiceV2>();

        if (registerDispatcher)
        {
            // V1 dependencies for dispatcher test
            services.AddOptions<RevisionIngestionDiagnosticsOptions>();
            services.AddSingleton<IOptionsMonitor<RevisionIngestionPaginationOptions>>(
                new StaticOptionsMonitor<RevisionIngestionPaginationOptions>(
                    new RevisionIngestionPaginationOptions()));
            services.AddDataProtection();
            services.AddSingleton<RevisionIngestionDiagnostics>();
            services.AddSingleton<TfsRequestThrottler>();
            services.AddSingleton<IRelationRevisionHydrator>(new StubRelationRevisionHydrator());
            services.AddSingleton<RevisionIngestionService>();
            services.AddSingleton<IRevisionIngestionService, RevisionIngestionDispatcher>();
        }

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

    private sealed class StubRelationRevisionHydrator : IRelationRevisionHydrator
    {
        public Task<RelationHydrationResult> HydrateAsync(
            IEnumerable<int> workItemIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RelationHydrationResult
            {
                Success = true,
                WorkItemsProcessed = 0,
                RevisionsHydrated = 0
            });
        }
    }

    private sealed class StubBackfillStartProvider : IBackfillStartProvider
    {
        private readonly DateTimeOffset? _earliestDate;

        public StubBackfillStartProvider(DateTimeOffset? earliestDate)
        {
            _earliestDate = earliestDate;
        }

        public Task<DateTimeOffset?> GetEarliestChangedDateUtcAsync(
            IReadOnlyCollection<int> workItemIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_earliestDate);
        }
    }

    // ─── Tests for empty-token chains (A–D) ────────────────────────

    [TestMethod]
    public async Task IngestRevisionsAsync_EmptyTokenChain_EventuallyTerminatesCleanly()
    {
        // Test 1: Empty-token chain eventually terminates cleanly
        // call1: raw=0, nextToken=T1
        // call2: raw=0, nextToken=T2
        // call3: raw=5, nextToken=T3
        // call4: raw=0, nextToken=null
        var results = new[]
        {
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T1"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T2"),
            new ReportingRevisionsResult(new[]
            {
                CreateRevision(10, 1), CreateRevision(11, 1),
                CreateRevision(42, 1), CreateRevision(99, 1),
                CreateRevision(10, 2)
            }, "T3"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null)
        };

        var options = new RevisionIngestionV2Options
        {
            RevisionIngestionMode = "V2",
            V2EnableWindowing = false,
            V2MaxConsecutiveEmptyPages = 5
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient, v2Options: options);
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");
        Assert.AreEqual(4, stubClient.ReportingCalls, "Expected 4 page calls");
        Assert.IsGreaterThan(0, result.RevisionsIngested, "Expected persisted > 0");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_EmptyTokenChainExceedsThreshold_Fails()
    {
        // Test 2: Empty-token chain exceeds threshold -> fails
        // raw=0 nextToken=T1, raw=0 nextToken=T2, raw=0 nextToken=T3
        // V2MaxConsecutiveEmptyPages=2, so 3 empty pages > 2 → fail
        var results = new[]
        {
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T1"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T2"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T3")
        };

        var options = new RevisionIngestionV2Options
        {
            RevisionIngestionMode = "V2",
            V2EnableWindowing = false,
            V2MaxConsecutiveEmptyPages = 2
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient, v2Options: options);
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsFalse(result.Success, "Expected failure due to EmptyPageWithToken stall");
        StringAssert.Contains(result.ErrorMessage, "EmptyPageWithToken");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_TokenCycleDetection_Fails()
    {
        // Test 3: Token cycle detection -> fails
        // raw=0 nextToken=T1, raw=0 nextToken=T2, raw=0 nextToken=T1 (cycle)
        var results = new[]
        {
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T1"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T2"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "T1")
        };

        var options = new RevisionIngestionV2Options
        {
            RevisionIngestionMode = "V2",
            V2EnableWindowing = false,
            V2MaxConsecutiveEmptyPages = 100 // high enough so empty threshold doesn't trigger
        };

        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient, v2Options: options);
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsFalse(result.Success, "Expected failure due to RepeatedTokenCycle");
        StringAssert.Contains(result.ErrorMessage, "RepeatedTokenCycle");
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_BackfillStartDerivation_UsesDerivedValue()
    {
        // Test 4: Backfill start derivation uses derived value when available
        var derivedDate = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1, derivedDate.AddDays(1)) }, null)
        };

        var options = new RevisionIngestionV2Options
        {
            RevisionIngestionMode = "V2",
            V2EnableWindowing = false
        };

        var backfillProvider = new StubBackfillStartProvider(derivedDate);
        var stubClient = new StubRevisionSource(results);
        using var provider = BuildServiceProvider(stubClient, v2Options: options,
            backfillStartProvider: backfillProvider);
        var service = provider.GetRequiredService<RevisionIngestionServiceV2>();

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success, $"Expected success but got: {result.Message}");

        // Verify the startDateTime passed to the revision source is at or after the derived date
        // (not 2000-01-01)
        Assert.AreEqual(1, stubClient.ReportingCalls);
        var requestedStart = stubClient.RequestedStartDateTimes[0];
        Assert.IsNotNull(requestedStart, "startDateTime should not be null");
        Assert.IsGreaterThanOrEqualTo(derivedDate, requestedStart!.Value,
            "startDateTime should be >= derived date");
        Assert.IsGreaterThanOrEqualTo(2021, requestedStart.Value.Year,
            "startDateTime year should be >= 2021 (derived)");
    }
}
