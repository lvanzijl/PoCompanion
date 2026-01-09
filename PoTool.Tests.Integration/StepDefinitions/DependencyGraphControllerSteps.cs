using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.WorkItems;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class DependencyGraphControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private DependencyGraphDto? _dependencyGraph;

    public DependencyGraphControllerSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [Given(@"work items with dependencies exist")]
    [Given(@"work items with circular dependencies exist")]
    [Given(@"work items with long dependency chain exist")]
    [Given(@"work items with blocking relationships exist")]
    public async Task GivenWorkItemsWithDependenciesExist(Table table)
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
                AreaPath = row["AreaPath"],
                JsonPayload = row["JsonPayload"],
                RetrievedAt = DateTimeOffset.UtcNow
            };

            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]))
            {
                workItem.Effort = int.Parse(row["Effort"]);
            }

            dbContext.WorkItems.Add(workItem);
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request dependency graph with no filters")]
    public async Task WhenIRequestDependencyGraphWithNoFilters()
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync("/api/workitems/dependency-graph");
        if (_response.IsSuccessStatusCode)
        {
            _dependencyGraph = await _response.Content.ReadFromJsonAsync<DependencyGraphDto>();
        }
    }

    [When(@"I request dependency graph with areaPathFilter ""(.*)""")]
    public async Task WhenIRequestDependencyGraphWithAreaPathFilter(string areaPathFilter)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/workitems/dependency-graph?areaPathFilter={Uri.EscapeDataString(areaPathFilter)}");
        if (_response.IsSuccessStatusCode)
        {
            _dependencyGraph = await _response.Content.ReadFromJsonAsync<DependencyGraphDto>();
        }
    }

    [When(@"I request dependency graph with workItemTypes ""(.*)""")]
    public async Task WhenIRequestDependencyGraphWithWorkItemTypes(string workItemTypes)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/workitems/dependency-graph?workItemTypes={Uri.EscapeDataString(workItemTypes)}");
        if (_response.IsSuccessStatusCode)
        {
            _dependencyGraph = await _response.Content.ReadFromJsonAsync<DependencyGraphDto>();
        }
    }

    [When(@"I request dependency graph with workItemIds ""(.*)""")]
    public async Task WhenIRequestDependencyGraphWithWorkItemIds(string workItemIds)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/workitems/dependency-graph?workItemIds={Uri.EscapeDataString(workItemIds)}");
        if (_response.IsSuccessStatusCode)
        {
            _dependencyGraph = await _response.Content.ReadFromJsonAsync<DependencyGraphDto>();
        }
    }

    [When(@"I request dependency graph with invalid workItemIds ""(.*)""")]
    public async Task WhenIRequestDependencyGraphWithInvalidWorkItemIds(string invalidIds)
    {
        _scenarioContext["Response"] = _response = await _client.GetAsync(
            $"/api/workitems/dependency-graph?workItemIds={Uri.EscapeDataString(invalidIds)}");
    }

    [Then(@"the dependency graph should have (.*) nodes")]
    public void ThenTheDependencyGraphShouldHaveNodes(int expectedNodeCount)
    {
        Assert.IsNotNull(_dependencyGraph, "Dependency graph should not be null");
        Assert.AreEqual(expectedNodeCount, _dependencyGraph.Nodes.Count, 
            $"Expected {expectedNodeCount} nodes but got {_dependencyGraph.Nodes.Count}");
    }

    [Then(@"the dependency graph should have links")]
    public void ThenTheDependencyGraphShouldHaveLinks()
    {
        Assert.IsNotNull(_dependencyGraph, "Dependency graph should not be null");
        Assert.IsTrue(_dependencyGraph.Links.Count > 0, "Dependency graph should have at least one link");
    }

    [Then(@"the dependency graph should have circular dependencies")]
    public void ThenTheDependencyGraphShouldHaveCircularDependencies()
    {
        Assert.IsNotNull(_dependencyGraph, "Dependency graph should not be null");
        Assert.IsTrue(_dependencyGraph.CircularDependencies.Count > 0, 
            "Dependency graph should have at least one circular dependency");
    }

    [Then(@"the dependency graph should have critical paths")]
    public void ThenTheDependencyGraphShouldHaveCriticalPaths()
    {
        Assert.IsNotNull(_dependencyGraph, "Dependency graph should not be null");
        Assert.IsTrue(_dependencyGraph.CriticalPaths.Count > 0, 
            "Dependency graph should have at least one critical path");
    }

    [Then(@"the dependency graph should have blocked work items")]
    public void ThenTheDependencyGraphShouldHaveBlockedWorkItems()
    {
        Assert.IsNotNull(_dependencyGraph, "Dependency graph should not be null");
        Assert.IsTrue(_dependencyGraph.BlockedWorkItemIds.Count > 0, 
            "Dependency graph should have at least one blocked work item");
    }
}
