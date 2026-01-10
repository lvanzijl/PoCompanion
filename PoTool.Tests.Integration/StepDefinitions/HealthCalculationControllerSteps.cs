using System.Net;
using System.Net.Http.Json;
using PoTool.Client.ApiClient;
using PoTool.Tests.Integration.Support;
using Reqnroll;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class HealthCalculationControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private CalculateHealthScoreResponse? _healthScoreResponse;

    public HealthCalculationControllerSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [When(@"I request health score calculation with")]
    public async Task WhenIRequestHealthScoreCalculationWith(Table table)
    {
        var row = table.Rows[0];

        var request = new CalculateHealthScoreRequest
        {
            TotalWorkItems = int.Parse(row["TotalWorkItems"]),
            WorkItemsWithoutEffort = int.Parse(row["WorkItemsWithoutEffort"]),
            WorkItemsInProgressWithoutEffort = int.Parse(row["WorkItemsInProgressWithoutEffort"]),
            ParentProgressIssues = int.Parse(row["ParentProgressIssues"]),
            BlockedItems = int.Parse(row["BlockedItems"])
        };

        _response = await _client.PostAsJsonAsync("/api/healthcalculation/calculate-score", request);

        if (_response.IsSuccessStatusCode)
        {
            _healthScoreResponse = await _response.Content.ReadFromJsonAsync<CalculateHealthScoreResponse>();
        }
    }

    [Then(@"the health calculation response should be OK")]
    public void ThenTheHealthCalculationResponseShouldBeOK()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
    }

    [Then(@"the health score should be (.*)")]
    public void ThenTheHealthScoreShouldBe(int expectedScore)
    {
        Assert.IsNotNull(_healthScoreResponse);
        Assert.AreEqual(expectedScore, _healthScoreResponse.HealthScore);
    }

    [Then(@"the health score should be greater than or equal to (.*)")]
    public void ThenTheHealthScoreShouldBeGreaterThanOrEqualTo(int minScore)
    {
        Assert.IsNotNull(_healthScoreResponse);
        Assert.IsTrue(_healthScoreResponse.HealthScore >= minScore,
            $"Health score {_healthScoreResponse.HealthScore} is less than {minScore}");
    }
}
