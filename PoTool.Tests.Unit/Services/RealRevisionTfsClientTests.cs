using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit;

/// <summary>
/// Tests for RealRevisionTfsClient, specifically focusing on URL building
/// and parameter validation for the reporting revisions endpoint.
/// </summary>
[TestClass]
public sealed class RealRevisionTfsClientTests
{
    private Mock<IHttpClientFactory> _mockHttpClientFactory = null!;
    private Mock<ITfsConfigurationService> _mockConfigService = null!;
    private Mock<ILogger<RealRevisionTfsClient>> _mockLogger = null!;
    private Mock<ILogger<TfsRequestThrottler>> _mockThrottlerLogger = null!;
    private TfsRequestThrottler _throttler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockConfigService = new Mock<ITfsConfigurationService>();
        _mockLogger = new Mock<ILogger<RealRevisionTfsClient>>();
        _mockThrottlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        _throttler = new TfsRequestThrottler(_mockThrottlerLogger.Object);
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_WithExpandModeNone_DoesNotIncludeExpand()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.DoesNotContain("$expand=", url, "URL must not contain $expand parameter when mode is None");
        Assert.DoesNotContain("relations", url, "URL must never contain 'relations' for reporting endpoint");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_WithExpandModeFields_IncludesExpandFields()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.Fields);

        // Assert
        Assert.Contains("$expand=fields", url, "URL should contain $expand=fields when mode is Fields");
        Assert.DoesNotContain("relations", url, "URL should never contain 'relations' for reporting endpoint");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_IncludesChangedByField()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.Contains("System.ChangedBy", url, "URL should request System.ChangedBy to avoid field omissions");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_NeverIncludesExpandRelations()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        // Act - Test all expand modes
        var urlNone = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        var urlFields = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.Fields);

        // Assert
        Assert.DoesNotContain("$expand=relations", urlNone, "URL should never contain $expand=relations (None mode)");
        Assert.DoesNotContain("$expand=relations", urlFields, "URL should never contain $expand=relations (Fields mode)");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_WithContinuationToken_ExcludesStartDateTime()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        var startDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var continuationToken = "some-token-123";

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: startDateTime,
            continuationToken: continuationToken,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.Contains("continuationToken=", url, "URL should contain continuation token");
        Assert.DoesNotContain("startDateTime=", url, "URL should NOT contain startDateTime when continuation token is present");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_WithoutContinuationToken_IncludesStartDateTime()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        var startDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: startDateTime,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.Contains("startDateTime=", url, "URL should contain startDateTime when no continuation token");
        Assert.DoesNotContain("continuationToken=", url, "URL should NOT contain continuation token when none provided");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_WithNeitherStartDateTimeNorContinuationToken_IsValid()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.DoesNotContain("startDateTime=", url, "URL should NOT contain startDateTime when not provided");
        Assert.DoesNotContain("continuationToken=", url, "URL should NOT contain continuation token when not provided");
        Assert.Contains("api-version=", url, "URL should always contain api-version");
    }

    /// <summary>
    /// Testable subclass that exposes the private BuildReportingRevisionsUrl method for testing.
    /// </summary>
    private class TestableRealRevisionTfsClient : RealRevisionTfsClient
    {
        public TestableRealRevisionTfsClient(
            IHttpClientFactory httpClientFactory,
            ITfsConfigurationService configService,
            ILogger<RealRevisionTfsClient> logger,
            TfsRequestThrottler throttler)
            : base(httpClientFactory, configService, logger, throttler)
        {
        }

        public string TestBuildReportingRevisionsUrl(
            TfsConfigEntity config,
            DateTimeOffset? startDateTime,
            string? continuationToken,
            ReportingExpandMode expandMode)
        {
            // Use reflection to call the private method
            var method = typeof(RealRevisionTfsClient).GetMethod(
                "BuildReportingRevisionsUrl",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (string)method!.Invoke(this, new object?[] { config, startDateTime, continuationToken, expandMode })!;
        }
    }
}
