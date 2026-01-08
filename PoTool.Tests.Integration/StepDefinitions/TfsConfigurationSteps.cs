using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class TfsConfigurationSteps
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ScenarioContext _scenarioContext;
    private HttpResponseMessage? _response;
    private TfsConfigDto? _savedConfig;
    private TfsConfigDto? _returnedConfig;

    public TfsConfigurationSteps(ScenarioContext scenarioContext, SharedTestContext sharedContext)
    {
        _scenarioContext = scenarioContext;
        _factory = sharedContext.Factory;
        _client = _factory.CreateClient();
    }

    [Given(@"I have valid TFS credentials")]
    public void GivenIHaveValidTfsCredentials(Table table)
    {
        _savedConfig = new TfsConfigDto
        {
            Url = table.Rows[0]["Value"],
            Project = table.Rows[1]["Value"],
            Pat = table.Rows[2]["Value"]
        };
    }

    [Given(@"I have saved TFS configuration")]
    public async Task GivenIHaveSavedTfsConfiguration(Table table)
    {
        _savedConfig = new TfsConfigDto
        {
            Url = table.Rows[0]["Value"],
            Project = table.Rows[1]["Value"],
            Pat = "test-pat-token"
        };

        _response = await _client.PostAsJsonAsync("/api/tfsconfig", _savedConfig);
        _response.EnsureSuccessStatusCode();
    }

    [Given(@"I have saved valid TFS configuration")]
    public async Task GivenIHaveSavedValidTfsConfiguration()
    {
        _savedConfig = new TfsConfigDto
        {
            Url = "https://dev.azure.com/testorg",
            Project = "TestProject",
            Pat = "test-pat-token"
        };

        _response = await _client.PostAsJsonAsync("/api/tfsconfig", _savedConfig);
        _response.EnsureSuccessStatusCode();
    }

    [When(@"I request the TFS configuration")]
    public async Task WhenIRequestTheTfsConfiguration()
    {
        _response = await _client.GetAsync("/api/tfsconfig");
        _scenarioContext["Response"] = _response;
        
        if (_response.StatusCode == HttpStatusCode.OK)
        {
            _returnedConfig = await _response.Content.ReadFromJsonAsync<TfsConfigDto>();
        }
    }

    [When(@"I save the TFS configuration")]
    public async Task WhenISaveTheTfsConfiguration()
    {
        _response = await _client.PostAsJsonAsync("/api/tfsconfig", _savedConfig);
        _scenarioContext["Response"] = _response;
    }

    [When(@"I validate the TFS connection")]
    public async Task WhenIValidateTheTfsConnection()
    {
        _response = await _client.GetAsync("/api/tfsvalidate");
        _scenarioContext["Response"] = _response;
    }

    [Then(@"the configuration should be saved successfully")]
    public void ThenTheConfigurationShouldBeSavedSuccessfully()
    {
        Assert.IsNotNull(_response);
        Assert.IsTrue(_response.IsSuccessStatusCode);
    }

    [Then(@"the returned configuration should match")]
    public void ThenTheReturnedConfigurationShouldMatch(Table table)
    {
        Assert.IsNotNull(_returnedConfig);
        Assert.AreEqual(table.Rows[0]["Value"], _returnedConfig.Url);
        Assert.AreEqual(table.Rows[1]["Value"], _returnedConfig.Project);
    }

    [Then(@"the validation should succeed")]
    public void ThenTheValidationShouldSucceed()
    {
        Assert.IsNotNull(_response);
        Assert.IsTrue(_response.IsSuccessStatusCode);
    }

    private class TfsConfigDto
    {
        public string? Url { get; set; }
        public string? Project { get; set; }
        public string? Pat { get; set; }
    }
}
