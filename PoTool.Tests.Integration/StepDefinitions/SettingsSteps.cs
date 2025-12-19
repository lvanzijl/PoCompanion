using System.Net;
using System.Net.Http.Json;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class SettingsSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HttpResponseMessage? _response;
    private SettingsDto? _settings;
    private SettingsDto? _returnedSettings;

    public SettingsSteps()
    {
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Given(@"I have settings to update")]
    public void GivenIHaveSettingsToUpdate(Table table)
    {
        var dataMode = table.Rows[0]["Value"];
        var goalIdsStr = table.Rows[1]["Value"];
        var goalIds = goalIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();

        _settings = new SettingsDto
        {
            DataMode = dataMode,
            ConfiguredGoalIds = goalIds
        };
    }

    [Given(@"I have updated the settings with DataMode ""(.*)"" and GoalIds ""(.*)""")]
    public async Task GivenIHaveUpdatedTheSettings(string dataMode, string goalIds)
    {
        var goalIdList = goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();

        _settings = new SettingsDto
        {
            DataMode = dataMode,
            ConfiguredGoalIds = goalIdList
        };

        _response = await _client.PutAsJsonAsync("/api/settings", _settings);
        _response.EnsureSuccessStatusCode();
    }

    [When(@"I request the application settings")]
    public async Task WhenIRequestTheApplicationSettings()
    {
        _response = await _client.GetAsync("/api/settings");
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _returnedSettings = await _response.Content.ReadFromJsonAsync<SettingsDto>();
        }
    }

    [When(@"I update the application settings")]
    public async Task WhenIUpdateTheApplicationSettings()
    {
        _response = await _client.PutAsJsonAsync("/api/settings", _settings);
    }

    [Then(@"the settings should be updated successfully")]
    public void ThenTheSettingsShouldBeUpdatedSuccessfully()
    {
        Assert.IsNotNull(_response);
        Assert.IsTrue(_response.IsSuccessStatusCode);
    }

    [Then(@"the returned settings should have DataMode ""(.*)""")]
    public void ThenTheReturnedSettingsShouldHaveDataMode(string expectedDataMode)
    {
        Assert.IsNotNull(_returnedSettings);
        Assert.AreEqual(expectedDataMode, _returnedSettings.DataMode);
    }

    [Then(@"the returned settings should have (\d+) goal IDs")]
    public void ThenTheReturnedSettingsShouldHaveGoalIds(int expectedCount)
    {
        Assert.IsNotNull(_returnedSettings);
        Assert.AreEqual(expectedCount, _returnedSettings.ConfiguredGoalIds.Count);
    }

    private class SettingsDto
    {
        public string DataMode { get; set; } = string.Empty;
        public List<int> ConfiguredGoalIds { get; set; } = new();
    }
}
