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
    public async Task VerifyCapabilitiesAsync_MissingAnalyticsField_FailsWorkItemFieldVerification()
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
        StringAssert.Contains(workItemFieldsCheck.ObservedBehavior ?? string.Empty, "Rhodium.Funding.ProjectElement");
    }

    [TestMethod]
    public async Task ValidateConnectionAsync_WhenAnalyticsMetadataEndpointFails_ReturnsFalse()
    {
        // Arrange
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[{\"name\":\"TestProject\"}]}");
        SetupHttpResponse($"_apis/projects/{_testConfig.Project}", HttpStatusCode.OK, $"{{\"name\":\"{_testConfig.Project}\"}}");
        SetupHttpResponse("_odata/v3.0-preview/$metadata", HttpStatusCode.NotFound, "Not Found");

        // Act
        var isValid = await _sut.ValidateConnectionAsync();

        // Assert
        Assert.IsFalse(isValid);
    }

    private void SetupSuccessfulHttpResponses()
    {
        // Server reachability
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[{\"name\":\"TestProject\"}]}");

        // Project access
        SetupHttpResponse($"_apis/projects/{_testConfig.Project}", HttpStatusCode.OK,
            $"{{\"name\":\"{_testConfig.Project}\"}}");

        // WIQL query
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[]}");

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

        // Batch read
        SetupHttpResponse("_apis/wit/workitems", HttpStatusCode.OK, "{\"value\":[]}");

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
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[]}");
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
        SetupHttpResponse("_apis/wit/workitems", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/build/definitions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/release/definitions", HttpStatusCode.OK, "{\"value\":[]}");
    }

    private void SetupSuccessfulHttpResponsesExceptProjectAccess()
    {
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[]}");
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
        SetupHttpResponse("_apis/wit/workitems", HttpStatusCode.OK, "{\"value\":[]}");
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
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
