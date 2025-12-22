using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Metrics;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class MetricsControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private SprintMetricsDto? _sprintMetrics;
    private VelocityTrendDto? _velocityTrend;
    private BacklogHealthDto? _backlogHealth;
    private MultiIterationBacklogHealthDto? _multiIterationHealth;
    private EffortDistributionDto? _effortDistribution;
    private SprintCapacityPlanDto? _capacityPlan;

    public MetricsControllerSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Given(@"work items exist for iteration ""(.*)""")]
    public async Task GivenWorkItemsExistForIteration(string iterationPath, Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        foreach (var row in table.Rows)
        {
            var workItem = new WorkItemEntity
            {
                TfsId = int.Parse(row["TfsId"]),
                Type = row["Type"],
                Title = row["Title"],
                State = row["State"],
                AreaPath = "\\TestArea",
                IterationPath = row["IterationPath"],
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.UtcNow
            };

            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]))
            {
                workItem.Effort = int.Parse(row["Effort"]);
            }

            if (row.ContainsKey("AssignedTo") && !string.IsNullOrWhiteSpace(row["AssignedTo"]))
            {
                // Note: WorkItemEntity doesn't have AssignedTo field
                // This info comes from JsonPayload in real implementation
            }

            dbContext.WorkItems.Add(workItem);
        }

        await dbContext.SaveChangesAsync();
    }

    [Given(@"work items exist for multiple iterations")]
    public async Task GivenWorkItemsExistForMultipleIterations(Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        foreach (var row in table.Rows)
        {
            var workItem = new WorkItemEntity
            {
                TfsId = int.Parse(row["TfsId"]),
                Type = row["Type"],
                Title = row["Title"],
                State = row["State"],
                IterationPath = row["IterationPath"],
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.UtcNow
            };

            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]))
            {
                workItem.Effort = int.Parse(row["Effort"]);
            }

            if (row.ContainsKey("AreaPath") && !string.IsNullOrWhiteSpace(row["AreaPath"]))
            {
                workItem.AreaPath = row["AreaPath"];
            }
            else
            {
                workItem.AreaPath = "\\TestArea";
            }

            dbContext.WorkItems.Add(workItem);
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request sprint metrics for iteration ""(.*)""")]
    public async Task WhenIRequestSprintMetricsForIteration(string iterationPath)
    {
        _response = await _client.GetAsync($"/api/metrics/sprint?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _sprintMetrics = await _response.Content.ReadFromJsonAsync<SprintMetricsDto>();
        }
    }

    [When(@"I request sprint metrics with empty iteration path")]
    public async Task WhenIRequestSprintMetricsWithEmptyIterationPath()
    {
        _response = await _client.GetAsync("/api/metrics/sprint?iterationPath=");
    }

    [When(@"I request velocity trend with default parameters")]
    public async Task WhenIRequestVelocityTrendWithDefaultParameters()
    {
        _response = await _client.GetAsync("/api/metrics/velocity");
        if (_response.IsSuccessStatusCode)
        {
            _velocityTrend = await _response.Content.ReadFromJsonAsync<VelocityTrendDto>();
        }
    }

    [When(@"I request velocity trend with maxSprints (.*)")]
    public async Task WhenIRequestVelocityTrendWithMaxSprints(int maxSprints)
    {
        _response = await _client.GetAsync($"/api/metrics/velocity?maxSprints={maxSprints}");
        if (_response.IsSuccessStatusCode)
        {
            _velocityTrend = await _response.Content.ReadFromJsonAsync<VelocityTrendDto>();
        }
    }

    [When(@"I request velocity trend with areaPath ""(.*)""")]
    public async Task WhenIRequestVelocityTrendWithAreaPath(string areaPath)
    {
        _response = await _client.GetAsync($"/api/metrics/velocity?areaPath={Uri.EscapeDataString(areaPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _velocityTrend = await _response.Content.ReadFromJsonAsync<VelocityTrendDto>();
        }
    }

    [When(@"I request backlog health for iteration ""(.*)""")]
    public async Task WhenIRequestBacklogHealthForIteration(string iterationPath)
    {
        _response = await _client.GetAsync($"/api/metrics/backlog-health?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _backlogHealth = await _response.Content.ReadFromJsonAsync<BacklogHealthDto>();
        }
    }

    [When(@"I request backlog health with empty iteration path")]
    public async Task WhenIRequestBacklogHealthWithEmptyIterationPath()
    {
        _response = await _client.GetAsync("/api/metrics/backlog-health?iterationPath=");
    }

    [When(@"I request multi-iteration backlog health")]
    public async Task WhenIRequestMultiIterationBacklogHealth()
    {
        _response = await _client.GetAsync("/api/metrics/multi-iteration-health");
        if (_response.IsSuccessStatusCode)
        {
            _multiIterationHealth = await _response.Content.ReadFromJsonAsync<MultiIterationBacklogHealthDto>();
        }
    }

    [When(@"I request multi-iteration backlog health with maxIterations (.*)")]
    public async Task WhenIRequestMultiIterationBacklogHealthWithMaxIterations(int maxIterations)
    {
        _response = await _client.GetAsync($"/api/metrics/multi-iteration-health?maxIterations={maxIterations}");
        if (_response.IsSuccessStatusCode)
        {
            _multiIterationHealth = await _response.Content.ReadFromJsonAsync<MultiIterationBacklogHealthDto>();
        }
    }

    [When(@"I request multi-iteration backlog health with areaPath ""(.*)""")]
    public async Task WhenIRequestMultiIterationBacklogHealthWithAreaPath(string areaPath)
    {
        _response = await _client.GetAsync($"/api/metrics/multi-iteration-health?areaPath={Uri.EscapeDataString(areaPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _multiIterationHealth = await _response.Content.ReadFromJsonAsync<MultiIterationBacklogHealthDto>();
        }
    }

    [When(@"I request effort distribution")]
    public async Task WhenIRequestEffortDistribution()
    {
        _response = await _client.GetAsync("/api/metrics/effort-distribution");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request effort distribution with maxIterations (.*)")]
    public async Task WhenIRequestEffortDistributionWithMaxIterations(int maxIterations)
    {
        _response = await _client.GetAsync($"/api/metrics/effort-distribution?maxIterations={maxIterations}");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request effort distribution with defaultCapacity (.*)")]
    public async Task WhenIRequestEffortDistributionWithDefaultCapacity(int defaultCapacity)
    {
        _response = await _client.GetAsync($"/api/metrics/effort-distribution?defaultCapacity={defaultCapacity}");
    }

    [When(@"I request effort distribution with areaPathFilter ""(.*)""")]
    public async Task WhenIRequestEffortDistributionWithAreaPathFilter(string areaPathFilter)
    {
        _response = await _client.GetAsync($"/api/metrics/effort-distribution?areaPathFilter={Uri.EscapeDataString(areaPathFilter)}");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request effort distribution with areaPathFilter ""(.*)"" maxIterations (.*) and defaultCapacity (.*)")]
    public async Task WhenIRequestEffortDistributionWithAllParameters(string areaPathFilter, int maxIterations, int defaultCapacity)
    {
        _response = await _client.GetAsync(
            $"/api/metrics/effort-distribution?areaPathFilter={Uri.EscapeDataString(areaPathFilter)}&maxIterations={maxIterations}&defaultCapacity={defaultCapacity}");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request sprint capacity plan for iteration ""(.*)""")]
    public async Task WhenIRequestSprintCapacityPlanForIteration(string iterationPath)
    {
        _response = await _client.GetAsync($"/api/metrics/capacity-plan?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _capacityPlan = await _response.Content.ReadFromJsonAsync<SprintCapacityPlanDto>();
        }
    }

    [When(@"I request sprint capacity plan with empty iteration path")]
    public async Task WhenIRequestSprintCapacityPlanWithEmptyIterationPath()
    {
        _response = await _client.GetAsync("/api/metrics/capacity-plan?iterationPath=");
    }

    [When(@"I request sprint capacity plan for iteration ""(.*)"" with defaultCapacity (.*)")]
    public async Task WhenIRequestSprintCapacityPlanWithDefaultCapacity(string iterationPath, int defaultCapacity)
    {
        _response = await _client.GetAsync(
            $"/api/metrics/capacity-plan?iterationPath={Uri.EscapeDataString(iterationPath)}&defaultCapacity={defaultCapacity}");
        if (_response.IsSuccessStatusCode)
        {
            _capacityPlan = await _response.Content.ReadFromJsonAsync<SprintCapacityPlanDto>();
        }
    }

    [Then(@"the sprint metrics should have data")]
    public void ThenTheSprintMetricsShouldHaveData()
    {
        Assert.IsNotNull(_sprintMetrics);
    }

    [Then(@"the response should be BadRequest")]
    public void ThenTheResponseShouldBeBadRequest()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.BadRequest, _response.StatusCode);
    }

    [Then(@"the response should be NotFound")]
    public void ThenTheResponseShouldBeNotFound()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
    }

    [Then(@"the response should be OK")]
    public void ThenTheResponseShouldBeOK()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
    }
}
