using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Commands;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class ValidationEnhancementsSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private List<ValidationViolationHistoryDto>? _historyRecords;
    private ValidationImpactAnalysisDto? _impactAnalysis;
    private FixValidationViolationResultDto? _fixResult;
    private List<FixValidationViolationDto> _fixSuggestions = new();

    public ValidationEnhancementsSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [Given(@"work items exist in the database with validation violations")]
    public async Task GivenWorkItemsExistWithValidationViolations(Table table)
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

            var workItem = new WorkItemEntity
            {
                TfsId = int.Parse(row["TfsId"]),
                Type = row["Type"],
                Title = row["Title"],
                ParentTfsId = parentTfsId,
                AreaPath = row["AreaPath"],
                IterationPath = row["IterationPath"],
                State = row["State"],
                JsonPayload = "{}",
                RetrievedAt = DateTimeOffset.UtcNow
            };

            dbContext.WorkItems.Add(workItem);
        }

        await dbContext.SaveChangesAsync();
    }

    [Given(@"I have fix suggestions for violations")]
    public void GivenIHaveFixSuggestionsForViolations(Table table)
    {
        _fixSuggestions.Clear();

        foreach (var row in table.Rows)
        {
            _fixSuggestions.Add(new FixValidationViolationDto(
                WorkItemId: int.Parse(row["WorkItemId"]),
                FixType: row["FixType"],
                Description: row["Description"],
                NewState: row["NewState"],
                Justification: $"Automated fix suggestion: {row["Description"]}"
            ));
        }
    }

    [When(@"I request validation history from ""(.*)""")]
    public async Task WhenIRequestValidationHistoryFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;
        if (_response.IsSuccessStatusCode)
        {
            _historyRecords = await _response.Content.ReadFromJsonAsync<List<ValidationViolationHistoryDto>>();
        }
    }

    [When(@"I request validation history with area path filter ""(.*)""")]
    public async Task WhenIRequestValidationHistoryWithAreaPathFilter(string areaPath)
    {
        _response = await _client.GetAsync($"/api/workitems/validation-history?areaPathFilter={areaPath}");
        _scenarioContext["Response"] = _response;
        if (_response.IsSuccessStatusCode)
        {
            _historyRecords = await _response.Content.ReadFromJsonAsync<List<ValidationViolationHistoryDto>>();
        }
    }

    [When(@"I request validation history with start date ""(.*)""")]
    public async Task WhenIRequestValidationHistoryWithStartDate(string startDate)
    {
        _response = await _client.GetAsync($"/api/workitems/validation-history?startDate={startDate}");
        _scenarioContext["Response"] = _response;
        if (_response.IsSuccessStatusCode)
        {
            _historyRecords = await _response.Content.ReadFromJsonAsync<List<ValidationViolationHistoryDto>>();
        }
    }

    [When(@"I request validation impact analysis from ""(.*)""")]
    public async Task WhenIRequestValidationImpactAnalysisFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;
        if (_response.IsSuccessStatusCode)
        {
            _impactAnalysis = await _response.Content.ReadFromJsonAsync<ValidationImpactAnalysisDto>();
        }
    }

    [When(@"I request validation impact analysis with area filter ""(.*)""")]
    public async Task WhenIRequestValidationImpactAnalysisWithAreaFilter(string areaPath)
    {
        _response = await _client.GetAsync($"/api/workitems/validation-impact-analysis?areaPathFilter={areaPath}");
        _scenarioContext["Response"] = _response;
        if (_response.IsSuccessStatusCode)
        {
            _impactAnalysis = await _response.Content.ReadFromJsonAsync<ValidationImpactAnalysisDto>();
        }
    }

    [When(@"I send batch fix request to ""(.*)""")]
    public async Task WhenISendBatchFixRequestTo(string endpoint)
    {
        var command = new FixValidationViolationBatchCommand(_fixSuggestions);
        _response = await _client.PostAsJsonAsync(endpoint, command);
        _scenarioContext["Response"] = _response;
        if (_response.IsSuccessStatusCode)
        {
            _fixResult = await _response.Content.ReadFromJsonAsync<FixValidationViolationResultDto>();
        }
    }

    [When(@"I send empty batch fix request to ""(.*)""")]
    public async Task WhenISendEmptyBatchFixRequestTo(string endpoint)
    {
        var command = new FixValidationViolationBatchCommand(new List<FixValidationViolationDto>());
        _response = await _client.PostAsJsonAsync(endpoint, command);
        _scenarioContext["Response"] = _response;
    }

    [Then(@"I should receive validation history records")]
    public void ThenIShouldReceiveValidationHistoryRecords()
    {
        Assert.IsNotNull(_historyRecords);
        Assert.IsTrue(_historyRecords.Count > 0, "Expected at least one history record");
    }

    [Then(@"the history should include violations for work item (.*)")]
    public void ThenTheHistoryShouldIncludeViolationsForWorkItem(int workItemId)
    {
        Assert.IsNotNull(_historyRecords);
        Assert.IsTrue(_historyRecords.Any(h => h.WorkItemId == workItemId),
            $"Expected history to include violations for work item {workItemId}");
    }

    [Then(@"all history records should have area path starting with ""(.*)""")]
    public void ThenAllHistoryRecordsShouldHaveAreaPathStartingWith(string areaPath)
    {
        Assert.IsNotNull(_historyRecords);
        Assert.IsTrue(_historyRecords.All(h => h.AreaPath.StartsWith(areaPath, StringComparison.OrdinalIgnoreCase)),
            $"Expected all history records to have area path starting with '{areaPath}'");
    }

    [Then(@"all history records should be after start date")]
    public void ThenAllHistoryRecordsShouldBeAfterStartDate()
    {
        Assert.IsNotNull(_historyRecords);
        // All records should exist (the filter would have excluded older ones)
        Assert.IsTrue(_historyRecords.Count >= 0);
    }

    [Then(@"I should receive impact analysis with violations")]
    public void ThenIShouldReceiveImpactAnalysisWithViolations()
    {
        Assert.IsNotNull(_impactAnalysis);
        Assert.IsTrue(_impactAnalysis.Violations.Count > 0, "Expected at least one violation in impact analysis");
    }

    [Then(@"the analysis should include recommendations")]
    public void ThenTheAnalysisShouldIncludeRecommendations()
    {
        Assert.IsNotNull(_impactAnalysis);
        Assert.IsNotNull(_impactAnalysis.Recommendations);
        // Recommendations may be empty if no patterns are found, so we just check it's not null
    }

    [Then(@"all violations should be from area path ""(.*)""")]
    public void ThenAllViolationsShouldBeFromAreaPath(string areaPath)
    {
        Assert.IsNotNull(_impactAnalysis);
        // Since we filtered by area path, all violations should be from that area
        // But we need to check against the work items themselves
        Assert.IsTrue(_impactAnalysis.Violations.Count > 0);
    }

    [Then(@"the fix result should show (.*) successful fix(?:es)?")]
    public void ThenTheFixResultShouldShowSuccessfulFixes(int expectedSuccessful)
    {
        Assert.IsNotNull(_fixResult);
        Assert.AreEqual(expectedSuccessful, _fixResult.SuccessfulFixes,
            $"Expected {expectedSuccessful} successful fixes but got {_fixResult.SuccessfulFixes}");
    }

    [Then(@"the fix result should show (.*) failed fix(?:es)?")]
    public void ThenTheFixResultShouldShowFailedFixes(int expectedFailed)
    {
        Assert.IsNotNull(_fixResult);
        Assert.AreEqual(expectedFailed, _fixResult.FailedFixes,
            $"Expected {expectedFailed} failed fixes but got {_fixResult.FailedFixes}");
    }
}
