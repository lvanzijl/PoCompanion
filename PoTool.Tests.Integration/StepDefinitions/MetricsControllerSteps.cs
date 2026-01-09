using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.Metrics;
using PoTool.Tests.Integration.Support;
using Reqnroll;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

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

    public MetricsControllerSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
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

            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]) && row["Effort"] != "null")
            {
                workItem.Effort = int.Parse(row["Effort"]);
            }

            // Note: WorkItemEntity doesn't have an AssignedTo field as a direct property.
            // In the real system, AssignedTo data is stored in the JsonPayload field.
            // For these tests, the AssignedTo information is not critical to the metrics calculations
            // being tested, so we omit it to keep tests focused and simple.

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

            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]) && row["Effort"] != "null")
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
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/sprint?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _sprintMetrics = await _response.Content.ReadFromJsonAsync<SprintMetricsDto>();
        }
    }

    [When(@"I request sprint metrics with empty iteration path")]
    public async Task WhenIRequestSprintMetricsWithEmptyIterationPath()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/sprint?iterationPath=");
    }

    [When(@"I request velocity trend with default parameters")]
    public async Task WhenIRequestVelocityTrendWithDefaultParameters()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/velocity");
        if (_response.IsSuccessStatusCode)
        {
            _velocityTrend = await _response.Content.ReadFromJsonAsync<VelocityTrendDto>();
        }
    }

    [When(@"I request velocity trend with maxSprints (.*)")]
    public async Task WhenIRequestVelocityTrendWithMaxSprints(int maxSprints)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/velocity?maxSprints={maxSprints}");
        if (_response.IsSuccessStatusCode)
        {
            _velocityTrend = await _response.Content.ReadFromJsonAsync<VelocityTrendDto>();
        }
    }

    [When(@"I request velocity trend with areaPath ""(.*)""")]
    public async Task WhenIRequestVelocityTrendWithAreaPath(string areaPath)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/velocity?areaPath={Uri.EscapeDataString(areaPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _velocityTrend = await _response.Content.ReadFromJsonAsync<VelocityTrendDto>();
        }
    }

    [When(@"I request backlog health for iteration ""(.*)""")]
    public async Task WhenIRequestBacklogHealthForIteration(string iterationPath)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/backlog-health?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _backlogHealth = await _response.Content.ReadFromJsonAsync<BacklogHealthDto>();
        }
    }

    [When(@"I request backlog health with empty iteration path")]
    public async Task WhenIRequestBacklogHealthWithEmptyIterationPath()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/backlog-health?iterationPath=");
    }

    [When(@"I request multi-iteration backlog health")]
    public async Task WhenIRequestMultiIterationBacklogHealth()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/multi-iteration-health");
        if (_response.IsSuccessStatusCode)
        {
            _multiIterationHealth = await _response.Content.ReadFromJsonAsync<MultiIterationBacklogHealthDto>();
        }
    }

    [When(@"I request multi-iteration backlog health with maxIterations (.*)")]
    public async Task WhenIRequestMultiIterationBacklogHealthWithMaxIterations(int maxIterations)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/multi-iteration-health?maxIterations={maxIterations}");
        if (_response.IsSuccessStatusCode)
        {
            _multiIterationHealth = await _response.Content.ReadFromJsonAsync<MultiIterationBacklogHealthDto>();
        }
    }

    [When(@"I request multi-iteration backlog health with areaPath ""(.*)""")]
    public async Task WhenIRequestMultiIterationBacklogHealthWithAreaPath(string areaPath)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/multi-iteration-health?areaPath={Uri.EscapeDataString(areaPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _multiIterationHealth = await _response.Content.ReadFromJsonAsync<MultiIterationBacklogHealthDto>();
        }
    }

    [When(@"I request effort distribution")]
    public async Task WhenIRequestEffortDistribution()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/effort-distribution");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request effort distribution with maxIterations (.*)")]
    public async Task WhenIRequestEffortDistributionWithMaxIterations(int maxIterations)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/effort-distribution?maxIterations={maxIterations}");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request effort distribution with defaultCapacity (.*)")]
    public async Task WhenIRequestEffortDistributionWithDefaultCapacity(int defaultCapacity)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/effort-distribution?defaultCapacity={defaultCapacity}");
    }

    [When(@"I request effort distribution with areaPathFilter ""(.*)""")]
    public async Task WhenIRequestEffortDistributionWithAreaPathFilter(string areaPathFilter)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/effort-distribution?areaPathFilter={Uri.EscapeDataString(areaPathFilter)}");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request effort distribution with areaPathFilter ""(.*)"" maxIterations (.*) and defaultCapacity (.*)")]
    public async Task WhenIRequestEffortDistributionWithAllParameters(string areaPathFilter, int maxIterations, int defaultCapacity)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/metrics/effort-distribution?areaPathFilter={Uri.EscapeDataString(areaPathFilter)}&maxIterations={maxIterations}&defaultCapacity={defaultCapacity}");
        if (_response.IsSuccessStatusCode)
        {
            _effortDistribution = await _response.Content.ReadFromJsonAsync<EffortDistributionDto>();
        }
    }

    [When(@"I request sprint capacity plan for iteration ""(.*)""")]
    public async Task WhenIRequestSprintCapacityPlanForIteration(string iterationPath)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync($"/api/metrics/capacity-plan?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _capacityPlan = await _response.Content.ReadFromJsonAsync<SprintCapacityPlanDto>();
        }
    }

    [When(@"I request sprint capacity plan with empty iteration path")]
    public async Task WhenIRequestSprintCapacityPlanWithEmptyIterationPath()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/capacity-plan?iterationPath=");
    }

    [When(@"I request sprint capacity plan for iteration ""(.*)"" with defaultCapacity (.*)")]
    public async Task WhenIRequestSprintCapacityPlanWithDefaultCapacity(string iterationPath, int defaultCapacity)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
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

    // Effort Estimation Suggestions steps
    private IReadOnlyList<EffortEstimationSuggestionDto>? _effortSuggestions;

    [When(@"I request effort estimation suggestions")]
    public async Task WhenIRequestEffortEstimationSuggestions()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/effort-estimation-suggestions");
        if (_response.IsSuccessStatusCode)
        {
            _effortSuggestions = await _response.Content.ReadFromJsonAsync<IReadOnlyList<EffortEstimationSuggestionDto>>();
        }
    }

    [When(@"I request effort estimation suggestions for iteration ""(.*)""")]
    public async Task WhenIRequestEffortEstimationSuggestionsForIteration(string iterationPath)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/metrics/effort-estimation-suggestions?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _effortSuggestions = await _response.Content.ReadFromJsonAsync<IReadOnlyList<EffortEstimationSuggestionDto>>();
        }
    }

    [When(@"I request effort estimation suggestions with onlyInProgressItems (.*)")]
    public async Task WhenIRequestEffortEstimationSuggestionsWithOnlyInProgressItems(string onlyInProgress)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/metrics/effort-estimation-suggestions?onlyInProgressItems={onlyInProgress}");
        if (_response.IsSuccessStatusCode)
        {
            _effortSuggestions = await _response.Content.ReadFromJsonAsync<IReadOnlyList<EffortEstimationSuggestionDto>>();
        }
    }

    [Then(@"the effort suggestions should contain suggestions")]
    public void ThenTheEffortSuggestionsShouldContainSuggestions()
    {
        Assert.IsNotNull(_effortSuggestions);
        Assert.IsTrue(_effortSuggestions.Count > 0);
    }

    [Then(@"the effort suggestions should contain (.*) suggestion")]
    public void ThenTheEffortSuggestionsShouldContainSpecificCount(int count)
    {
        Assert.IsNotNull(_effortSuggestions);
        Assert.AreEqual(count, _effortSuggestions.Count);
    }

    [Given(@"work items exist with mixed states")]
    public async Task GivenWorkItemsExistWithMixedStates(Table table)
    {
        await GivenWorkItemsExistForMultipleIterations(table);
    }

    // Effort Estimation Quality steps
    private EffortEstimationQualityDto? _effortQuality;

    [When(@"I request effort estimation quality")]
    public async Task WhenIRequestEffortEstimationQuality()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/metrics/effort-estimation-quality");
        if (_response.IsSuccessStatusCode)
        {
            _effortQuality = await _response.Content.ReadFromJsonAsync<EffortEstimationQualityDto>();
        }
    }

    [When(@"I request effort estimation quality with maxIterations (.*)")]
    public async Task WhenIRequestEffortEstimationQualityWithMaxIterations(int maxIterations)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/metrics/effort-estimation-quality?maxIterations={maxIterations}");
        if (_response.IsSuccessStatusCode)
        {
            _effortQuality = await _response.Content.ReadFromJsonAsync<EffortEstimationQualityDto>();
        }
    }

    [Then(@"the quality metrics should contain data")]
    public void ThenTheQualityMetricsShouldContainData()
    {
        Assert.IsNotNull(_effortQuality);
        Assert.IsTrue(_effortQuality.TotalCompletedWorkItems > 0);
    }
}
