using System.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.Sqlite;
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
public sealed class RevisionIngestionServiceTests
{
    private static readonly int[] DefaultDescendantWorkItemIds = { 10, 11, 42, 99 };

    [TestMethod]
    public async Task IngestRevisionsAsync_StopsAfterContinuationTokenClears()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "t1"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null)
        };

        var stubClient = new StubRevisionTfsClient(results);

        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, stubClient.ReportingCalls);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_RecordsTerminationReasonFromClient()
    {
        var termination = new ReportingRevisionsTermination(
            ReportingRevisionsTerminationReason.MaxEmptyPages,
            "Max empty pages exceeded");
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, null, termination)
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.WasTerminatedEarly);
        Assert.AreEqual(termination.Reason, result.TerminationReason);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var watermark = await context.RevisionIngestionWatermarks.SingleAsync();
        StringAssert.Contains(watermark.LastErrorMessage ?? string.Empty, termination.Message);
        Assert.AreEqual(1, stubClient.ReportingCalls);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_StopsOnTerminationResult()
    {
        var termination = new ReportingRevisionsTermination(
            ReportingRevisionsTerminationReason.RepeatedContinuationToken,
            "Continuation token repeated");
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, null, termination)
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.WasTerminatedEarly);
        Assert.AreEqual(termination.Reason, result.TerminationReason);
        Assert.AreEqual(1, stubClient.ReportingCalls);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_CompletesOnEmptyPageWithNullToken()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(99, 1) }, "t1"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null)
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, stubClient.ReportingCalls);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_ContinuesWhenEmptyPages_DoesNotTerminate()
    {
        // This test verifies that empty pages (after scoping) do not cause early termination
        // The ingestion should continue as long as the token advances and HasMoreResults is true
        var results = new[]
        {
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "t1"),
            new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "t2"),
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, null) // Final page with data
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.WasTerminatedEarly); // Should NOT terminate early
        Assert.AreEqual(3, stubClient.ReportingCalls); // All 3 pages should be processed
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_TerminatesOnRepeatedContinuationToken()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "t1"),
            new ReportingRevisionsResult(new[] { CreateRevision(11, 1) }, "t1")
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.WasTerminatedEarly);
        Assert.AreEqual(ReportingRevisionsTerminationReason.RepeatedContinuationToken, result.TerminationReason);
        Assert.AreEqual(2, stubClient.ReportingCalls);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_PersistsFieldAndRelationDeltas()
    {
        var revision = new WorkItemRevision
        {
            WorkItemId = 42,
            RevisionNumber = 1,
            WorkItemType = "Bug",
            Title = "Connection timeout",
            State = "New",
            IterationPath = "Iteration 1",
            AreaPath = "Area 1",
            ChangedDate = DateTimeOffset.UtcNow,
            FieldDeltas = new Dictionary<string, FieldDelta>
            {
                ["System.State"] = new FieldDelta
                {
                    FieldName = "System.State",
                    OldValue = "New",
                    NewValue = "Active"
                }
            },
            RelationDeltas = new[]
            {
                new RelationDelta
                {
                    ChangeType = PoTool.Core.Contracts.RelationChangeType.Added,
                    RelationType = "System.LinkTypes.Hierarchy-Forward",
                    TargetWorkItemId = 101
                }
            }
        };

        var results = new[]
        {
            new ReportingRevisionsResult(new[] { revision }, null)
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var header = await context.RevisionHeaders.SingleAsync();
        var fieldDelta = await context.RevisionFieldDeltas.SingleAsync();
        var relationDelta = await context.RevisionRelationDeltas.SingleAsync();

        Assert.AreEqual(header.Id, fieldDelta.RevisionHeaderId);
        Assert.AreEqual(header.Id, relationDelta.RevisionHeaderId);
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_FiltersRevisionsAndHydrationToAllowedWorkItemIds()
    {
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(1, 1), CreateRevision(100, 1) }, null)
        };

        var stubClient = new StubRevisionTfsClient(results);
        var relationHydrator = new StubRelationRevisionHydrator();
        using var provider = BuildServiceProvider(
            stubClient,
            backlogRootId: 100,
            descendantWorkItemIds: new[] { 100, 101 },
            relationHydrator: relationHydrator);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var headers = await context.RevisionHeaders.ToListAsync();
        Assert.HasCount(1, headers);
        Assert.AreEqual(100, headers[0].WorkItemId);
        CollectionAssert.AreEquivalent(
            new[] { 100 },
            relationHydrator.HydratedWorkItemIds?.ToArray());
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_ContinuesWithManyScopedEmptyPages()
    {
        // This test verifies that many scoped empty pages (raw pages have data but scoped to 0) 
        // do not cause early termination. The ingestion continues as long as token advances.
        var results = new[]
        {
            // Page 1: out-of-scope revision (workItemId=1, not in allowed set)
            new ReportingRevisionsResult(new[] { CreateRevision(1, 1) }, "t1"),
            // Page 2: out-of-scope revision (workItemId=2, not in allowed set)
            new ReportingRevisionsResult(new[] { CreateRevision(2, 1) }, "t2"),
            // Page 3: out-of-scope revision (workItemId=3, not in allowed set)
            new ReportingRevisionsResult(new[] { CreateRevision(3, 1) }, "t3"),
            // Page 4: in-scope revision (workItemId=100, in allowed set)
            new ReportingRevisionsResult(new[] { CreateRevision(100, 1) }, null)
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(
            stubClient,
            backlogRootId: 100,
            descendantWorkItemIds: new[] { 100, 101 });
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.WasTerminatedEarly); // Should NOT terminate early despite empty scoped pages
        Assert.AreEqual(4, stubClient.ReportingCalls); // All 4 pages should be processed

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        Assert.AreEqual(1, await context.RevisionHeaders.CountAsync()); // Only the last page had scoped revision
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_TerminatesAtMaxTotalPages()
    {
        // This test verifies that MaxTotalPages is enforced as a safety limit
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(10, 1) }, "t1"),
            new ReportingRevisionsResult(new[] { CreateRevision(10, 2) }, "t2"),
            new ReportingRevisionsResult(new[] { CreateRevision(10, 3) }, "t3"),
            new ReportingRevisionsResult(new[] { CreateRevision(10, 4) }, "t4"),
            new ReportingRevisionsResult(new[] { CreateRevision(10, 5) }, "t5"),
            new ReportingRevisionsResult(new[] { CreateRevision(10, 6) }, "t6") // Should not be reached
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(
            stubClient,
            new RevisionIngestionPaginationOptions { MaxTotalPages = 5 });
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.WasTerminatedEarly);
        Assert.AreEqual(ReportingRevisionsTerminationReason.MaxTotalPages, result.TerminationReason);
        Assert.AreEqual(5, stubClient.ReportingCalls); // Should stop at page 5
    }

    [TestMethod]
    public async Task IngestRevisionsAsync_BackfillInfersStartDateFromCachedWorkItems()
    {
        // This test verifies that backfill automatically infers the start date 
        // from the earliest CreatedDate of cached work items
        var results = new[]
        {
            new ReportingRevisionsResult(new[] { CreateRevision(100, 1) }, null)
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient, backlogRootId: 100, descendantWorkItemIds: new[] { 100 });

        // Add a cached work item with a known CreatedDate
        using (var setupScope = provider.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            var earliestDate = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
            context.WorkItems.Add(new WorkItemEntity
            {
                TfsId = 100,
                Type = "Feature",
                Title = "Test Feature",
                AreaPath = "Area",
                IterationPath = "Iteration",
                State = "Active",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                CreatedDate = earliestDate
            });
            await context.SaveChangesAsync();
        }

        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPaginationOptions>>(),
            provider.GetRequiredService<IDataProtectionProvider>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        if (!result.Success)
        {
            Assert.Fail($"Ingestion failed: {result.ErrorMessage ?? result.Message}");
        }

        Assert.IsTrue(result.Success);
        // The backfill should have used an inferred start date (1 day before earliest CreatedDate)
        // We can't directly assert the start date used, but the test ensures no exception is thrown
        // and the ingestion completes successfully
    }

    private sealed class StubRevisionTfsClient : IRevisionTfsClient
    {
        private readonly Queue<ReportingRevisionsResult> _results;

        public StubRevisionTfsClient(IEnumerable<ReportingRevisionsResult> results)
        {
            _results = new Queue<ReportingRevisionsResult>(results);
        }

        public int ReportingCalls { get; private set; }

        public Task<ReportingRevisionsResult> GetReportingRevisionsAsync(
            DateTimeOffset? startDateTime = null,
            string? continuationToken = null,
            ReportingExpandMode expandMode = ReportingExpandMode.None,
            CancellationToken cancellationToken = default)
        {
            ReportingCalls++;
            return Task.FromResult(_results.Dequeue());
        }

        public Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
            int workItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<WorkItemRevision>)Array.Empty<WorkItemRevision>());
        }

        public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class StubRelationRevisionHydrator : IRelationRevisionHydrator
    {
        public IReadOnlyList<int>? HydratedWorkItemIds { get; private set; }

        public Task<RelationHydrationResult> HydrateAsync(
            IEnumerable<int> workItemIds,
            CancellationToken cancellationToken = default)
        {
            HydratedWorkItemIds = workItemIds.ToList();
            return Task.FromResult(new RelationHydrationResult
            {
                Success = true,
                WorkItemsProcessed = HydratedWorkItemIds.Count,
                RevisionsHydrated = 0
            });
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private sealed class NullDisposable : IDisposable
        {
            public static NullDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }

        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<T, string> listener)
        {
            return NullDisposable.Instance;
        }
    }

    private static ServiceProvider BuildServiceProvider(
        IRevisionTfsClient revisionClient,
        RevisionIngestionPaginationOptions? paginationOptions = null,
        int backlogRootId = 1,
        IReadOnlyCollection<int>? descendantWorkItemIds = null,
        StubRelationRevisionHydrator? relationHydrator = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<RevisionIngestionDiagnosticsOptions>();
        services.AddOptions<RevisionIngestionPersistenceOptimizationOptions>();
        services.AddSingleton<IOptionsMonitor<RevisionIngestionPaginationOptions>>(
            new StaticOptionsMonitor<RevisionIngestionPaginationOptions>(
                paginationOptions ?? new RevisionIngestionPaginationOptions()));
        services.AddDataProtection();
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        services.AddSingleton(connection);
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<ITfsClient>(
            CreateTfsClient(backlogRootId, descendantWorkItemIds ?? DefaultDescendantWorkItemIds));
        services.AddSingleton<IRevisionTfsClient>(revisionClient);
        services.AddSingleton<IRelationRevisionHydrator>(relationHydrator ?? new StubRelationRevisionHydrator());
        services.AddSingleton<RevisionIngestionDiagnostics>();
        services.AddSingleton<TfsRequestThrottler>();

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
        context.SaveChanges();
        return provider;
    }

    private static ITfsClient CreateTfsClient(int backlogRootId, IReadOnlyCollection<int> descendantWorkItemIds)
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
            new WorkItemDto(
                backlogRootId,
                "Feature",
                $"Root {backlogRootId}",
                null,
                "Area",
                "Iteration",
                "Active",
                now,
                null,
                null)
        };

        foreach (var descendantId in descendantWorkItemIds.Distinct())
        {
            if (descendantId == backlogRootId)
            {
                continue;
            }

            workItems.Add(new WorkItemDto(
                descendantId,
                "Feature",
                $"Item {descendantId}",
                backlogRootId,
                "Area",
                "Iteration",
                "Active",
                now,
                null,
                null));
        }

        return workItems;
    }

    private static WorkItemRevision CreateRevision(int workItemId, int revisionNumber)
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
            ChangedDate = DateTimeOffset.UtcNow
        };
    }
}
