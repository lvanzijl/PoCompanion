using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.PullRequests;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class PullRequestsControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private List<PullRequestDto>? _pullRequests;
    private PullRequestDto? _pullRequest;
    private List<PullRequestMetricsDto>? _metrics;

    public PullRequestsControllerSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Given(@"pull requests exist in the database")]
    public async Task GivenPullRequestsExistInTheDatabase(Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        foreach (var row in table.Rows)
        {
            var pr = new PullRequestEntity
            {
                Id = int.Parse(row["PullRequestId"]),
                Title = row["Title"],
                Status = row["Status"],
                CreatedBy = row["CreatedBy"],
                CreatedDate = DateTimeOffset.Parse(row["CreatedDate"]),
                RepositoryName = row["RepositoryName"],
                SourceBranch = "refs/heads/feature",
                TargetBranch = "refs/heads/main",
                IterationPath = "TestProject\\Sprint1",
                RetrievedAt = DateTimeOffset.UtcNow
            };

            dbContext.PullRequests.Add(pr);
        }

        await dbContext.SaveChangesAsync();
    }

    [When(@"I request all pull requests from ""(.*)""")]
    public async Task WhenIRequestAllPullRequestsFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        if (_response.IsSuccessStatusCode)
        {
            _pullRequests = await _response.Content.ReadFromJsonAsync<List<PullRequestDto>>();
        }
    }

    [When(@"I request pull request (.*) from controller")]
    public async Task WhenIRequestPullRequestFromController(int prId)
    {
        _response = await _client.GetAsync($"/api/pullrequests/{prId}");
        if (_response.IsSuccessStatusCode)
        {
            _pullRequest = await _response.Content.ReadFromJsonAsync<PullRequestDto>();
        }
    }

    [When(@"I request pull request metrics from ""(.*)""")]
    public async Task WhenIRequestPullRequestMetricsFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        if (_response.IsSuccessStatusCode)
        {
            _metrics = await _response.Content.ReadFromJsonAsync<List<PullRequestMetricsDto>>();
        }
    }

    [When(@"I request filtered pull requests with iterationPath ""(.*)""")]
    public async Task WhenIRequestFilteredPullRequestsWithIterationPath(string iterationPath)
    {
        _response = await _client.GetAsync($"/api/pullrequests/filter?iterationPath={Uri.EscapeDataString(iterationPath)}");
        if (_response.IsSuccessStatusCode)
        {
            _pullRequests = await _response.Content.ReadFromJsonAsync<List<PullRequestDto>>();
        }
    }

    [When(@"I request filtered pull requests with createdBy ""(.*)""")]
    public async Task WhenIRequestFilteredPullRequestsWithCreatedBy(string createdBy)
    {
        _response = await _client.GetAsync($"/api/pullrequests/filter?createdBy={Uri.EscapeDataString(createdBy)}");
        if (_response.IsSuccessStatusCode)
        {
            _pullRequests = await _response.Content.ReadFromJsonAsync<List<PullRequestDto>>();
        }
    }

    [When(@"I request filtered pull requests from ""(.*)"" to ""(.*)""")]
    public async Task WhenIRequestFilteredPullRequestsFromTo(string fromDate, string toDate)
    {
        _response = await _client.GetAsync($"/api/pullrequests/filter?fromDate={Uri.EscapeDataString(fromDate)}&toDate={Uri.EscapeDataString(toDate)}");
        if (_response.IsSuccessStatusCode)
        {
            _pullRequests = await _response.Content.ReadFromJsonAsync<List<PullRequestDto>>();
        }
    }

    [When(@"I request filtered pull requests with status ""(.*)""")]
    public async Task WhenIRequestFilteredPullRequestsWithStatus(string status)
    {
        _response = await _client.GetAsync($"/api/pullrequests/filter?status={Uri.EscapeDataString(status)}");
        if (_response.IsSuccessStatusCode)
        {
            _pullRequests = await _response.Content.ReadFromJsonAsync<List<PullRequestDto>>();
        }
    }

    [When(@"I request filtered pull requests with all parameters")]
    public async Task WhenIRequestFilteredPullRequestsWithAllParameters(Table table)
    {
        var row = table.Rows[0];
        var iterationPath = row["IterationPath"];
        var createdBy = row["CreatedBy"];
        var fromDate = row["FromDate"];
        var toDate = row["ToDate"];
        var status = row["Status"];

        _response = await _client.GetAsync(
            $"/api/pullrequests/filter?iterationPath={Uri.EscapeDataString(iterationPath)}&createdBy={Uri.EscapeDataString(createdBy)}&fromDate={Uri.EscapeDataString(fromDate)}&toDate={Uri.EscapeDataString(toDate)}&status={Uri.EscapeDataString(status)}");
        if (_response.IsSuccessStatusCode)
        {
            _pullRequests = await _response.Content.ReadFromJsonAsync<List<PullRequestDto>>();
        }
    }

    [When(@"I request pull request (.*) iterations")]
    public async Task WhenIRequestPullRequestIterations(int prId)
    {
        _response = await _client.GetAsync($"/api/pullrequests/{prId}/iterations");
    }

    [When(@"I request pull request (.*) comments")]
    public async Task WhenIRequestPullRequestComments(int prId)
    {
        _response = await _client.GetAsync($"/api/pullrequests/{prId}/comments");
    }

    [When(@"I request pull request (.*) file changes")]
    public async Task WhenIRequestPullRequestFileChanges(int prId)
    {
        _response = await _client.GetAsync($"/api/pullrequests/{prId}/filechanges");
    }

    [When(@"I send sync pull requests command")]
    public async Task WhenISendSyncPullRequestsCommand()
    {
        _response = await _client.PostAsync("/api/pullrequests/sync", null);
    }

    [When(@"I request PR review bottleneck analysis")]
    public async Task WhenIRequestPRReviewBottleneckAnalysis()
    {
        _response = await _client.GetAsync("/api/pullrequests/review-bottleneck");
    }

    [When(@"I request PR review bottleneck with maxPRs (.*) and daysBack (.*)")]
    public async Task WhenIRequestPRReviewBottleneckWithParameters(int maxPRs, int daysBack)
    {
        _response = await _client.GetAsync($"/api/pullrequests/review-bottleneck?maxPRs={maxPRs}&daysBack={daysBack}");
    }

    [Then(@"I should receive at least (.*) pull requests")]
    public void ThenIShouldReceiveAtLeastPullRequests(int count)
    {
        Assert.IsNotNull(_pullRequests);
        Assert.IsTrue(_pullRequests.Count >= count, $"Expected at least {count} pull requests, but got {_pullRequests.Count}");
    }

    [Then(@"the pull request should have title ""(.*)""")]
    public void ThenThePullRequestShouldHaveTitle(string title)
    {
        Assert.IsNotNull(_pullRequest);
        Assert.AreEqual(title, _pullRequest.Title);
    }

    [Then(@"the metrics should contain aggregated data")]
    public void ThenTheMetricsShouldContainAggregatedData()
    {
        Assert.IsNotNull(_metrics);
    }

    [Then(@"the sync result should contain synced count")]
    public async Task ThenTheSyncResultShouldContainSyncedCount()
    {
        Assert.IsNotNull(_response);
        var content = await _response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("SyncedCount") || content.Contains("syncedCount"));
    }
}
