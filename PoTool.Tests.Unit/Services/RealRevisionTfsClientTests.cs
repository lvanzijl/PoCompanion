using System.Reflection;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Exceptions;
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
    private Mock<ILogger<TfsRequestSender>> _mockRequestSenderLogger = null!;
    private TfsRequestSender _requestSender = null!;
    private Mock<IOptionsMonitor<RevisionIngestionPaginationOptions>> _mockPaginationOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockConfigService = new Mock<ITfsConfigurationService>();
        _mockLogger = new Mock<ILogger<RealRevisionTfsClient>>();
        _mockThrottlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        _throttler = new TfsRequestThrottler(_mockThrottlerLogger.Object);
        _mockRequestSenderLogger = new Mock<ILogger<TfsRequestSender>>();
        _requestSender = new TfsRequestSender(_mockRequestSenderLogger.Object);
        _mockPaginationOptions = new Mock<IOptionsMonitor<RevisionIngestionPaginationOptions>>();
        _mockPaginationOptions.Setup(options => options.CurrentValue)
            .Returns(new RevisionIngestionPaginationOptions());
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.IsFalse(
            url.Contains("$expand=", StringComparison.Ordinal),
            "URL must not contain $expand parameter when mode is None");
        Assert.IsFalse(
            url.Contains("relations", StringComparison.OrdinalIgnoreCase),
            "URL must never contain 'relations' for reporting endpoint");
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.Fields);

        // Assert
        Assert.IsTrue(
            url.Contains("$expand=fields", StringComparison.Ordinal),
            "URL should contain $expand=fields when mode is Fields");
        Assert.IsFalse(
            url.Contains("relations", StringComparison.OrdinalIgnoreCase),
            "URL should never contain 'relations' for reporting endpoint");
    }

    [TestMethod]
    public void BuildReportingRevisionsUrl_WithInvalidExpandMode_Throws()
    {
        // Arrange
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        const ReportingExpandMode InvalidExpandMode = (ReportingExpandMode)99;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        // Act
        var exception = ExpectInnerException<InvalidOperationException>(() =>
            client.TestBuildReportingRevisionsUrl(
                config,
                startDateTime: null,
                continuationToken: null,
                expandMode: InvalidExpandMode));

        var message = exception.Message;

        // Assert
        Assert.IsTrue(message.Contains("workitemrevisions", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(message.Contains("relations", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(message.Contains("only None/Fields", StringComparison.OrdinalIgnoreCase));
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        StringAssert.Contains(url, "System.ChangedBy", "URL should include System.ChangedBy in the fields list");
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

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
        Assert.IsFalse(
            urlNone.Contains("$expand=relations", StringComparison.Ordinal),
            "URL should never contain $expand=relations (None mode)");
        Assert.IsFalse(
            urlFields.Contains("$expand=relations", StringComparison.Ordinal),
            "URL should never contain $expand=relations (Fields mode)");
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var startDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var continuationToken = "some-token-123";

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: startDateTime,
            continuationToken: continuationToken,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.IsTrue(
            url.Contains("continuationToken=", StringComparison.Ordinal),
            "URL should contain continuation token");
        Assert.IsFalse(
            url.Contains("startDateTime=", StringComparison.Ordinal),
            "URL should NOT contain startDateTime when continuation token is present");
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var startDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: startDateTime,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.IsTrue(
            url.Contains("startDateTime=", StringComparison.Ordinal),
            "URL should contain startDateTime when no continuation token");
        Assert.IsFalse(
            url.Contains("continuationToken=", StringComparison.Ordinal),
            "URL should NOT contain continuation token when none provided");
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
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        // Act
        var url = client.TestBuildReportingRevisionsUrl(
            config,
            startDateTime: null,
            continuationToken: null,
            expandMode: ReportingExpandMode.None);

        // Assert
        Assert.IsFalse(
            url.Contains("startDateTime=", StringComparison.Ordinal),
            "URL should NOT contain startDateTime when not provided");
        Assert.IsFalse(
            url.Contains("continuationToken=", StringComparison.Ordinal),
            "URL should NOT contain continuation token when not provided");
        Assert.IsTrue(
            url.Contains("api-version=", StringComparison.Ordinal),
            "URL should always contain api-version");
    }

    [TestMethod]
    public void ReportingRevisionsResult_IsCompleteTrue_WhenContinuationTokenMissing()
    {
        var result = new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null);

        Assert.IsTrue(result.IsComplete);
    }

    [TestMethod]
    public void ReportingRevisionsResult_IsCompleteFalse_WhenContinuationTokenPresent()
    {
        var result = new ReportingRevisionsResult(new[] { CreateMinimalRevision() }, "token");

        Assert.IsFalse(result.IsComplete);
    }

    [TestMethod]
    public void ReportingRevisionsResult_IsCompleteTrue_WhenRevisionsPresentAndNoToken()
    {
        var result = new ReportingRevisionsResult(new[] { CreateMinimalRevision() }, null);

        Assert.IsTrue(result.IsComplete);
    }

    [TestMethod]
    public void ReportingRevisionsResult_HasMoreResultsFalse_WhenContinuationTokenMissing()
    {
        var result = new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), null);

        Assert.IsFalse(result.HasMoreResults);
    }

    [TestMethod]
    public void ReportingRevisionsResult_HasMoreResultsTrue_WhenContinuationTokenPresent()
    {
        var result = new ReportingRevisionsResult(new[] { CreateMinimalRevision() }, "token");

        Assert.IsTrue(result.HasMoreResults);
    }

    [TestMethod]
    public void ReportingRevisionsResult_AllowsEmptyPageWithContinuationToken()
    {
        var result = new ReportingRevisionsResult(Array.Empty<WorkItemRevision>(), "token");

        Assert.IsFalse(result.IsComplete);
        Assert.IsTrue(result.HasMoreResults);
        CollectionAssert.AreEqual(Array.Empty<WorkItemRevision>(), (WorkItemRevision[])result.Revisions);
    }

    [TestMethod]
    public void ReportingRevisionsResult_ThrowsWhenTerminationHasContinuationToken()
    {
        var termination = new ReportingRevisionsTermination(
            ReportingRevisionsTerminationReason.MaxTotalPages,
            "Pagination stopped");

        var exception = CaptureException<InvalidOperationException>(() =>
            new ReportingRevisionsResult(new[] { CreateMinimalRevision() }, "token", termination));

        StringAssert.Contains(exception.Message, "terminated result", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ExtractContinuationToken_WhenHeaderIsWhitespace_ReturnsNull()
    {
        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        foreach (var headerValue in new[] { string.Empty, "   " })
        {
            var response = new HttpResponseMessage();
            response.Headers.TryAddWithoutValidation("x-ms-continuationtoken", headerValue);

            var token = client.TestExtractContinuationToken(response);

            Assert.IsNull(token);
        }
    }

    [TestMethod]
    public void ExtractContinuationToken_WhenHeaderMissing_ReturnsNull()
    {
        var response = new HttpResponseMessage();

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var token = client.TestExtractContinuationToken(response);

        Assert.IsNull(token);
    }

    [TestMethod]
    public void BuildWorkItemRevisionsUrl_IncludesExpandRelations()
    {
        var config = new TfsConfigEntity
        {
            Url = "https://tfs.example.com/DefaultCollection",
            ApiVersion = "6.0"
        };

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var url = client.TestBuildWorkItemRevisionsUrl(config, 123);

        Assert.IsTrue(url.Contains("$expand=relations", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithValueArray_ReturnsRevisions()
    {
        var json = """
            {
              "value": [
                {
                  "id": 1,
                  "rev": 2,
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z"
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(1, revisions);
        Assert.AreEqual(1, revisions[0].WorkItemId);
        Assert.AreEqual(2, revisions[0].RevisionNumber);
        Assert.AreEqual("Task", revisions[0].WorkItemType);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithValuesArray_ReturnsRevisions()
    {
        var json = """
            {
              "values": [
                {
                  "id": 1,
                  "rev": 2,
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z"
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(1, revisions);
        Assert.AreEqual(1, revisions[0].WorkItemId);
        Assert.AreEqual(2, revisions[0].RevisionNumber);
        Assert.AreEqual("Task", revisions[0].WorkItemType);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithStringIntegers_ParsesIntFields()
    {
        var json = """
            {
              "value": [
                {
                  "id": "42",
                  "rev": "3",
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z",
                    "Microsoft.VSTS.Scheduling.Effort": "5"
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(1, revisions);
        Assert.AreEqual(42, revisions[0].WorkItemId);
        Assert.AreEqual(3, revisions[0].RevisionNumber);
        Assert.AreEqual(5d, revisions[0].Effort);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithDecimalEffortNumber_ParsesDouble()
    {
        var json = """
            {
              "value": [
                {
                  "id": 10,
                  "rev": 4,
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z",
                    "Microsoft.VSTS.Scheduling.Effort": 1.25
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(1, revisions);
        Assert.AreEqual(1.25d, revisions[0].Effort);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithDecimalEffortString_ParsesDouble()
    {
        var json = """
            {
              "value": [
                {
                  "id": 10,
                  "rev": 4,
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z",
                    "Microsoft.VSTS.Scheduling.Effort": "0.5"
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(1, revisions);
        Assert.AreEqual(0.5d, revisions[0].Effort);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithInvalidRequiredInt_SkipsRevision()
    {
        var json = """
            {
              "value": [
                {
                  "id": "not-a-number",
                  "rev": 1,
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z"
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(0, revisions);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WithInvalidOptionalInt_ReturnsNullEffort()
    {
        var json = """
            {
              "value": [
                {
                  "id": 1,
                  "rev": 1,
                  "fields": {
                    "System.WorkItemType": "Task",
                    "System.Title": "Sample",
                    "System.State": "New",
                    "System.IterationPath": "Project\\Sprint 1",
                    "System.AreaPath": "Project",
                    "System.ChangedDate": "2024-01-01T00:00:00Z",
                    "Microsoft.VSTS.Scheduling.Effort": { "value": "invalid" }
                  }
                }
              ]
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var revisions = client.TestParseReportingRevisionsPayload(json);

        Assert.HasCount(1, revisions);
        Assert.IsNull(revisions[0].Effort);
    }

    [TestMethod]
    public void ParseReportingRevisionsPayload_WhenMissingValueArray_ThrowsWithTruncatedPayloadMarker()
    {
        var largePayload = new string('x', 2100);
        var json = $"{{\"payload\":\"{largePayload}\"}}";

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var tfsException = ExpectInnerException<TfsException>(() =>
            _ = client.TestParseReportingRevisionsPayload(json));

        Assert.IsTrue(tfsException.ErrorContent?.Contains("... (truncated)", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [TestMethod]
    public void ParseWorkItemRevisionFromPerItem_WhenRelationsMissing_Throws()
    {
        var json = """
            {
              "id": 1,
              "rev": 1,
              "fields": {
                "System.WorkItemType": "Task",
                "System.Title": "Sample",
                "System.State": "New",
                "System.IterationPath": "Project\\Sprint 1",
                "System.AreaPath": "Project",
                "System.ChangedDate": "2024-01-01T00:00:00Z"
              }
            }
            """;

        var client = new TestableRealRevisionTfsClient(
            _mockHttpClientFactory.Object,
            _mockConfigService.Object,
            _mockLogger.Object,
            _throttler,
            _requestSender, _mockPaginationOptions.Object);

        var tfsException = ExpectInnerException<TfsException>(() =>
            client.TestParseWorkItemRevisionFromPerItem(json, 1));

        Assert.IsTrue(tfsException.Message.Contains("relations", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(tfsException.Message.Contains("$expand=relations", StringComparison.OrdinalIgnoreCase));
    }

    private static TException ExpectInnerException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is TException innerException)
        {
            return innerException;
        }

        throw new AssertFailedException($"Expected {typeof(TException).Name} was not thrown");
    }

    private static TException CaptureException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new AssertFailedException($"Expected {typeof(TException).Name} was not thrown");
    }

    private static WorkItemRevision CreateMinimalRevision()
    {
        return new WorkItemRevision
        {
            WorkItemId = 1,
            RevisionNumber = 1,
            WorkItemType = "Bug",
            Title = "Test",
            State = "New",
            IterationPath = "Iteration 1",
            AreaPath = "Area 1",
            ChangedDate = DateTimeOffset.UtcNow
        };
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
            TfsRequestThrottler throttler,
            TfsRequestSender requestSender,
            IOptionsMonitor<RevisionIngestionPaginationOptions> paginationOptions)
            : base(httpClientFactory, configService, logger, throttler, requestSender, paginationOptions)
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

        public string TestBuildWorkItemRevisionsUrl(TfsConfigEntity config, int workItemId)
        {
            var method = typeof(RealRevisionTfsClient).GetMethod(
                "BuildWorkItemRevisionsUrl",
                BindingFlags.NonPublic | BindingFlags.Instance);

            return (string)method!.Invoke(this, new object?[] { config, workItemId })!;
        }

        public IReadOnlyList<WorkItemRevision> TestParseReportingRevisionsPayload(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var method = typeof(RealRevisionTfsClient).GetMethod(
                "ParseReportingRevisionsPayload",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var parseResult = method!.Invoke(this, new object?[] { doc });
            var revisionsProperty = parseResult!.GetType().GetProperty("Revisions");
            return (IReadOnlyList<WorkItemRevision>)revisionsProperty!.GetValue(parseResult)!;
        }

        public string? TestExtractContinuationToken(HttpResponseMessage response)
        {
            var method = typeof(RealRevisionTfsClient).GetMethod(
                "ExtractContinuationToken",
                BindingFlags.NonPublic | BindingFlags.Static);

            return (string?)method!.Invoke(null, new object?[] { response });
        }

        public void TestParseWorkItemRevisionFromPerItem(string json, int workItemId)
        {
            using var doc = JsonDocument.Parse(json);
            var method = typeof(RealRevisionTfsClient).GetMethod(
                "ParseWorkItemRevisionFromPerItem",
                BindingFlags.NonPublic | BindingFlags.Instance);

            _ = method!.Invoke(this, new object?[] { doc.RootElement, workItemId, null, null });
        }
    }
}
