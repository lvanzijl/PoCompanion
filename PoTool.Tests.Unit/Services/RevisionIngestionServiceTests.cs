using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.Sqlite;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Integrations.Tfs.Diagnostics;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class RevisionIngestionServiceTests
{
    [TestMethod]
    public async Task IngestRevisionsAsync_StopsAfterContinuationTokenClears()
    {
        var results = new[]
        {
            new ReportingRevisionsResult
            {
                Revisions = Array.Empty<WorkItemRevision>(),
                ContinuationToken = "t1"
            },
            new ReportingRevisionsResult
            {
                Revisions = Array.Empty<WorkItemRevision>(),
                ContinuationToken = null
            }
        };

        var stubClient = new StubRevisionTfsClient(results);

        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
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
                    ChangeType = RelationChangeType.Added,
                    RelationType = "System.LinkTypes.Hierarchy-Forward",
                    TargetWorkItemId = 101
                }
            }
        };

        var results = new[]
        {
            new ReportingRevisionsResult
            {
                Revisions = new[] { revision },
                ContinuationToken = null
            }
        };

        var stubClient = new StubRevisionTfsClient(results);
        using var provider = BuildServiceProvider(stubClient);
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>(),
            provider.GetRequiredService<RevisionIngestionDiagnostics>(),
            provider.GetRequiredService<TfsRequestThrottler>(),
            provider.GetRequiredService<IOptionsMonitor<RevisionIngestionPersistenceOptimizationOptions>>());

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

    private static ServiceProvider BuildServiceProvider(IRevisionTfsClient revisionClient)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<RevisionIngestionDiagnosticsOptions>();
        services.AddOptions<RevisionIngestionPersistenceOptimizationOptions>();
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        services.AddSingleton(connection);
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<IRevisionTfsClient>(revisionClient);
        services.AddSingleton<IRelationRevisionHydrator, StubRelationRevisionHydrator>();
        services.AddSingleton<RevisionIngestionDiagnostics>();
        services.AddSingleton<TfsRequestThrottler>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        context.Database.EnsureCreated();
        context.Profiles.Add(new PoTool.Api.Persistence.Entities.ProfileEntity
        {
            Id = 1,
            Name = "Test Owner",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });
        context.SaveChanges();
        return provider;
    }
}
