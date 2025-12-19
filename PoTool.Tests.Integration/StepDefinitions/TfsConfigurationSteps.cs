using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class TfsConfigurationSteps
{
    private readonly SharedTestState _context;
    private TfsConfigDto? _savedConfig;
    private TfsConfigDto? _returnedConfig;

    public TfsConfigurationSteps(SharedTestState context)
    {
        _context = context;
    }

    [Given(@"the application is running")]
    public void GivenTheApplicationIsRunning()
    {
        // Application is already running via the factory
        Assert.IsNotNull(_context.Client);
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

        _context.Response = await _context.Client.PostAsJsonAsync("/api/tfsconfig", _savedConfig);
        _context.Response.EnsureSuccessStatusCode();
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

        _context.Response = await _context.Client.PostAsJsonAsync("/api/tfsconfig", _savedConfig);
        _context.Response.EnsureSuccessStatusCode();
    }

    [When(@"I request the TFS configuration")]
    public async Task WhenIRequestTheTfsConfiguration()
    {
        _context.Response = await _context.Client.GetAsync("/api/tfsconfig");
        
        if (_context.Response.StatusCode == HttpStatusCode.OK)
        {
            _returnedConfig = await _context.Response.Content.ReadFromJsonAsync<TfsConfigDto>();
        }
    }

    [When(@"I save the TFS configuration")]
    public async Task WhenISaveTheTfsConfiguration()
    {
        _context.Response = await _context.Client.PostAsJsonAsync("/api/tfsconfig", _savedConfig);
    }

    [When(@"I validate the TFS connection")]
    public async Task WhenIValidateTheTfsConnection()
    {
        _context.Response = await _context.Client.GetAsync("/api/tfsvalidate");
    }

    [Then(@"the response should be (.*)")]
    public void ThenTheResponseShouldBe(string expectedStatus)
    {
        Assert.IsNotNull(_context.Response);
        
        var expected = Enum.Parse<HttpStatusCode>(expectedStatus);
        Assert.AreEqual(expected, _context.Response.StatusCode, 
            $"Expected {expected} but got {_context.Response.StatusCode}");
    }

    [Then(@"the configuration should be saved successfully")]
    public void ThenTheConfigurationShouldBeSavedSuccessfully()
    {
        Assert.IsNotNull(_context.Response);
        Assert.IsTrue(_context.Response.IsSuccessStatusCode);
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
        Assert.IsNotNull(_context.Response);
        Assert.IsTrue(_context.Response.IsSuccessStatusCode);
    }

    private class TfsConfigDto
    {
        public string? Url { get; set; }
        public string? Project { get; set; }
        public string? Pat { get; set; }
    }
}
