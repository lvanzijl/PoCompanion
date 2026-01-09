using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class ErrorScenarioSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private List<HttpResponseMessage> _responses = new();
    private Stopwatch _stopwatch = new();

    public ErrorScenarioSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [When(@"I request an endpoint without authentication")]
    public async Task WhenIRequestAnEndpointWithoutAuthentication()
    {
        // Remove any authentication headers
        _client.DefaultRequestHeaders.Authorization = null;
        _response = await _client.GetAsync("/api/workitems");
        _scenarioContext["Response"] = _response;
    }

    [When(@"I request a non-existent work item with ID (.*)")]
    public async Task WhenIRequestANonExistentWorkItemWithID(int id)
    {
        _response = await _client.GetAsync($"/api/workitems/{id}");
        _scenarioContext["Response"] = _response;
    }

    [When(@"I send a malformed request to create a work item")]
    public async Task WhenISendAMalformedRequestToCreateAWorkItem()
    {
        var malformedJson = "{ invalid json }";
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");
        _response = await _client.PostAsync("/api/workitems", content);
        _scenarioContext["Response"] = _response;
    }

    [When(@"I send (.*) concurrent requests to get work items")]
    public async Task WhenISendConcurrentRequestsToGetWorkItems(int count)
    {
        _stopwatch.Start();
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < count; i++)
        {
            tasks.Add(_client.GetAsync("/api/workitems"));
        }

        _responses = (await Task.WhenAll(tasks)).ToList();
        _stopwatch.Stop();
        
        _scenarioContext["Responses"] = _responses;
        _scenarioContext["Elapsed"] = _stopwatch.Elapsed;
    }

    [When(@"I send a TFS configuration without URL")]
    public async Task WhenISendATFSConfigurationWithoutURL()
    {
        var config = new
        {
            ProjectName = "TestProject",
            // URL is missing
            PersonalAccessToken = "test-token"
        };

        _response = await _client.PostAsJsonAsync("/api/tfs-config", config);
        _scenarioContext["Response"] = _response;
    }

    [When(@"I send a PUT request to a GET-only endpoint")]
    public async Task WhenISendAPUTRequestToAGETOnlyEndpoint()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        _response = await _client.PutAsync("/health", content);
        _scenarioContext["Response"] = _response;
    }

    [When(@"I request work items with a large result set")]
    public async Task WhenIRequestWorkItemsWithALargeResultSet()
    {
        // Request without filters to get all work items
        _response = await _client.GetAsync("/api/workitems");
        _scenarioContext["Response"] = _response;
    }

    [When(@"I send a request with unsupported content type")]
    public async Task WhenISendARequestWithUnsupportedContentType()
    {
        var content = new StringContent("test data", Encoding.UTF8, "text/plain");
        _response = await _client.PostAsync("/api/workitems", content);
        _scenarioContext["Response"] = _response;
    }

    [Then(@"all responses should be OK")]
    public void ThenAllResponsesShouldBeOK()
    {
        var responses = _scenarioContext.Get<List<HttpResponseMessage>>("Responses");
        Assert.IsNotNull(responses);
        Assert.IsTrue(responses.All(r => r.StatusCode == HttpStatusCode.OK),
            "All responses should have OK status code");
    }

    [Then(@"all responses should complete within (.*) seconds")]
    public void ThenAllResponsesShouldCompleteWithinSeconds(int maxSeconds)
    {
        var elapsed = _scenarioContext.Get<TimeSpan>("Elapsed");
        Assert.IsTrue(elapsed.TotalSeconds <= maxSeconds,
            $"Requests took {elapsed.TotalSeconds:F2}s but should complete within {maxSeconds}s");
    }

    [Then(@"the error message should mention ""(.*)""")]
    public async Task ThenTheErrorMessageShouldMention(string expectedText)
    {
        var response = _scenarioContext.Get<HttpResponseMessage>("Response");
        Assert.IsNotNull(response);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains(expectedText), 
            $"Error message should mention '{expectedText}'");
    }

    [Then(@"the response should contain multiple items")]
    public async Task ThenTheResponseShouldContainMultipleItems()
    {
        var response = _scenarioContext.Get<HttpResponseMessage>("Response");
        Assert.IsNotNull(response);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        // Check that response is a non-empty JSON array
        Assert.IsTrue(content.Contains("[") && content.Contains("]"),
            "Response should be a JSON array");
        Assert.IsTrue(content.Length > 2, "Response array should not be empty");
    }
}
