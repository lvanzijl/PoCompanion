# TFS Integration - Quick Reference

**Quick Start Guide for Developers**

---

## Current Implementation Status

### ✅ Implemented
- Work Items: Basic retrieval
- PAT Authentication
- Configuration: TfsConfigurationService
- Tests: MockTfsClient, TfsClientTests

### ❌ Not Implemented (Stubs Only)
- Pull Requests: All methods
- NTLM Authentication
- Retry Logic
- Incremental Sync
- Error Recovery

---

## API Endpoints Reference

### Work Items

**Get Work Items by Area Path:**
```csharp
// WIQL Query
POST {tfsUrl}/_apis/wit/wiql?api-version=7.0
Body: { "query": "Select [System.Id] From WorkItems Where [System.AreaPath] = '...' " }

// Batch Get
GET {tfsUrl}/_apis/wit/workitems?ids={ids}&$expand=All&api-version=7.0
```

### Pull Requests

**List Pull Requests:**
```http
GET {tfsUrl}/{project}/_apis/git/repositories/{repo}/pullrequests?api-version=7.0
Query Params:
  - searchCriteria.status: Active|Completed|Abandoned|All
  - searchCriteria.createdBy: {author}
  - $top: 100 (max per page)
  - $skip: 0 (pagination offset)
```

**Get PR Iterations:**
```http
GET {tfsUrl}/{project}/_apis/git/repositories/{repo}/pullrequests/{prId}/iterations?api-version=7.0
```

**Get PR Comments:**
```http
GET {tfsUrl}/{project}/_apis/git/repositories/{repo}/pullrequests/{prId}/threads?api-version=7.0
```

**Get PR File Changes:**
```http
GET {tfsUrl}/{project}/_apis/git/repositories/{repo}/pullrequests/{prId}/iterations/{iterationId}/changes?api-version=7.0
```

---

## Authentication

### PAT (Personal Access Token)
```csharp
var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{pat}"));
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
```

### NTLM (Windows Auth) - NOT YET IMPLEMENTED
```csharp
var handler = new HttpClientHandler
{
    UseDefaultCredentials = true,
    Credentials = CredentialCache.DefaultNetworkCredentials
};
var httpClient = new HttpClient(handler);
```

---

## Error Handling Patterns

### Current (Basic)
```csharp
try
{
    var response = await httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();
    // ... process
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "TFS request failed");
    throw;
}
```

### Planned (Enhanced)
```csharp
try
{
    return await ExecuteWithRetryAsync(async () =>
    {
        var response = await httpClient.GetAsync(url);
        return await HandleHttpErrorsAsync<T>(response);
    });
}
catch (TfsAuthenticationException ex)
{
    _logger.LogError(ex, "TFS authentication failed - check PAT");
    throw;
}
catch (TfsRateLimitException ex)
{
    _logger.LogWarning(ex, "TFS rate limit exceeded - backing off");
    throw;
}
```

---

## Testing Patterns

### Unit Tests (MSTest)
```csharp
[TestMethod]
public async Task GetPullRequestsAsync_ParsesResponse_Correctly()
{
    // Arrange
    var mockResponse = LoadTestData("pull-requests-response.json");
    SetupHttpResponse(HttpStatusCode.OK, mockResponse);
    
    // Act
    var result = await _tfsClient.GetPullRequestsAsync("MyRepo");
    
    // Assert
    Assert.AreEqual(2, result.Count());
    Assert.AreEqual("Add feature X", result.First().Title);
}

private void SetupHttpResponse(HttpStatusCode code, string content)
{
    _httpMessageHandlerMock
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = code,
            Content = new StringContent(content)
        });
}
```

### Integration Tests (Reqnroll)
```gherkin
Scenario: Sync pull requests successfully
  Given TFS is configured with valid credentials
  When I request to sync pull requests
  Then pull requests are retrieved from TFS
  And pull requests are stored in the database
```

```csharp
[Given(@"TFS is configured with valid credentials")]
public async Task GivenTfsIsConfigured()
{
    // Use MockTfsClient - no real TFS calls
    _mockTfsClient.LoadTestData("pull-requests-response.json");
}
```

---

## Common Tasks

### Add a New TFS API Endpoint

1. **Define method in ITfsClient:**
```csharp
public interface ITfsClient
{
    Task<IEnumerable<MyDto>> GetMyDataAsync(CancellationToken ct = default);
}
```

2. **Implement in TfsClient:**
```csharp
public async Task<IEnumerable<MyDto>> GetMyDataAsync(CancellationToken ct = default)
{
    var config = await _configService.GetConfigEntityAsync(ct);
    var pat = _configService.UnprotectPatEntity(config);
    
    SetAuthentication(pat);
    
    var url = $"{config.Url}/_apis/myendpoint?api-version=7.0";
    var response = await _httpClient.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    
    var json = await response.Content.ReadAsStringAsync(ct);
    var data = JsonSerializer.Deserialize<MyResponse>(json);
    
    return data.Value.Select(item => new MyDto(...));
}
```

3. **Implement in MockTfsClient:**
```csharp
public Task<IEnumerable<MyDto>> GetMyDataAsync(CancellationToken ct = default)
{
    // Return mock data
    return Task.FromResult(_mockData.AsEnumerable());
}
```

4. **Add unit tests:**
```csharp
[TestMethod]
public async Task GetMyDataAsync_ReturnsData()
{
    // Test implementation
}
```

### Add Retry Logic to a Method

```csharp
private async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    CancellationToken ct = default)
{
    int attempt = 0;
    
    while (true)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
        {
            attempt++;
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            _logger.LogWarning("Retry {Attempt}/{Max} after {Delay}s", 
                attempt, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay, ct);
        }
    }
}

// Use it:
return await ExecuteWithRetryAsync(async () => 
{
    var response = await _httpClient.GetAsync(url);
    return await ProcessResponse(response);
});
```

### Parse TFS JSON Response

```csharp
using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

var items = doc.RootElement
    .GetProperty("value")
    .EnumerateArray()
    .Select(item => new MyDto(
        Id: item.GetProperty("id").GetInt32(),
        Name: item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""
    ));
```

---

## Configuration

### Current Config
```csharp
public class TfsConfigEntity
{
    public string Url { get; set; }              // e.g., "https://tfs.company.com/DefaultCollection"
    public string Project { get; set; }          // e.g., "MyProject"
    // Note: PAT is no longer stored in database - stored client-side (see PAT_STORAGE_BEST_PRACTICES.md)
}
```

### Planned Enhanced Config
```csharp
public class TfsConfigEntity
{
    public string Url { get; set; }
    public string Project { get; set; }
    public TfsAuthMode AuthMode { get; set; }    // Pat or Ntlm
    // Note: PAT removed - stored client-side only (see PAT_STORAGE_BEST_PRACTICES.md)
    public bool UseDefaultCredentials { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiVersion { get; set; } = "7.0";
}

public enum TfsAuthMode { Pat, Ntlm }
```

---

## Useful Commands

### Run Unit Tests
```bash
cd PoTool.Tests.Unit
dotnet test --filter "TfsClient"
```

### Run Integration Tests
```bash
cd PoTool.Tests.Integration
dotnet test --filter "Category=TFS"
```

### Test Against Real TFS (Local Development Only)
```bash
# Update appsettings.Development.json
{
  "TfsConfig": {
    "Url": "https://your-tfs.company.com",
    "Project": "YourProject",
    "Pat": "your-pat-here"
  }
}

# Run API
cd PoTool.Api
dotnet run

# Test endpoints
curl http://localhost:5291/api/workitems
```

---

## Debugging Tips

### Enable Verbose HTTP Logging
```csharp
// In Program.cs
builder.Services.AddHttpClient<TfsClient>()
    .ConfigureHttpMessageHandlerBuilder(builder =>
    {
        builder.AdditionalHandlers.Add(new LoggingHttpMessageHandler(logger));
    });

public class LoggingHttpMessageHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        _logger.LogDebug("TFS Request: {Method} {Uri}", request.Method, request.RequestUri);
        var response = await base.SendAsync(request, ct);
        _logger.LogDebug("TFS Response: {StatusCode}", response.StatusCode);
        return response;
    }
}
```

### Inspect TFS JSON Responses
```csharp
var json = await response.Content.ReadAsStringAsync();
_logger.LogDebug("TFS Response JSON: {Json}", json);
File.WriteAllText($"/tmp/tfs-response-{DateTime.Now:yyyyMMdd-HHmmss}.json", json);
```

### Test Auth Quickly
```bash
# Test PAT
curl -u :YOUR_PAT https://tfs.company.com/_apis/projects?api-version=7.0

# Test NTLM (from domain-joined machine)
curl --ntlm -u : https://tfs.company.com/_apis/projects?api-version=7.0
```

---

## Common Issues

### 401 Unauthorized
- ❌ PAT is invalid or expired
- ❌ PAT lacks required permissions
- ✅ Generate new PAT with "Work Items (Read)" and "Code (Read)" scopes

### 403 Forbidden
- ❌ User lacks permissions in TFS project
- ✅ Grant user "Reader" or "Contributor" role in TFS

### 404 Not Found
- ❌ Project name or URL incorrect
- ❌ API version not supported by TFS version
- ✅ Verify URL: `{tfsUrl}/_apis/projects?api-version=7.0`

### Timeout
- ❌ TFS is slow or unreachable
- ❌ Large dataset without pagination
- ✅ Increase timeout in configuration
- ✅ Implement pagination

---

## Resources

**Official Docs:**
- [Azure DevOps REST API Reference](https://docs.microsoft.com/en-us/rest/api/azure/devops/)
- [Work Item Tracking API](https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/)
- [Git API (Pull Requests)](https://docs.microsoft.com/en-us/rest/api/azure/devops/git/)

**Repository Docs:**
- Full Plan: `docs/TFS_ONPREM_INTEGRATION_PLAN.md`
- Executive Summary: `docs/TFS_INTEGRATION_EXECUTIVE_SUMMARY.md`
- Architecture: `docs/ARCHITECTURE_RULES.md`

**Code Locations:**
- Interface: `PoTool.Core/Contracts/ITfsClient.cs`
- Implementation: `PoTool.Api/Services/TfsClient.cs`
- Tests: `PoTool.Tests.Unit/TfsClientTests.cs`
- Mock: `PoTool.Tests.Integration/Support/MockTfsClient.cs`

---

**Last Updated:** December 20, 2024  
**Maintainer:** Engineering Team
