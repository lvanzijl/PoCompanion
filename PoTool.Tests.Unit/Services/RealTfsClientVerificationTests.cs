using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;
using PoTool.Core.Contracts;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealTfsClientVerificationTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private PoToolDbContext _dbContext = null!;
    private TfsConfigurationService _configService = null!;
    private Mock<ILogger<RealTfsClient>> _mockLogger = null!;
    private RealTfsClient _sut = null!;
    private TfsConfigEntity _testConfig = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger<RealTfsClient>>();

        // Create in-memory database for config service
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new PoToolDbContext(options);

        // Create real config service (no longer using mock)
        var configLogger = new Mock<ILogger<TfsConfigurationService>>();
        var gateLogger = new Mock<ILogger<EfConcurrencyGate>>();
        var gate = new EfConcurrencyGate(gateLogger.Object); // Use real implementation instead of mock
        _configService = new TfsConfigurationService(_dbContext, configLogger.Object, gate);

        _testConfig = new TfsConfigEntity
        {
            Url = "https://test-tfs.example.com",
            Project = "TestProject",
            ApiVersion = "7.0",
            TimeoutSeconds = 30,
            UseDefaultCredentials = true
        };

        // Save test config to database
        await _configService.SaveConfigAsync(
            _testConfig.Url,
            _testConfig.Project,
            "TestProject\\Area",
            _testConfig.UseDefaultCredentials,
            _testConfig.TimeoutSeconds,
            _testConfig.ApiVersion);

        // Create throttler (use real implementation for tests)
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object, readConcurrency: 10, writeConcurrency: 10);

        // Create request sender (use real implementation for tests)
        var senderLogger = new Mock<ILogger<TfsRequestSender>>();
        var requestSender = new TfsRequestSender(senderLogger.Object);

        // Create mock IHttpClientFactory
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _sut = new RealTfsClient(
            mockFactory.Object,
            _configService,
            _mockLogger.Object,
            throttler,
            requestSender
        );
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_AllChecksPass_ReturnsSuccessReport()
    {
        // Arrange
        SetupSuccessfulHttpResponses();

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success, $"Verification should succeed. Failed checks: {string.Join(", ", result.Checks.Where(c => !c.Success).Select(c => c.CapabilityId))}");
        Assert.HasCount(9, result.Checks); // 9 read-only checks (server, project, query, hierarchy, fields, batch, revisions, PRs, pipelines)
        Assert.IsTrue(result.Checks.All(c => c.Success));
        Assert.AreEqual(_testConfig.Url, result.ServerUrl);
        Assert.AreEqual(_testConfig.Project, result.ProjectName);
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_ServerUnreachable_ReturnsFailureReport()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Checks.Any(c => !c.Success));
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_WithWriteChecks_IncludesWriteVerification()
    {
        // Arrange
        SetupSuccessfulHttpResponses();
        const int testWorkItemId = 12345;

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(
            includeWriteChecks: true,
            workItemIdForWriteCheck: testWorkItemId);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IncludedWriteChecks);
        Assert.HasCount(10, result.Checks); // 9 read-only + 1 write check
        var writeCheck = result.Checks.FirstOrDefault(c => c.CapabilityId == "work-item-update");
        Assert.IsNotNull(writeCheck);
        Assert.AreEqual($"Work Item #{testWorkItemId}", writeCheck.TargetScope);
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_AuthenticationFailure_ReturnsAuthFailureCategory()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("_apis/projects")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Unauthorized")
            });

        // Setup other endpoints to succeed
        SetupSuccessfulHttpResponsesExceptProjects();

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Success);
        var serverCheck = result.Checks.FirstOrDefault(c => c.CapabilityId == "server-reachability");
        Assert.IsNotNull(serverCheck);
        Assert.IsFalse(serverCheck.Success);
        Assert.AreEqual(FailureCategory.Authentication, serverCheck.FailureCategory);
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_IncludesAllCapabilityIds()
    {
        // Arrange
        SetupSuccessfulHttpResponses();

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        // Assert
        var expectedCapabilities = new[]
        {
            "server-reachability",
            "project-access",
            "work-item-query",
            "work-item-fields",
            "batch-read",
            "work-item-revisions",
            "pull-requests"
        };

        foreach (var capability in expectedCapabilities)
        {
            Assert.IsTrue(
                result.Checks.Any(c => c.CapabilityId == capability),
                $"Missing capability check: {capability}");
        }
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_FailedChecksIncludeResolutionGuidance()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("_apis/projects")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Unauthorized")
            });

        SetupSuccessfulHttpResponsesExceptProjects();

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        // Assert
        var failedCheck = result.Checks.FirstOrDefault(c => !c.Success);
        Assert.IsNotNull(failedCheck);
        Assert.IsNotNull(failedCheck.LikelyCauses);
        Assert.IsNotEmpty(failedCheck.LikelyCauses);
        Assert.IsNotNull(failedCheck.ResolutionGuidance);
        Assert.IsNotEmpty(failedCheck.ResolutionGuidance);
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_PayloadMissingAnalyticsField_DoesNotFailWorkItemFieldVerification()
    {
        SetupSuccessfulHttpResponses();
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":101,\"fields\":{" +
            "\"System.WorkItemType\":\"Epic\"," +
            "\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\"," +
            "\"Microsoft.VSTS.Common.TimeCriticality\":42.5" +
            "}}" +
            "]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsTrue(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "Rhodium.Funding.ProjectElement: metadata=found");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "payload=present=0, absent=1, empty=0");
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_MetadataMissingAnalyticsField_FailsWorkItemFieldVerification()
    {
        SetupSuccessfulHttpResponses();
        SetupHttpResponse("_apis/wit/fields", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"referenceName\":\"System.Id\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}," +
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.AreaPath\"}," +
            "{\"referenceName\":\"System.IterationPath\"}," +
            "{\"referenceName\":\"System.Description\"}," +
            "{\"referenceName\":\"System.CreatedDate\"}," +
            "{\"referenceName\":\"System.ChangedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.ClosedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.Severity\"}," +
            "{\"referenceName\":\"System.Tags\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BusinessValue\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.Effort\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.StoryPoints\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BacklogPriority\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.TimeCriticality\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectNumber\"}" +
            "]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsFalse(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsFalse(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "Metadata validation failed");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "Rhodium.Funding.ProjectElement: metadata=not found");
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_NullOnlySampleValues_LogsWarningButSucceeds()
    {
        SetupSuccessfulHttpResponses();
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":101,\"fields\":{" +
            "\"System.WorkItemType\":\"Epic\"," +
            "\"Rhodium.Funding.ProjectNumber\":null," +
            "\"Rhodium.Funding.ProjectElement\":\"\"," +
            "\"Microsoft.VSTS.Common.TimeCriticality\":null" +
            "}}" +
            "]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsTrue(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "payload=present=0, absent=0, empty=1");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "sampled values are empty");
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_RecentSamplingIncludesMultipleWorkItemsInDiagnostics()
    {
        SetupSuccessfulHttpResponses();
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK,
            "{\"workItems\":[{\"id\":105},{\"id\":104},{\"id\":103},{\"id\":102},{\"id\":101}]}");
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":105,\"fields\":{\"System.WorkItemType\":\"Feature\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-5\",\"Rhodium.Funding.ProjectElement\":\"ELM-5\",\"Microsoft.VSTS.Common.TimeCriticality\":10}}," +
            "{\"id\":104,\"fields\":{\"System.WorkItemType\":\"Epic\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-4\",\"Rhodium.Funding.ProjectElement\":\"ELM-4\",\"Microsoft.VSTS.Common.TimeCriticality\":20}}," +
            "{\"id\":103,\"fields\":{\"System.WorkItemType\":\"Bug\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-3\",\"Rhodium.Funding.ProjectElement\":\"ELM-3\",\"Microsoft.VSTS.Common.TimeCriticality\":30}}," +
            "{\"id\":102,\"fields\":{\"System.WorkItemType\":\"PBI\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-2\",\"Rhodium.Funding.ProjectElement\":\"ELM-2\",\"Microsoft.VSTS.Common.TimeCriticality\":40}}," +
            "{\"id\":101,\"fields\":{\"System.WorkItemType\":\"Task\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\",\"Rhodium.Funding.ProjectElement\":\"ELM-1\",\"Microsoft.VSTS.Common.TimeCriticality\":50}}" +
            "]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsTrue(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "105 (Feature)");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "101 (Task)");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "validated 5 work items");
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_NoRecentSampleItems_ReturnsDiagnosticResultWithoutFailure()
    {
        SetupSuccessfulHttpResponses();
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsTrue(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "No recent non-Removed work items were available for payload sampling");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "payload=no sampled items");
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_AreaScopedSamplingFallback_UsesProjectWideRecentSample()
    {
        SetupSuccessfulHttpResponses();

        _mockHttpMessageHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("_apis/wit/wiql")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"workItems\":[{\"id\":101}]}")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"workItems\":[{\"id\":101}]}")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"workItems\":[]}")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"workItems\":[{\"id\":105},{\"id\":104},{\"id\":103}]}")
            });

        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":105,\"fields\":{\"System.WorkItemType\":\"Feature\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-5\",\"Rhodium.Funding.ProjectElement\":\"ELM-5\",\"Microsoft.VSTS.Common.TimeCriticality\":10}}," +
            "{\"id\":104,\"fields\":{\"System.WorkItemType\":\"Epic\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-4\",\"Rhodium.Funding.ProjectElement\":\"ELM-4\",\"Microsoft.VSTS.Common.TimeCriticality\":20}}," +
            "{\"id\":103,\"fields\":{\"System.WorkItemType\":\"Bug\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-3\",\"Rhodium.Funding.ProjectElement\":\"ELM-3\",\"Microsoft.VSTS.Common.TimeCriticality\":30}}" +
            "]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsTrue(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "project-wide fallback sampling was used");
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "105 (Feature)");
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_PayloadShapeFallback_DoesNotChangeValidationSemantics()
    {
        SetupSuccessfulHttpResponses();
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"workItems\":[" +
            "{\"id\":101,\"fields\":{\"System.WorkItemType\":\"Feature\",\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\",\"Rhodium.Funding.ProjectElement\":\"ELM-1\",\"Microsoft.VSTS.Common.TimeCriticality\":42.5}}" +
            "]}");

        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success);
        var workItemFieldsCheck = result.Checks.Single(check => check.CapabilityId == "work-item-fields");
        Assert.IsTrue(workItemFieldsCheck.Success);
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "101 (Feature)");
    }

    private void SetupSuccessfulHttpResponses()
    {
        // Server reachability
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[{\"name\":\"TestProject\"}]}");

        // Project access
        SetupHttpResponse($"_apis/projects/{_testConfig.Project}", HttpStatusCode.OK,
            $"{{\"name\":\"{_testConfig.Project}\"}}");

        // WIQL query
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[{\"id\":101}]}");

        // Work item fields
        SetupHttpResponse("_apis/wit/fields", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"referenceName\":\"System.Id\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}," +
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.AreaPath\"}," +
            "{\"referenceName\":\"System.IterationPath\"}," +
            "{\"referenceName\":\"System.Description\"}," +
            "{\"referenceName\":\"System.CreatedDate\"}," +
            "{\"referenceName\":\"System.ChangedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.ClosedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.Severity\"}," +
            "{\"referenceName\":\"System.Tags\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BusinessValue\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.Effort\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.StoryPoints\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BacklogPriority\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.TimeCriticality\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectNumber\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectElement\"}" +
            "]}");

        // Batch read and sample payload validation
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":101,\"fields\":{" +
            "\"System.WorkItemType\":\"Feature\"," +
            "\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\"," +
            "\"Rhodium.Funding.ProjectElement\":\"ELM-1\"," +
            "\"Microsoft.VSTS.Common.TimeCriticality\":42.5" +
            "}}" +
            "]}");

        // Revisions
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");

        // Pull requests (repositories)
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");

        // Pipelines
        SetupHttpResponse("_apis/build/definitions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/release/definitions", HttpStatusCode.OK, "{\"value\":[]}");

        // Work item get for write check
        SetupHttpResponse("_apis/wit/workitems/", HttpStatusCode.OK, "{\"id\":12345}");
    }

    private void SetupSuccessfulHttpResponsesExceptProjects()
    {
        SetupHttpResponse($"_apis/projects/{_testConfig.Project}", HttpStatusCode.OK,
            $"{{\"name\":\"{_testConfig.Project}\"}}");
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[{\"id\":101}]}");
        SetupHttpResponse("_apis/wit/fields", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"referenceName\":\"System.Id\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}," +
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.AreaPath\"}," +
            "{\"referenceName\":\"System.IterationPath\"}," +
            "{\"referenceName\":\"System.Description\"}," +
            "{\"referenceName\":\"System.CreatedDate\"}," +
            "{\"referenceName\":\"System.ChangedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.ClosedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.Severity\"}," +
            "{\"referenceName\":\"System.Tags\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BusinessValue\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.Effort\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.StoryPoints\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BacklogPriority\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.TimeCriticality\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectNumber\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectElement\"}" +
            "]}");
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":101,\"fields\":{" +
            "\"System.WorkItemType\":\"Feature\"," +
            "\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\"," +
            "\"Rhodium.Funding.ProjectElement\":\"ELM-1\"," +
            "\"Microsoft.VSTS.Common.TimeCriticality\":42.5" +
            "}}" +
            "]}");
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/build/definitions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/release/definitions", HttpStatusCode.OK, "{\"value\":[]}");
    }

    private void SetupSuccessfulHttpResponsesExceptProjectAccess()
    {
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[{\"id\":101}]}");
        SetupHttpResponse("_apis/wit/fields", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"referenceName\":\"System.Id\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}," +
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.AreaPath\"}," +
            "{\"referenceName\":\"System.IterationPath\"}," +
            "{\"referenceName\":\"System.Description\"}," +
            "{\"referenceName\":\"System.CreatedDate\"}," +
            "{\"referenceName\":\"System.ChangedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.ClosedDate\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.Severity\"}," +
            "{\"referenceName\":\"System.Tags\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BusinessValue\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.Effort\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Scheduling.StoryPoints\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.BacklogPriority\"}," +
            "{\"referenceName\":\"Microsoft.VSTS.Common.TimeCriticality\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectNumber\"}," +
            "{\"referenceName\":\"Rhodium.Funding.ProjectElement\"}" +
            "]}");
        SetupHttpResponse("_apis/wit/workitemsbatch", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"id\":101,\"fields\":{" +
            "\"System.WorkItemType\":\"Feature\"," +
            "\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\"," +
            "\"Rhodium.Funding.ProjectElement\":\"ELM-1\"," +
            "\"Microsoft.VSTS.Common.TimeCriticality\":42.5" +
            "}}" +
            "]}");
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/build/definitions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/release/definitions", HttpStatusCode.OK, "{\"value\":[]}");
    }

    private void SetupHttpResponse(string uriContains, HttpStatusCode statusCode, string content)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(uriContains)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
