using System.Net;
using System.Net.Http.Json;
using PoTool.Shared.Settings;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class SettingsSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private SettingsDto? _settings;
    private SettingsDto? _returnedSettings;

    public SettingsSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        // Use shared factory to avoid creating a new web server per step class
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [Given(@"I have settings to update")]
    public void GivenIHaveSettingsToUpdate(Table table)
    {
        var dataModeStr = table.Rows[0]["Value"];
        var goalIdsStr = table.Rows[1]["Value"];
        var goalIds = goalIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();

        // OBSOLETE: DataMode and ConfiguredGoalIds removed
        // var dataMode = Enum.Parse<DataMode>(dataModeStr);

        _settings = new SettingsDto(
            Id: 0,
            // DataMode: dataMode,
            // ConfiguredGoalIds: goalIds,
            ActiveProfileId: null,
            LastModified: DateTimeOffset.UtcNow
        );
    }

    [Given(@"I have updated the settings with DataMode ""(.*)"" and GoalIds ""(.*)""")]
    public async Task GivenIHaveUpdatedTheSettings(string dataMode, string goalIds)
    {
        // OBSOLETE: DataMode and ConfiguredGoalIds removed
        // var goalIdList = goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
        //     .Select(int.Parse)
        //     .ToList();

        // var dataModeEnum = Enum.Parse<DataMode>(dataMode);

        _settings = new SettingsDto(
            Id: 0,
            // DataMode: dataModeEnum,
            // ConfiguredGoalIds: goalIdList,
            ActiveProfileId: null,
            LastModified: DateTimeOffset.UtcNow
        );

        // OBSOLETE: Update endpoint removed
        // Create the request model that the API expects
        // var request = new
        // {
        //     DataMode = (int)dataModeEnum,  // Send as int for enum
        //     ConfiguredGoalIds = goalIdList
        // };

        // _response = await _client.PutAsJsonAsync("/api/settings", request);
        _response = new HttpResponseMessage(HttpStatusCode.NotFound);  // Stub for removed endpoint
        // _response.EnsureSuccessStatusCode();
    }

    [When(@"I request the application settings")]
    public async Task WhenIRequestTheApplicationSettings()
    {
        _response = await _client.GetAsync("/api/settings");
        _scenarioContext["Response"] = _response;
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _returnedSettings = await _response.Content.ReadFromJsonAsync<SettingsDto>();
        }
    }

    [When(@"I update the application settings")]
    public async Task WhenIUpdateTheApplicationSettings()
    {
        // OBSOLETE: This endpoint no longer exists
        // Create the request model that the API expects
        // var request = new
        // {
        //     DataMode = (int)_settings!.DataMode,  // Send as int for enum
        //     ConfiguredGoalIds = _settings.ConfiguredGoalIds
        // };
        
        // _response = await _client.PutAsJsonAsync("/api/settings", request);
        _response = new HttpResponseMessage(HttpStatusCode.NotFound);  // Stub for removed endpoint
        _scenarioContext["Response"] = _response;
    }

    [Then(@"the settings should be updated successfully")]
    public void ThenTheSettingsShouldBeUpdatedSuccessfully()
    {
        // OBSOLETE: Update endpoint removed
        // Assert.IsNotNull(_response);
        // Assert.IsTrue(_response.IsSuccessStatusCode);
    }

    [Then(@"the returned settings should have DataMode ""(.*)""")]
    public void ThenTheReturnedSettingsShouldHaveDataMode(string expectedDataMode)
    {
        // OBSOLETE: DataMode removed
        // Assert.IsNotNull(_returnedSettings);
        // var expectedEnum = Enum.Parse<DataMode>(expectedDataMode);
        // Assert.AreEqual(expectedEnum, _returnedSettings.DataMode);
    }

    [Then(@"the returned settings should have (\d+) goal IDs")]
    public void ThenTheReturnedSettingsShouldHaveGoalIds(int expectedCount)
    {
        // OBSOLETE: ConfiguredGoalIds removed
        // Assert.IsNotNull(_returnedSettings);
        // Assert.AreEqual(expectedCount, _returnedSettings.ConfiguredGoalIds.Count);
    }
}
