using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Tests.Integration.Support;
using Reqnroll;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class HealthSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private HealthResponse? _healthResponse;

    public HealthSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [When(@"I request the health endpoint")]
    public async Task WhenIRequestTheHealthEndpoint()
    {
        _response = await _client.GetAsync("/health");
        _scenarioContext["Response"] = _response;
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _healthResponse = await _response.Content.ReadFromJsonAsync<HealthResponse>();
        }
    }

    [Then(@"the health status should be ""(.*)""")]
    public void ThenTheHealthStatusShouldBe(string expectedStatus)
    {
        Assert.IsNotNull(_healthResponse);
        Assert.AreEqual(expectedStatus, _healthResponse.Status);
    }

    [Then(@"the response should include a timestamp")]
    public void ThenTheResponseShouldIncludeATimestamp()
    {
        Assert.IsNotNull(_healthResponse);
        Assert.IsTrue(_healthResponse.Timestamp > DateTime.MinValue, 
            "Health response should include a valid timestamp");
    }

    private class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
