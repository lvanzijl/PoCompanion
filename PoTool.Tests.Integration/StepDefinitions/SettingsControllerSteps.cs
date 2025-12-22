using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Settings;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class SettingsControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private SettingsDto? _settings;

    public SettingsControllerSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Given(@"settings exist in the database")]
    public async Task GivenSettingsExistInTheDatabase(Table table)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var row = table.Rows[0];
        var dataMode = Enum.Parse<DataMode>(row["DataMode"]);
        var goalIdsStr = row["ConfiguredGoalIds"];

        var settings = new SettingsEntity
        {
            Id = 1,
            DataMode = dataMode,
            ConfiguredGoalIds = goalIdsStr,
            LastModified = DateTimeOffset.UtcNow
        };

        dbContext.Settings.Add(settings);
        await dbContext.SaveChangesAsync();
    }

    [When(@"I request settings from ""(.*)""")]
    public async Task WhenIRequestSettingsFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        if (_response.IsSuccessStatusCode)
        {
            _settings = await _response.Content.ReadFromJsonAsync<SettingsDto>();
        }
    }

    [When(@"I update settings with DataMode ""(.*)"" and goal IDs ""(.*)""")]
    public async Task WhenIUpdateSettingsWithDataModeAndGoalIDs(string dataMode, string goalIds)
    {
        var goalIdList = string.IsNullOrWhiteSpace(goalIds)
            ? new List<int>()
            : goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

        var request = new
        {
            DataMode = Enum.Parse<DataMode>(dataMode),
            ConfiguredGoalIds = goalIdList
        };

        _response = await _client.PutAsJsonAsync("/api/settings", request);
        if (_response.IsSuccessStatusCode)
        {
            _settings = await _response.Content.ReadFromJsonAsync<SettingsDto>();
        }
    }

    [When(@"I update settings with DataMode ""(.*)"" and empty goal IDs")]
    public async Task WhenIUpdateSettingsWithDataModeAndEmptyGoalIDs(string dataMode)
    {
        var request = new
        {
            DataMode = Enum.Parse<DataMode>(dataMode),
            ConfiguredGoalIds = new List<int>()
        };

        _response = await _client.PutAsJsonAsync("/api/settings", request);
        if (_response.IsSuccessStatusCode)
        {
            _settings = await _response.Content.ReadFromJsonAsync<SettingsDto>();
        }
    }

    [Then(@"the response should be OK")]
    public void ThenTheResponseShouldBeOK()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
    }

    [Then(@"the response should be NotFound")]
    public void ThenTheResponseShouldBeNotFound()
    {
        Assert.IsNotNull(_response);
        Assert.AreEqual(HttpStatusCode.NotFound, _response.StatusCode);
    }

    [Then(@"the settings should have DataMode ""(.*)""")]
    public void ThenTheSettingsShouldHaveDataMode(string dataMode)
    {
        Assert.IsNotNull(_settings);
        Assert.AreEqual(Enum.Parse<DataMode>(dataMode), _settings.DataMode);
    }

    [Then(@"the settings should have goal IDs ""(.*)""")]
    public void ThenTheSettingsShouldHaveGoalIDs(string goalIds)
    {
        Assert.IsNotNull(_settings);
        var expectedIds = goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();
        CollectionAssert.AreEqual(expectedIds, _settings.ConfiguredGoalIds);
    }

    [Then(@"the updated settings should have DataMode ""(.*)""")]
    public void ThenTheUpdatedSettingsShouldHaveDataMode(string dataMode)
    {
        Assert.IsNotNull(_settings);
        Assert.AreEqual(Enum.Parse<DataMode>(dataMode), _settings.DataMode);
    }

    [Then(@"the updated settings should have goal IDs ""(.*)""")]
    public void ThenTheUpdatedSettingsShouldHaveGoalIDs(string goalIds)
    {
        Assert.IsNotNull(_settings);
        var expectedIds = goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();
        CollectionAssert.AreEqual(expectedIds, _settings.ConfiguredGoalIds);
    }
}
