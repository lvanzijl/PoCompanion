using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts.TfsVerification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealTfsClientVerificationTests
{
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<TfsConfigurationService> _mockConfigService = null!;
    private Mock<TfsAuthenticationProvider> _mockAuthProvider = null!;
    private Mock<ILogger<RealTfsClient>> _mockLogger = null!;
    private RealTfsClient _sut = null!;
    private TfsConfigEntity _testConfig = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockConfigService = new Mock<TfsConfigurationService>(MockBehavior.Strict);
        _mockAuthProvider = new Mock<TfsAuthenticationProvider>();
        _mockLogger = new Mock<ILogger<RealTfsClient>>();

        _testConfig = new TfsConfigEntity
        {
            Url = "https://test-tfs.example.com",
            Project = "TestProject",
            ApiVersion = "7.0",
            TimeoutSeconds = 30,
            UseDefaultCredentials = true
        };

        _mockConfigService.Setup(x => x.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testConfig);

        // Create mock IHttpClientFactory
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _sut = new RealTfsClient(
            _httpClient,
            mockFactory.Object,
            _mockConfigService.Object,
            _mockAuthProvider.Object,
            _mockLogger.Object
        );
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
        Assert.IsTrue(result.Success);
        Assert.HasCount(7, result.Checks); // 5 base + 2 new checks
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
        Assert.HasCount(8, result.Checks); // 7 read-only + 1 write check
        var writeCheck = result.Checks.FirstOrDefault(c => c.CapabilityId == "work-item-update");
        Assert.IsNotNull(writeCheck);
        Assert.AreEqual($"Work Item #{testWorkItemId}", writeCheck.TargetScope);
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_WithWriteChecksButNoWorkItemId_SkipsWriteVerification()
    {
        // Arrange
        SetupSuccessfulHttpResponses();

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(
            includeWriteChecks: true,
            workItemIdForWriteCheck: null);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IncludedWriteChecks);
        Assert.HasCount(7, result.Checks); // Only read-only checks
        Assert.IsFalse(result.Checks.Any(c => c.CapabilityId == "work-item-update"));
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
    public async Task VerifyCapabilitiesAsync_ProjectNotFound_ReturnsAuthorizationFailure()
    {
        // Arrange
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse($"_apis/projects/{_testConfig.Project}", HttpStatusCode.NotFound, "");
        SetupSuccessfulHttpResponsesExceptProjectAccess();

        // Act
        var result = await _sut.VerifyCapabilitiesAsync(includeWriteChecks: false);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Success);
        var projectCheck = result.Checks.FirstOrDefault(c => c.CapabilityId == "project-access");
        Assert.IsNotNull(projectCheck);
        Assert.IsFalse(projectCheck.Success);
        Assert.AreEqual(FailureCategory.Authorization, projectCheck.FailureCategory);
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
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}" +
            "]}");

        // Batch read
        SetupHttpResponse("_apis/wit/workitems", HttpStatusCode.OK, "{\"value\":[]}");

        // Revisions
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");

        // Pull requests (repositories)
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");

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
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}" +
            "]}");
        SetupHttpResponse("_apis/wit/workitems", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");
    }

    private void SetupSuccessfulHttpResponsesExceptProjectAccess()
    {
        SetupHttpResponse("_apis/projects", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/wit/wiql", HttpStatusCode.OK, "{\"workItems\":[]}");
        SetupHttpResponse("_apis/wit/fields", HttpStatusCode.OK,
            "{\"value\":[" +
            "{\"referenceName\":\"System.Id\"}," +
            "{\"referenceName\":\"System.Title\"}," +
            "{\"referenceName\":\"System.State\"}," +
            "{\"referenceName\":\"System.WorkItemType\"}" +
            "]}");
        SetupHttpResponse("_apis/wit/workitems", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/wit/workitems/1/revisions", HttpStatusCode.OK, "{\"value\":[]}");
        SetupHttpResponse("_apis/git/repositories", HttpStatusCode.OK, "{\"value\":[]}");
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
