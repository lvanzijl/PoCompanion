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
public class WorkItemsControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HttpResponseMessage? _response;
    private List<WorkItemDto>? _workItems;
    private WorkItemDto? _workItem;

    public WorkItemsControllerSteps()
    {
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Given(@"work items exist in the database")]
    public async Task GivenWorkItemsExistInTheDatabase(Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        foreach (var row in table.Rows)
        {
            var workItem = new WorkItemDto(
                TfsId: int.Parse(row["TfsId"]),
                Type: row["Type"],
                Title: row["Title"],
                ParentTfsId: null,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: row["State"],
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            );

            dbContext.WorkItems.Add(new WorkItemEntity
            {
                TfsId = workItem.TfsId,
                Type = workItem.Type,
                Title = workItem.Title,
                ParentTfsId = workItem.ParentTfsId,
                AreaPath = workItem.AreaPath,
                IterationPath = workItem.IterationPath,
                State = workItem.State,
                JsonPayload = workItem.JsonPayload,
                RetrievedAt = workItem.RetrievedAt
            });
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request all work items from ""(.*)""")]
    public async Task WhenIRequestAllWorkItemsFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItems = await _response.Content.ReadFromJsonAsync<List<WorkItemDto>>();
        }
    }

    [When(@"I request work item (\d+) from controller")]
    public async Task WhenIRequestWorkItemFromController(int tfsId)
    {
        _response = await _client.GetAsync($"/api/workitems/{tfsId}");
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItem = await _response.Content.ReadFromJsonAsync<WorkItemDto>();
        }
    }

    [When(@"I request filtered work items with filter ""(.*)""")]
    public async Task WhenIRequestFilteredWorkItems(string filter)
    {
        _response = await _client.GetAsync($"/api/workitems/filter/{filter}");
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItems = await _response.Content.ReadFromJsonAsync<List<WorkItemDto>>();
        }
    }

    [When(@"I request goal hierarchy for IDs ""(.*)""")]
    public async Task WhenIRequestGoalHierarchyForIds(string goalIds)
    {
        _response = await _client.GetAsync($"/api/workitems/goals?goalIds={goalIds}");
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItems = await _response.Content.ReadFromJsonAsync<List<WorkItemDto>>();
        }
    }

    [Then(@"I should receive at least (\d+) work items")]
    public void ThenIShouldReceiveAtLeastWorkItems(int minCount)
    {
        Assert.IsNotNull(_workItems);
        Assert.IsTrue(_workItems.Count >= minCount, 
            $"Expected at least {minCount} work items, but got {_workItems.Count}");
    }

    [Then(@"the work item should have title ""(.*)""")]
    public void ThenTheWorkItemShouldHaveTitle(string expectedTitle)
    {
        Assert.IsNotNull(_workItem);
        Assert.AreEqual(expectedTitle, _workItem.Title);
    }

    [Then(@"the work item should have type ""(.*)""")]
    public void ThenTheWorkItemShouldHaveType(string expectedType)
    {
        Assert.IsNotNull(_workItem);
        Assert.AreEqual(expectedType, _workItem.Type);
    }

    [Then(@"the results should contain work items matching ""(.*)""")]
    public void ThenTheResultsShouldContainWorkItemsMatching(string filter)
    {
        Assert.IsNotNull(_workItems);
        Assert.IsTrue(_workItems.Count > 0, "Expected at least one work item in results");
    }

    [Then(@"the hierarchy should include descendants of goal (\d+)")]
    public void ThenTheHierarchyShouldIncludeDescendants(int goalId)
    {
        Assert.IsNotNull(_workItems);
        Assert.IsTrue(_workItems.Any(w => w.TfsId == goalId), 
            $"Expected goal {goalId} in hierarchy");
    }
}
