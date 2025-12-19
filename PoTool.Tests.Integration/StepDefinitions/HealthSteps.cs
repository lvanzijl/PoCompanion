using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class HealthSteps
{
    private readonly SharedTestState _context;
    
    private HealthResponse? _healthResponse;

    public HealthSteps(SharedTestState context)
    {
        _context = context;
    }

    [When(@"I request the health endpoint")]
    public async Task WhenIRequestTheHealthEndpoint()
    {
        _context.Response = await _context.Client.GetAsync("/health");
        
        if (_context.Response.StatusCode == HttpStatusCode.OK)
        {
            _healthResponse = await _context.Response.Content.ReadFromJsonAsync<HealthResponse>();
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
