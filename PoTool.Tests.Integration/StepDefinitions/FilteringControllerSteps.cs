using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Client.ApiClient;
using PoTool.Tests.Integration.Support;
using Reqnroll;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class FilteringControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private FilterByValidationResponse? _filterByValidationResponse;
    private GetWorkItemIdsByValidationFilterResponse? _getIdsResponse;
    private CountWorkItemsByValidationFilterResponse? _countResponse;
    private IsDescendantOfGoalsResponse? _descendantResponse;

    public FilteringControllerSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [Given(@"work items with validation exist in the database")]
    public async Task GivenWorkItemsWithValidationExistInTheDatabase(Table table)
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

            bool hasEffort = row.ContainsKey("HasEffort") && bool.Parse(row["HasEffort"]);

            var entity = new WorkItemEntity
            {
                TfsId = int.Parse(row["TfsId"]),
                Type = row["Type"],
                Title = row["Title"],
                ParentTfsId = parentTfsId,
                AreaPath = "\\TestArea",
                IterationPath = "\\TestIteration",
                State = row["State"],
                RetrievedAt = DateTimeOffset.UtcNow,
                Effort = hasEffort ? 5 : null
            };

            dbContext.WorkItems.Add(entity);
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request filtering by validation with target IDs ""(.*)""")]
    public async Task WhenIRequestFilteringByValidationWithTargetIDs(string targetIdsString)
    {
        var targetIds = targetIdsString.Split(',').Select(int.Parse).ToHashSet();
        var request = new FilterByValidationRequest { TargetIds = targetIds };

        _response = await _client.PostAsJsonAsync("/api/filtering/by-validation-with-ancestors", request);

        if (_response.IsSuccessStatusCode)
        {
            _filterByValidationResponse = await _response.Content.ReadFromJsonAsync<FilterByValidationResponse>();
        }
    }

    [When(@"I request work item IDs by validation filter ""(.*)""")]
    public async Task WhenIRequestWorkItemIDsByValidationFilter(string filterId)
    {
        var request = new GetWorkItemIdsByValidationFilterRequest { FilterId = filterId };

        _response = await _client.PostAsJsonAsync("/api/filtering/ids-by-validation-filter", request);

        if (_response.IsSuccessStatusCode)
        {
            _getIdsResponse = await _response.Content.ReadFromJsonAsync<GetWorkItemIdsByValidationFilterResponse>();
        }
    }

    [When(@"I count work items by validation filter ""(.*)""")]
    public async Task WhenICountWorkItemsByValidationFilter(string filterId)
    {
        var request = new CountWorkItemsByValidationFilterRequest { FilterId = filterId };

        _response = await _client.PostAsJsonAsync("/api/filtering/count-by-validation-filter", request);

        if (_response.IsSuccessStatusCode)
        {
            _countResponse = await _response.Content.ReadFromJsonAsync<CountWorkItemsByValidationFilterResponse>();
        }
    }

    [When(@"I check if work item (.*) is descendant of goals ""(.*)""")]
    public async Task WhenICheckIfWorkItemIsDescendantOfGoals(int workItemId, string goalIdsString)
    {
        var goalIds = goalIdsString.Split(',').Select(int.Parse).ToList();
        var request = new IsDescendantOfGoalsRequest
        {
            WorkItemId = workItemId,
            GoalIds = goalIds
        };

        _response = await _client.PostAsJsonAsync("/api/filtering/is-descendant-of-goals", request);

        if (_response.IsSuccessStatusCode)
        {
            _descendantResponse = await _response.Content.ReadFromJsonAsync<IsDescendantOfGoalsResponse>();
        }
    }

    [Then(@"the filtering response should be OK")]
    public void ThenTheFilteringResponseShouldBeOK()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
    }

    [Then(@"the filtered IDs should include ""(.*)""")]
    public void ThenTheFilteredIDsShouldInclude(string expectedIdsString)
    {
        var expectedIds = expectedIdsString.Split(',').Select(int.Parse).ToHashSet();

        if (_filterByValidationResponse != null)
        {
            Assert.IsNotNull(_filterByValidationResponse.WorkItemIds);
            var actualIds = _filterByValidationResponse.WorkItemIds.ToHashSet();

            foreach (var expectedId in expectedIds)
            {
                Assert.IsTrue(actualIds.Contains(expectedId),
                    $"Expected ID {expectedId} not found in filtered results");
            }
        }
        else if (_getIdsResponse != null)
        {
            Assert.IsNotNull(_getIdsResponse.WorkItemIds);
            var actualIds = _getIdsResponse.WorkItemIds.ToHashSet();

            foreach (var expectedId in expectedIds)
            {
                Assert.IsTrue(actualIds.Contains(expectedId),
                    $"Expected ID {expectedId} not found in filtered results");
            }
        }
        else
        {
            Assert.Fail("No filtering response available");
        }
    }

    [Then(@"the count should be (.*)")]
    public void ThenTheCountShouldBe(int expectedCount)
    {
        Assert.IsNotNull(_countResponse);
        Assert.AreEqual(expectedCount, _countResponse.Count);
    }

    [Then(@"the descendant check should return (true|false)")]
    public void ThenTheDescendantCheckShouldReturn(bool expected)
    {
        Assert.IsNotNull(_descendantResponse);
        Assert.AreEqual(expected, _descendantResponse.IsDescendant);
    }
}
