using System.Net;
using System.Net.Http.Json;
using PoTool.Core.Settings;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class ProfilesControllerSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private ProfileDto? _createdProfile;
    private ProfileDto? _returnedProfile;
    private List<ProfileDto>? _profilesList;
    private int? _currentProfileId;

    public ProfilesControllerSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _factory = new IntegrationTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [When(@"I request all profiles from ""(.*)""")]
    public async Task WhenIRequestAllProfilesFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;

        if (_response.IsSuccessStatusCode)
        {
            _profilesList = await _response.Content.ReadFromJsonAsync<List<ProfileDto>>();
        }
    }

    [When(@"I create a profile with name ""(.*)"" and area paths ""(.*)""")]
    public async Task WhenICreateAProfileWithNameAndAreaPaths(string name, string areaPaths)
    {
        var areaPathList = string.IsNullOrEmpty(areaPaths)
            ? new List<string>()
            : areaPaths.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

        var request = new
        {
            Name = name,
            AreaPaths = areaPathList,
            TeamName = string.Empty,
            GoalIds = new List<int>()
        };

        _response = await _client.PostAsJsonAsync("/api/profiles", request);
        _scenarioContext["Response"] = _response;

        if (_response.IsSuccessStatusCode)
        {
            _createdProfile = await _response.Content.ReadFromJsonAsync<ProfileDto>();
            _currentProfileId = _createdProfile?.Id;
        }
    }

    [When(@"I create a profile with name ""(.*)"" team ""(.*)"" and goals ""(.*)""")]
    public async Task WhenICreateAProfileWithNameTeamAndGoals(string name, string teamName, string goals)
    {
        var goalIds = string.IsNullOrEmpty(goals)
            ? new List<int>()
            : goals.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

        var request = new
        {
            Name = name,
            AreaPaths = new List<string>(),
            TeamName = teamName,
            GoalIds = goalIds
        };

        _response = await _client.PostAsJsonAsync("/api/profiles", request);
        _scenarioContext["Response"] = _response;

        if (_response.IsSuccessStatusCode)
        {
            _createdProfile = await _response.Content.ReadFromJsonAsync<ProfileDto>();
            _currentProfileId = _createdProfile?.Id;
        }
    }

    [When(@"I create a profile with name ""(.*)"" and empty area paths")]
    public async Task WhenICreateAProfileWithNameAndEmptyAreaPaths(string name)
    {
        await WhenICreateAProfileWithNameAndAreaPaths(name, string.Empty);
    }

    [Given(@"a profile exists with name ""(.*)"" and area paths ""(.*)""")]
    public async Task GivenAProfileExistsWithNameAndAreaPaths(string name, string areaPaths)
    {
        await WhenICreateAProfileWithNameAndAreaPaths(name, areaPaths);
        Assert.IsNotNull(_createdProfile);
        _currentProfileId = _createdProfile.Id;
    }

    [When(@"I request the profile by its ID")]
    public async Task WhenIRequestTheProfileByItsID()
    {
        Assert.IsNotNull(_currentProfileId);
        _response = await _client.GetAsync($"/api/profiles/{_currentProfileId}");
        _scenarioContext["Response"] = _response;

        if (_response.IsSuccessStatusCode)
        {
            _returnedProfile = await _response.Content.ReadFromJsonAsync<ProfileDto>();
        }
    }

    [When(@"I update the profile with name ""(.*)"" and area paths ""(.*)""")]
    public async Task WhenIUpdateTheProfileWithNameAndAreaPaths(string name, string areaPaths)
    {
        Assert.IsNotNull(_currentProfileId);

        var areaPathList = string.IsNullOrEmpty(areaPaths)
            ? new List<string>()
            : areaPaths.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

        var request = new
        {
            ProfileId = _currentProfileId.Value,
            Name = name,
            AreaPaths = areaPathList,
            TeamName = string.Empty,
            GoalIds = new List<int>()
        };

        _response = await _client.PutAsJsonAsync($"/api/profiles/{_currentProfileId}", request);
        _scenarioContext["Response"] = _response;

        if (_response.IsSuccessStatusCode)
        {
            _createdProfile = await _response.Content.ReadFromJsonAsync<ProfileDto>();
        }
    }

    [When(@"I update the profile with area paths ""(.*)""")]
    public async Task WhenIUpdateTheProfileWithAreaPaths(string areaPaths)
    {
        Assert.IsNotNull(_currentProfileId);
        Assert.IsNotNull(_createdProfile);

        await WhenIUpdateTheProfileWithNameAndAreaPaths(_createdProfile.Name, areaPaths);
    }

    [When(@"I delete the profile by its ID")]
    public async Task WhenIDeleteTheProfileByItsID()
    {
        Assert.IsNotNull(_currentProfileId);
        _response = await _client.DeleteAsync($"/api/profiles/{_currentProfileId}");
        _scenarioContext["Response"] = _response;
    }

    [When(@"I set the profile as active")]
    [Given(@"the profile is set as active")]
    public async Task WhenISetTheProfileAsActive()
    {
        Assert.IsNotNull(_currentProfileId);
        
        var request = new { ProfileId = _currentProfileId };
        _response = await _client.PostAsJsonAsync("/api/profiles/active", request);
        
        if (_response.StatusCode != HttpStatusCode.OK)
        {
            _scenarioContext["Response"] = _response;
        }
    }

    [When(@"I request the active profile from ""(.*)""")]
    public async Task WhenIRequestTheActiveProfileFrom(string endpoint)
    {
        _response = await _client.GetAsync(endpoint);
        _scenarioContext["Response"] = _response;

        if (_response.IsSuccessStatusCode)
        {
            _returnedProfile = await _response.Content.ReadFromJsonAsync<ProfileDto>();
        }
    }

    [Then(@"the profiles list should be empty")]
    public void ThenTheProfilesListShouldBeEmpty()
    {
        Assert.IsNotNull(_profilesList);
        Assert.AreEqual(0, _profilesList.Count);
    }

    [Then(@"the profiles list should contain (.*) profiles")]
    public void ThenTheProfilesListShouldContainProfiles(int count)
    {
        Assert.IsNotNull(_profilesList);
        Assert.AreEqual(count, _profilesList.Count);
    }

    [Then(@"the created profile should have name ""(.*)""")]
    public void ThenTheCreatedProfileShouldHaveName(string name)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.AreEqual(name, _createdProfile.Name);
    }

    [Then(@"the created profile should have (.*) area paths")]
    public void ThenTheCreatedProfileShouldHaveAreaPaths(int count)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.AreEqual(count, _createdProfile.AreaPaths.Count);
    }

    [Then(@"the created profile should have team name ""(.*)""")]
    public void ThenTheCreatedProfileShouldHaveTeamName(string teamName)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.AreEqual(teamName, _createdProfile.TeamName);
    }

    [Then(@"the created profile should have (.*) goal IDs")]
    public void ThenTheCreatedProfileShouldHaveGoalIDs(int count)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.AreEqual(count, _createdProfile.GoalIds.Count);
    }

    [Then(@"the returned profile should have name ""(.*)""")]
    public void ThenTheReturnedProfileShouldHaveName(string name)
    {
        Assert.IsNotNull(_returnedProfile);
        Assert.AreEqual(name, _returnedProfile.Name);
    }

    [Then(@"the updated profile should have name ""(.*)""")]
    public void ThenTheUpdatedProfileShouldHaveName(string name)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.AreEqual(name, _createdProfile.Name);
    }

    [Then(@"the updated profile should have area path ""(.*)""")]
    public void ThenTheUpdatedProfileShouldHaveAreaPath(string areaPath)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.IsTrue(_createdProfile.AreaPaths.Contains(areaPath));
    }

    [Then(@"the updated profile should have (.*) area paths")]
    public void ThenTheUpdatedProfileShouldHaveAreaPaths(int count)
    {
        Assert.IsNotNull(_createdProfile);
        Assert.AreEqual(count, _createdProfile.AreaPaths.Count);
    }

    [Then(@"the profile should not exist anymore")]
    public async Task ThenTheProfileShouldNotExistAnymore()
    {
        Assert.IsNotNull(_currentProfileId);
        var checkResponse = await _client.GetAsync($"/api/profiles/{_currentProfileId}");
        Assert.AreEqual(HttpStatusCode.NotFound, checkResponse.StatusCode);
    }

    [Then(@"the settings should have the active profile ID set")]
    public async Task ThenTheSettingsShouldHaveTheActiveProfileIDSet()
    {
        var settingsResponse = await _client.GetAsync("/api/settings");
        Assert.AreEqual(HttpStatusCode.OK, settingsResponse.StatusCode);
        
        var settings = await settingsResponse.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.IsNotNull(settings);
        Assert.IsNotNull(settings.ActiveProfileId);
        Assert.AreEqual(_currentProfileId, settings.ActiveProfileId);
    }

    [Then(@"the area paths should be hierarchical")]
    public void ThenTheAreaPathsShouldBeHierarchical()
    {
        Assert.IsNotNull(_createdProfile);
        Assert.IsTrue(_createdProfile.AreaPaths.Count > 0);
        
        // Check that at least one path is a parent of another
        var hasHierarchy = false;
        for (int i = 0; i < _createdProfile.AreaPaths.Count; i++)
        {
            for (int j = 0; j < _createdProfile.AreaPaths.Count; j++)
            {
                if (i != j && _createdProfile.AreaPaths[j].StartsWith(_createdProfile.AreaPaths[i] + "\\"))
                {
                    hasHierarchy = true;
                    break;
                }
            }
            if (hasHierarchy) break;
        }
        
        Assert.IsTrue(hasHierarchy, "Area paths should have hierarchical relationship");
    }
}
