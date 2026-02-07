using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

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

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase($"RevisionIngestion_{Guid.NewGuid()}"));
        services.AddSingleton<IRevisionTfsClient>(stubClient);
        services.AddSingleton<IRelationRevisionHydrator, StubRelationRevisionHydrator>();

        using var provider = services.BuildServiceProvider();
        var service = new RevisionIngestionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RevisionIngestionService>>());

        var result = await service.IngestRevisionsAsync(1, cancellationToken: CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, stubClient.ReportingCalls);
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
}
