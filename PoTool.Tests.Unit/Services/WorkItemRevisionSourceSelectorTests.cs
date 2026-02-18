using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemRevisionSourceSelectorTests
{
    [TestMethod]
    public async Task GetSourceAsync_ReturnsConfiguredODataSource()
    {
        var configService = new Mock<ITfsConfigurationService>();
        configService
            .Setup(service => service.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TfsConfigEntity { RevisionSource = RevisionSource.AnalyticsODataRevisions });

        var restSource = new StubRevisionSource(RevisionSource.RestReportingRevisions);
        var odataSource = new StubRevisionSource(RevisionSource.AnalyticsODataRevisions);
        var selector = new WorkItemRevisionSourceSelector(
            configService.Object,
            new Mock<IProductOwnerRevisionSourceOverrideProvider>().Object,
            new[] { restSource, odataSource },
            NullLogger<WorkItemRevisionSourceSelector>.Instance);

        var source = await selector.GetSourceAsync();

        Assert.AreEqual(RevisionSource.AnalyticsODataRevisions, source.SourceType);
    }

    [TestMethod]
    public async Task GetSourceAsync_DefaultsToRestWhenConfigMissing()
    {
        var configService = new Mock<ITfsConfigurationService>();
        configService
            .Setup(service => service.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((TfsConfigEntity?)null);

        var restSource = new StubRevisionSource(RevisionSource.RestReportingRevisions);
        var odataSource = new StubRevisionSource(RevisionSource.AnalyticsODataRevisions);
        var selector = new WorkItemRevisionSourceSelector(
            configService.Object,
            new Mock<IProductOwnerRevisionSourceOverrideProvider>().Object,
            new[] { restSource, odataSource },
            NullLogger<WorkItemRevisionSourceSelector>.Instance);

        var source = await selector.GetSourceAsync();

        Assert.AreEqual(RevisionSource.RestReportingRevisions, source.SourceType);
    }

    [TestMethod]
    public async Task GetSourceAsync_UsesProductOwnerOverride_WhenConfigured()
    {
        var configService = new Mock<ITfsConfigurationService>();
        configService
            .Setup(service => service.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TfsConfigEntity { RevisionSource = RevisionSource.RestReportingRevisions });

        var overrideProvider = new Mock<IProductOwnerRevisionSourceOverrideProvider>();
        overrideProvider
            .Setup(provider => provider.GetOverrideAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RevisionSource.AnalyticsODataRevisions);

        var restSource = new StubRevisionSource(RevisionSource.RestReportingRevisions);
        var odataSource = new StubRevisionSource(RevisionSource.AnalyticsODataRevisions);
        var selector = new WorkItemRevisionSourceSelector(
            configService.Object,
            overrideProvider.Object,
            new[] { restSource, odataSource },
            NullLogger<WorkItemRevisionSourceSelector>.Instance);

        var source = await selector.GetSourceAsync(42);

        Assert.AreEqual(RevisionSource.AnalyticsODataRevisions, source.SourceType);
    }

    private sealed class StubRevisionSource : IWorkItemRevisionSource
    {
        public StubRevisionSource(RevisionSource sourceType)
        {
            SourceType = sourceType;
        }

        public RevisionSource SourceType { get; }

        public Task<ReportingRevisionsResult> GetRevisionsAsync(
            DateTimeOffset? startDateTime = null,
            string? continuationToken = null,
            ReportingExpandMode expandMode = ReportingExpandMode.None,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null));
        }

        public Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
            int workItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<WorkItemRevision>)Array.Empty<WorkItemRevision>());
        }
    }
}
