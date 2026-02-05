using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Shared.WorkItems;
using PoTool.Tests.Integration.Support;
using Reqnroll;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class WorkItemsControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private List<WorkItemDto>? _workItems;
    private WorkItemDto? _workItem;
    private List<WorkItemWithValidationDto>? _workItemsWithValidation;

    public WorkItemsControllerSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
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
                RetrievedAt: DateTimeOffset.UtcNow,
                    Effort: null,
                    Description: null
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
                RetrievedAt = workItem.RetrievedAt
            });
        }

        await dbContext.SaveChangesAsync();
    }

    [Given(@"work items exist in the database with parent-child relationships")]
    public async Task GivenWorkItemsExistWithParentChildRelationships(Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        foreach (var row in table.Rows)
        {
            int? parentTfsId = null;
            if (row.ContainsKey("ParentTfsId") && !string.IsNullOrWhiteSpace(row["ParentTfsId"]))
            {
                parentTfsId = int.Parse(row["ParentTfsId"]);
            }

            int? effort = null;
            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]))
            {
                effort = int.Parse(row["Effort"]);
            }

            var workItem = new WorkItemDto(
                TfsId: int.Parse(row["TfsId"]),
                Type: row["Type"],
                Title: row["Title"],
                ParentTfsId: parentTfsId,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: row["State"],
                RetrievedAt: DateTimeOffset.UtcNow,
                    Effort: effort,
                    Description: null
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
                RetrievedAt = workItem.RetrievedAt,
                Effort = workItem.Effort
            });
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request all work items from ""(.*)""")]
    public async Task WhenIRequestAllWorkItemsFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;

        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItems = await _response.Content.ReadFromJsonAsync<List<WorkItemDto>>();
        }
    }

    [When(@"I request work item (-?\d+) from controller")]
    public async Task WhenIRequestWorkItemFromController(int tfsId)
    {
        _response = await _client.GetAsync($"/api/workitems/{tfsId}");
        _scenarioContext["Response"] = _response;

        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItem = await _response.Content.ReadFromJsonAsync<WorkItemDto>();
        }
    }

    [When(@"I request filtered work items with filter ""(.*)""")]
    public async Task WhenIRequestFilteredWorkItems(string filter)
    {
        _response = await _client.GetAsync($"/api/workitems/filter/{filter}");
        _scenarioContext["Response"] = _response;

        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItems = await _response.Content.ReadFromJsonAsync<List<WorkItemDto>>();
        }
    }

    [When(@"I request goal hierarchy for IDs ""(.*)""")]
    public async Task WhenIRequestGoalHierarchyForIds(string goalIds)
    {
        _response = await _client.GetAsync($"/api/workitems/goals?goalIds={goalIds}");
        _scenarioContext["Response"] = _response;

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

    [When(@"I request all work items with validation from ""(.*)""")]
    public async Task WhenIRequestAllWorkItemsWithValidationFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;

        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItemsWithValidation = await _response.Content.ReadFromJsonAsync<List<WorkItemWithValidationDto>>();
        }
    }

    [Then(@"work item (\d+) should have validation errors about parent not in progress")]
    public void ThenWorkItemShouldHaveValidationErrorsAboutParentNotInProgress(int tfsId)
    {
        Assert.IsNotNull(_workItemsWithValidation);
        var workItem = _workItemsWithValidation.FirstOrDefault(w => w.TfsId == tfsId);
        Assert.IsNotNull(workItem, $"Work item {tfsId} not found");
        Assert.IsTrue(workItem.ValidationIssues.Count > 0, "Expected validation issues");
        Assert.IsTrue(workItem.ValidationIssues.Any(i =>
            i.Severity == "Error" && i.Message.Contains("Parent")),
            "Expected error about parent not in progress");
    }

    [Then(@"work item (\d+) should have no validation issues")]
    public void ThenWorkItemShouldHaveNoValidationIssues(int tfsId)
    {
        Assert.IsNotNull(_workItemsWithValidation);
        var workItem = _workItemsWithValidation.FirstOrDefault(w => w.TfsId == tfsId);
        Assert.IsNotNull(workItem, $"Work item {tfsId} not found");
        Assert.AreEqual(0, workItem.ValidationIssues.Count, "Expected no validation issues");
    }

    [When(@"I request all goals from ""(.*)""")]
    public async Task WhenIRequestAllGoalsFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;

        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _workItems = await _response.Content.ReadFromJsonAsync<List<WorkItemDto>>();
        }
    }

    [Then(@"all returned work items should be of type ""(.*)""")]
    public void ThenAllReturnedWorkItemsShouldBeOfType(string expectedType)
    {
        Assert.IsNotNull(_workItems);
        Assert.IsTrue(_workItems.Count > 0, "Expected at least one work item");
        Assert.IsTrue(_workItems.All(w => w.Type == expectedType),
            $"Expected all work items to be of type {expectedType}");
    }

    [Given(@"work item revisions exist")]
    public Task GivenWorkItemRevisionsExist(Table table)
    {
        // Mock TFS client is pre-configured to return revision data for all work item IDs
        // This step is present for documentation in the feature file to make the test scenario clear
        // The MockTfsClient.GetWorkItemRevisionsAsync() method returns 3 mock revisions for any work item ID
        return Task.CompletedTask;
    }

    [Given(@"work item state timeline exists")]
    public async Task GivenWorkItemStateTimelineExists(Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        foreach (var row in table.Rows)
        {
            var workItemId = int.Parse(row["WorkItemId"]);

            // Ensure the work item exists in the database
            if (!dbContext.WorkItems.Any(w => w.TfsId == workItemId))
            {
                dbContext.WorkItems.Add(new WorkItemEntity
                {
                    TfsId = workItemId,
                    Type = "Task",
                    Title = $"Work Item {workItemId}",
                    State = row["State"],
                    AreaPath = "\\TestArea",
                    IterationPath = "\\TestIteration",
                    RetrievedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request work item (-?\d+) revisions")]
    public async Task WhenIRequestWorkItemRevisions(int workItemId)
    {
        _response = await _client.GetAsync($"/api/workitems/{workItemId}/revisions");
        _scenarioContext["Response"] = _response;
    }

    [When(@"I request work item (-?\d+) state timeline")]
    public async Task WhenIRequestWorkItemStateTimeline(int workItemId)
    {
        _response = await _client.GetAsync($"/api/workitems/{workItemId}/state-timeline");
        _scenarioContext["Response"] = _response;
    }

    [Then(@"I should receive revision history")]
    public async Task ThenIShouldReceiveRevisionHistory()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        var content = await _response.Content.ReadAsStringAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(content));
    }

    [Then(@"I should receive empty revision list")]
    public async Task ThenIShouldReceiveEmptyRevisionList()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        var revisions = await _response.Content.ReadFromJsonAsync<List<object>>();
        Assert.IsNotNull(revisions);
        Assert.AreEqual(0, revisions.Count);
    }

    [Then(@"I should receive state timeline data")]
    public async Task ThenIShouldReceiveStateTimelineData()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        var content = await _response.Content.ReadAsStringAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(content));
    }

    // Bulk Assign Effort steps
    private BulkEffortAssignmentResultDto? _bulkAssignmentResult;

    [Given(@"work items exist for testing")]
    public async Task GivenWorkItemsExistForTesting(Table table)
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
                RetrievedAt = DateTimeOffset.UtcNow
            };

            if (row.ContainsKey("Effort") && !string.IsNullOrWhiteSpace(row["Effort"]) && row["Effort"] != "null")
            {
                workItem.Effort = int.Parse(row["Effort"]);
            }

            dbContext.WorkItems.Add(workItem);
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I bulk assign effort with assignments")]
    public async Task WhenIBulkAssignEffortWithAssignments(Table table)
    {
        var assignments = new List<BulkEffortAssignmentDto>();
        foreach (var row in table.Rows)
        {
            assignments.Add(new BulkEffortAssignmentDto(
                WorkItemId: int.Parse(row["WorkItemId"]),
                EffortValue: int.Parse(row["EffortValue"])
            ));
        }

        var command = new Core.WorkItems.Commands.BulkAssignEffortCommand(assignments);
        _scenarioContext["Response"] = _response = await _client.PostAsJsonAsync("/api/workitems/bulk-assign-effort", command);

        if (_response.IsSuccessStatusCode)
        {
            _bulkAssignmentResult = await _response.Content.ReadFromJsonAsync<BulkEffortAssignmentResultDto>();
        }
    }

    [When(@"I bulk assign effort with empty assignments")]
    public async Task WhenIBulkAssignEffortWithEmptyAssignments()
    {
        var command = new Core.WorkItems.Commands.BulkAssignEffortCommand(new List<BulkEffortAssignmentDto>());
        _scenarioContext["Response"] = _response = await _client.PostAsJsonAsync("/api/workitems/bulk-assign-effort", command);
    }

    [Then(@"the bulk assignment should show (.*) successful updates")]
    public void ThenTheBulkAssignmentShouldShowSuccessfulUpdates(int expectedCount)
    {
        Assert.IsNotNull(_bulkAssignmentResult);
        Assert.AreEqual(expectedCount, _bulkAssignmentResult.SuccessfulUpdates);
    }

    [Then(@"the bulk assignment should show (.*) failed updates")]
    public void ThenTheBulkAssignmentShouldShowFailedUpdates(int expectedCount)
    {
        Assert.IsNotNull(_bulkAssignmentResult);
        Assert.AreEqual(expectedCount, _bulkAssignmentResult.FailedUpdates);
    }
}
