using System.Net;
using System.Net.Http.Json;
using PoTool.Core.Settings;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class SettingsSteps
{
    private readonly SharedTestState _context;
    
    private UpdateSettingsRequest? _settingsRequest;
    private PoTool.Core.Settings.SettingsDto? _returnedSettings;

    public SettingsSteps(SharedTestState context)
    {
        _context = context;
    }

    [Given(@"I have settings to update")]
    public void GivenIHaveSettingsToUpdate(Table table)
    {
        var dataModeStr = table.Rows[0]["Value"];
        var goalIdsStr = table.Rows[1]["Value"];
        var goalIds = goalIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();

        // Map "Live" to "Tfs" for the enum
        var dataMode = dataModeStr == "Live" ? DataMode.Tfs : Enum.Parse<DataMode>(dataModeStr, ignoreCase: true);
        
        _settingsRequest = new UpdateSettingsRequest(dataMode, goalIds);
    }

    [Given(@"I have updated the settings with DataMode ""(.*)"" and GoalIds ""(.*)""")]
    public async Task GivenIHaveUpdatedTheSettings(string dataModeStr, string goalIds)
    {
        var goalIdList = goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();

        // Map "Live" to "Tfs" for the enum
        var dataMode = dataModeStr == "Live" ? DataMode.Tfs : Enum.Parse<DataMode>(dataModeStr, ignoreCase: true);
        
        _settingsRequest = new UpdateSettingsRequest(dataMode, goalIdList);

        _context.Response = await _context.Client.PutAsJsonAsync("/api/settings", _settingsRequest);
        _context.Response.EnsureSuccessStatusCode();
    }

    [When(@"I request the application settings")]
    public async Task WhenIRequestTheApplicationSettings()
    {
        _context.Response = await _context.Client.GetAsync("/api/settings");
        
        if (_context.Response.StatusCode == HttpStatusCode.OK)
        {
            _returnedSettings = await _context.Response.Content.ReadFromJsonAsync<PoTool.Core.Settings.SettingsDto>();
        }
    }

    [When(@"I update the application settings")]
    public async Task WhenIUpdateTheApplicationSettings()
    {
        _context.Response = await _context.Client.PutAsJsonAsync("/api/settings", _settingsRequest);
    }

    [Then(@"the settings should be updated successfully")]
    public void ThenTheSettingsShouldBeUpdatedSuccessfully()
    {
        Assert.IsNotNull(_context.Response);
        Assert.IsTrue(_context.Response.IsSuccessStatusCode);
    }

    [Then(@"the returned settings should have DataMode ""(.*)""")]
    public void ThenTheReturnedSettingsShouldHaveDataMode(string expectedDataMode)
    {
        Assert.IsNotNull(_returnedSettings);
        // Map "Live" to "Tfs" for comparison
        var expectedMode = expectedDataMode == "Live" ? DataMode.Tfs : Enum.Parse<DataMode>(expectedDataMode, ignoreCase: true);
        Assert.AreEqual(expectedMode, _returnedSettings.DataMode);
    }

    [Then(@"the returned settings should have (\d+) goal IDs")]
    public void ThenTheReturnedSettingsShouldHaveGoalIds(int expectedCount)
    {
        Assert.IsNotNull(_returnedSettings);
        Assert.AreEqual(expectedCount, _returnedSettings.ConfiguredGoalIds.Count);
    }
}

public record UpdateSettingsRequest(
    DataMode DataMode,
    List<int> ConfiguredGoalIds
);
