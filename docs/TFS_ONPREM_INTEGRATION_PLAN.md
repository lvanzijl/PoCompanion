# Real On-Premises TFS Integration Plan

**Date:** December 20, 2024  
**Purpose:** Comprehensive plan for implementing real on-premises TFS integration for all features  
**Status:** Planning Phase

---

## Executive Summary

This document outlines the complete plan for implementing real on-premises Team Foundation Server (TFS) integration for the PO Companion application. The plan covers both Azure DevOps (cloud) and on-premises TFS Server, ensuring full feature parity across both deployment scenarios.

**Key Goals:**
1. Full TFS/Azure DevOps integration for all features (Work Items, Pull Requests)
2. Support for both cloud (Azure DevOps) and on-premises TFS
3. Robust authentication supporting PAT and NTLM
4. Comprehensive error handling and resilience
5. 100% test coverage with mock TFS data
6. Architecture compliance per `ARCHITECTURE_RULES.md`

---

## Current State Analysis

### Existing Implementation

**ITfsClient Interface** (`PoTool.Core/Contracts/ITfsClient.cs`):
- ✅ Work Items: `GetWorkItemsAsync()` - IMPLEMENTED
- ✅ Connection validation: `ValidateConnectionAsync()` - IMPLEMENTED
- ⚠️ Pull Requests: `GetPullRequestsAsync()` - STUB ONLY
- ⚠️ PR Iterations: `GetPullRequestIterationsAsync()` - STUB ONLY
- ⚠️ PR Comments: `GetPullRequestCommentsAsync()` - STUB ONLY
- ⚠️ PR File Changes: `GetPullRequestFileChangesAsync()` - STUB ONLY

**Current TfsClient Implementation** (`PoTool.Api/Services/TfsClient.cs`):
- Uses `HttpClient` with Azure DevOps REST API
- Basic authentication via PAT (Personal Access Token)
- Work Items: WIQL queries + batch retrieval with parent relationships
- Pull Requests: All methods return empty collections (not implemented)
- API Version: `7.0` (Azure DevOps Services / TFS 2022+)

**Authentication:**
- PAT storage: Client-side using browser secure storage (see `PAT_STORAGE_BEST_PRACTICES.md`)
- Configuration: TfsConfigurationService manages URL, Project (non-sensitive config only)
- PAT provided by client per request or per session
- No NTLM/Windows Authentication support yet

**Testing:**
- MockTfsClient for integration tests
- TfsClientTests with mocked HttpMessageHandler
- No real TFS calls in tests (architecture requirement)

### Gaps and Limitations

**Work Items:**
- ✅ Basic retrieval works
- ❌ No support for TFS 2015/2017 (older API versions)
- ❌ No batch error handling for large area paths
- ❌ No incremental sync (always full refresh)
- ❌ No work item mutations (create, update, delete)
- ❌ No attachment support
- ❌ No work item links/relations beyond parent

**Pull Requests:**
- ❌ No implementation (all methods are stubs)
- ❌ No PR metrics calculation
- ❌ No iteration timeline analysis
- ❌ No file change statistics
- ❌ No comment threading
- ❌ No PR status tracking

**Authentication:**
- ❌ No Windows Authentication (NTLM/Kerberos)
- ❌ No OAuth 2.0 support
- ❌ No multi-server configuration

**Resilience:**
- ❌ No retry policies
- ❌ No circuit breaker
- ❌ No rate limiting
- ❌ No timeout configuration

---

## TFS Version Support Matrix

### Target Versions

| TFS Version | API Version | Support Priority | Notes |
|------------|-------------|------------------|-------|
| Azure DevOps Services | 7.0, 7.1 | ⭐⭐⭐ High | Cloud, latest features |
| TFS 2022 | 7.0 | ⭐⭐⭐ High | Latest on-prem |
| TFS 2019 | 5.1 | ⭐⭐ Medium | Common on-prem |
| TFS 2018 | 4.1 | ⭐⭐ Medium | Common on-prem |
| TFS 2017 | 3.2 | ⭐ Low | Legacy |
| TFS 2015 | 2.2 | ⭐ Low | Legacy |

**Recommendation:** Focus on API versions 5.1+ (TFS 2019+) for initial implementation.

---

## Integration Approach: REST API vs SDK

### Option 1: Azure DevOps REST API (Current Approach)

**Pros:**
- ✅ No additional dependencies
- ✅ Works with all TFS versions
- ✅ Full control over HTTP calls
- ✅ Easy to mock in tests
- ✅ .NET 10 compatible

**Cons:**
- ❌ Manual JSON parsing
- ❌ Manual error handling
- ❌ Manual pagination
- ❌ No type safety

### Option 2: Azure DevOps SDK (Microsoft.TeamFoundationServer.Client)

**Pros:**
- ✅ Type-safe client libraries
- ✅ Built-in error handling
- ✅ Automatic pagination
- ✅ Well-documented

**Cons:**
- ❌ Large dependency footprint
- ❌ May not be .NET 10 compatible yet
- ❌ Harder to mock in tests
- ❌ Less control over HTTP behavior

### Option 3: Hybrid Approach

**Pros:**
- ✅ Use SDK models/types for type safety
- ✅ Use REST API for actual calls
- ✅ Best of both worlds

**Cons:**
- ❌ Two dependency sets

### **RECOMMENDATION: Continue with REST API Approach (Option 1)**

**Rationale:**
1. Architecture compliance: Easier to abstract and test
2. No additional dependencies (aligned with ARCHITECTURE_RULES.md)
3. Full control over HTTP behavior (retry, timeout, logging)
4. .NET 10 compatibility guaranteed
5. Already working for Work Items

**Enhancement:** Create strongly-typed response models in `PoTool.Core` to improve type safety.

---

## Authentication Strategy

### Current Authentication
- ✅ PAT (Personal Access Token) for Azure DevOps
- ❌ No Windows Authentication for on-prem TFS

### Required Authentication Methods

#### 1. Personal Access Token (PAT)
**Use Case:** Azure DevOps, TFS 2019+  
**Implementation:** Already implemented via Basic Auth  
**Enhancement:** None needed

#### 2. NTLM/Windows Authentication
**Use Case:** On-premises TFS with Windows Authentication  
**Implementation Required:**
```csharp
public class TfsAuthenticationProvider
{
    public HttpMessageHandler GetAuthHandler(TfsAuthMode mode, string? pat = null)
    {
        return mode switch
        {
            TfsAuthMode.Pat => new HttpClientHandler 
            { 
                Credentials = new NetworkCredential("", pat) 
            },
            TfsAuthMode.Ntlm => new HttpClientHandler 
            { 
                UseDefaultCredentials = true,
                Credentials = CredentialCache.DefaultNetworkCredentials
            },
            _ => throw new NotSupportedException()
        };
    }
}
```

#### 3. OAuth 2.0 (Future)
**Use Case:** Enhanced security for Azure DevOps  
**Priority:** Low (PAT is sufficient)

### Configuration Updates

**New Settings Required:**
```json
{
  "TfsConfig": {
    "Url": "https://tfs.company.com/DefaultCollection",
    "Project": "MyProject",
    "AuthMode": "Pat|Ntlm",
    // Note: PAT is stored client-side in browser secure storage, not in config
    "UseDefaultCredentials": false,
    "Timeout": 30
  }
}
```

---

## Work Items Integration Enhancement

### Current Implementation
- ✅ WIQL query by area path
- ✅ Batch retrieval with parent relationships
- ✅ Field extraction (Type, Title, State, Area, Iteration, Parent)

### Required Enhancements

#### 1. API Version Negotiation
**Problem:** Different TFS versions support different API versions  
**Solution:**
```csharp
private async Task<string> DetectApiVersionAsync()
{
    // Try 7.0 first (latest)
    // Fall back to 5.1, 4.1, 3.2 as needed
    // Cache the result
}
```

#### 2. Incremental Sync
**Problem:** Full refresh is slow for large projects  
**Solution:**
- Track last sync timestamp
- Use `System.ChangedDate` in WIQL query
- Only retrieve work items modified since last sync

**Implementation:**
```csharp
public async Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(
    string areaPath, 
    DateTimeOffset? since = null, 
    CancellationToken cancellationToken = default)
{
    var wiqlCondition = since.HasValue 
        ? $"AND [System.ChangedDate] >= '{since.Value:yyyy-MM-ddTHH:mm:ssZ}'"
        : "";
    
    var wiql = $@"
        Select [System.Id] 
        From WorkItems 
        Where [System.AreaPath] = '{EscapeWiql(areaPath)}'
        {wiqlCondition}
    ";
    // ... rest of implementation
}
```

#### 3. Work Item Field Extraction Enhancement
**Current:** Only basic fields extracted  
**Required:** Support custom fields and effort

**Implementation:**
```csharp
public sealed record WorkItemDto
{
    // ... existing fields
    public int? Effort { get; init; }
    public Dictionary<string, object?>? CustomFields { get; init; }
}
```

#### 4. Error Handling and Retry
**Problem:** Network issues cause failures  
**Solution:** Implement retry logic with exponential backoff

```csharp
private async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    CancellationToken cancellationToken)
{
    int retries = 3;
    int delayMs = 1000;
    
    for (int i = 0; i < retries; i++)
    {
        try
        {
            return await operation();
        }
        catch (HttpRequestException) when (i < retries - 1)
        {
            await Task.Delay(delayMs * (int)Math.Pow(2, i), cancellationToken);
        }
    }
    throw new InvalidOperationException("Max retries exceeded");
}
```

#### 5. Large Result Set Handling
**Problem:** WIQL has max result limit (20,000)  
**Solution:** Pagination support

---

## Pull Requests Integration Implementation

### Architecture

**Core DTOs** (Already defined in `PoTool.Core/PullRequests/`):
- ✅ `PullRequestDto` - Basic PR data
- ✅ `PullRequestIterationDto` - PR iterations (pushes)
- ✅ `PullRequestCommentDto` - Review comments
- ✅ `PullRequestFileChangeDto` - File changes per iteration
- ✅ `PullRequestMetricsDto` - Calculated metrics

### TFS Git API Endpoints

#### 1. Get Pull Requests
**Endpoint:** `GET {org}/{project}/_apis/git/repositories/{repo}/pullrequests?api-version=7.0`

**Query Parameters:**
- `searchCriteria.status`: Active, Completed, Abandoned, All
- `searchCriteria.createdBy`: Filter by author
- `searchCriteria.reviewerId`: Filter by reviewer
- `searchCriteria.sourceRefName`: Source branch
- `searchCriteria.targetRefName`: Target branch
- `$top`: Max results (default 100)
- `$skip`: Pagination offset

**Response Mapping:**
```csharp
{
  "pullRequestId": 1234,
  "title": "Add feature X",
  "createdBy": { "displayName": "John Doe", "uniqueName": "john@company.com" },
  "creationDate": "2024-12-01T10:00:00Z",
  "closedDate": "2024-12-05T15:30:00Z",
  "status": "completed",
  "sourceRefName": "refs/heads/feature/x",
  "targetRefName": "refs/heads/main",
  "repository": { "name": "MyRepo" }
}
```

**Implementation:**
```csharp
public async Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
    string? repositoryName = null,
    DateTimeOffset? fromDate = null,
    DateTimeOffset? toDate = null,
    CancellationToken cancellationToken = default)
{
    var config = await _configService.GetConfigEntityAsync(cancellationToken);
    var pat = _configService.UnprotectPatEntity(config);
    
    // Get all repositories or specific one
    var repos = await GetRepositoriesAsync(repositoryName, cancellationToken);
    
    var allPRs = new List<PullRequestDto>();
    
    foreach (var repo in repos)
    {
        var url = BuildPullRequestsUrl(config.Url, config.Project, repo.Name, fromDate, toDate);
        var response = await GetAsync<PullRequestsResponse>(url, pat, cancellationToken);
        
        foreach (var pr in response.Value)
        {
            allPRs.Add(new PullRequestDto(
                Id: pr.PullRequestId,
                RepositoryName: repo.Name,
                Title: pr.Title,
                CreatedBy: pr.CreatedBy.DisplayName,
                CreatedDate: pr.CreationDate,
                CompletedDate: pr.ClosedDate,
                Status: pr.Status,
                IterationPath: DetermineIterationPath(pr, config.Project),
                SourceBranch: pr.SourceRefName,
                TargetBranch: pr.TargetRefName,
                RetrievedAt: DateTimeOffset.UtcNow
            ));
        }
    }
    
    return allPRs;
}
```

#### 2. Get Pull Request Iterations
**Endpoint:** `GET {org}/{project}/_apis/git/repositories/{repo}/pullrequests/{prId}/iterations?api-version=7.0`

**Response:**
```json
{
  "value": [
    {
      "id": 1,
      "createdDate": "2024-12-01T10:00:00Z",
      "updatedDate": "2024-12-01T11:00:00Z",
      "hasMoreCommits": false,
      "changeList": [
        {
          "item": { "path": "/src/file.cs" },
          "changeType": "edit"
        }
      ]
    }
  ]
}
```

**Implementation:**
```csharp
public async Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
    int pullRequestId,
    string repositoryName,
    CancellationToken cancellationToken = default)
{
    var config = await _configService.GetConfigEntityAsync(cancellationToken);
    var pat = _configService.UnprotectPatEntity(config);
    
    var url = $"{config.Url.TrimEnd('/')}/{config.Project}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/iterations?api-version=7.0";
    
    var response = await GetAsync<IterationsResponse>(url, pat, cancellationToken);
    
    return response.Value.Select(iteration => new PullRequestIterationDto(
        Id: iteration.Id,
        CreatedDate: iteration.CreatedDate,
        UpdatedDate: iteration.UpdatedDate,
        FileCount: iteration.ChangeList?.Count ?? 0
    ));
}
```

#### 3. Get Pull Request Comments
**Endpoint:** `GET {org}/{project}/_apis/git/repositories/{repo}/pullrequests/{prId}/threads?api-version=7.0`

**Response:**
```json
{
  "value": [
    {
      "id": 100,
      "publishedDate": "2024-12-01T12:00:00Z",
      "status": "active",
      "comments": [
        {
          "id": 1,
          "author": { "displayName": "Jane Doe" },
          "content": "Please fix indentation",
          "publishedDate": "2024-12-01T12:00:00Z"
        }
      ]
    }
  ]
}
```

**Implementation:**
```csharp
public async Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
    int pullRequestId,
    string repositoryName,
    CancellationToken cancellationToken = default)
{
    var config = await _configService.GetConfigEntityAsync(cancellationToken);
    var pat = _configService.UnprotectPatEntity(config);
    
    var url = $"{config.Url.TrimEnd('/')}/{config.Project}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/threads?api-version=7.0";
    
    var response = await GetAsync<ThreadsResponse>(url, pat, cancellationToken);
    
    var comments = new List<PullRequestCommentDto>();
    
    foreach (var thread in response.Value)
    {
        foreach (var comment in thread.Comments)
        {
            comments.Add(new PullRequestCommentDto(
                Id: comment.Id,
                ThreadId: thread.Id,
                Author: comment.Author.DisplayName,
                Content: comment.Content,
                PublishedDate: comment.PublishedDate
            ));
        }
    }
    
    return comments;
}
```

#### 4. Get Pull Request File Changes
**Endpoint:** `GET {org}/{project}/_apis/git/repositories/{repo}/pullrequests/{prId}/iterations/{iterationId}/changes?api-version=7.0`

**Response:**
```json
{
  "changeEntries": [
    {
      "item": { "path": "/src/file.cs" },
      "changeType": "edit",
      "sourceServerItem": "/src/file.cs",
      "originalPath": "/src/file.cs"
    }
  ]
}
```

**Implementation:**
```csharp
public async Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
    int pullRequestId,
    string repositoryName,
    int iterationId,
    CancellationToken cancellationToken = default)
{
    var config = await _configService.GetConfigEntityAsync(cancellationToken);
    var pat = _configService.UnprotectPatEntity(config);
    
    var url = $"{config.Url.TrimEnd('/')}/{config.Project}/_apis/git/repositories/{repositoryName}/pullrequests/{pullRequestId}/iterations/{iterationId}/changes?api-version=7.0";
    
    var response = await GetAsync<ChangesResponse>(url, pat, cancellationToken);
    
    return response.ChangeEntries.Select(change => new PullRequestFileChangeDto(
        FilePath: change.Item.Path,
        ChangeType: change.ChangeType
    ));
}
```

### Pull Request Metrics Calculation

**Metrics to Calculate:**
1. **Time Open (Duration):** `CompletedDate - CreatedDate`
2. **Time Between Iterations:** Difference between iteration dates
3. **Rework Time:** Time spent on iterations after comments
4. **File Count:** Total files changed
5. **Average Lines Per File:** Requires diff API call

**Implementation Location:** `PoTool.Api/Services/PullRequestMetricsService.cs`

---

## Error Handling and Resilience

### 1. HTTP Error Handling

**Categories:**
- **400 Bad Request:** Invalid WIQL, invalid parameters
- **401 Unauthorized:** Invalid PAT, expired token
- **403 Forbidden:** Insufficient permissions
- **404 Not Found:** Repository/project not found
- **429 Too Many Requests:** Rate limiting
- **500 Server Error:** TFS internal error
- **503 Service Unavailable:** TFS down

**Strategy:**
```csharp
private async Task<T> HandleHttpErrorsAsync<T>(
    Func<Task<HttpResponseMessage>> request,
    CancellationToken cancellationToken)
{
    var response = await request();
    
    if (response.IsSuccessStatusCode)
    {
        return await DeserializeAsync<T>(response, cancellationToken);
    }
    
    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
    
    throw response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => new TfsAuthenticationException("Invalid credentials", errorContent),
        HttpStatusCode.Forbidden => new TfsAuthorizationException("Insufficient permissions", errorContent),
        HttpStatusCode.NotFound => new TfsResourceNotFoundException("Resource not found", errorContent),
        HttpStatusCode.TooManyRequests => new TfsRateLimitException("Rate limit exceeded", errorContent),
        _ => new TfsException($"TFS request failed: {response.StatusCode}", errorContent)
    };
}
```

### 2. Retry Policy

**Transient Errors:** 429, 500, 503, network timeouts  
**Strategy:** Exponential backoff with jitter

```csharp
private async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    CancellationToken cancellationToken = default)
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
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) + Random.Shared.Next(0, 1000) / 1000.0);
            
            _logger.LogWarning(ex, "TFS request failed (attempt {Attempt}/{MaxRetries}), retrying after {Delay}s", 
                attempt, maxRetries, delay.TotalSeconds);
            
            await Task.Delay(delay, cancellationToken);
        }
    }
}

private bool IsTransient(Exception ex)
{
    return ex is TfsRateLimitException 
        || ex is HttpRequestException
        || (ex is TfsException tfsEx && tfsEx.StatusCode >= 500);
}
```

### 3. Circuit Breaker (Future Enhancement)

**Purpose:** Prevent cascading failures when TFS is down  
**Library:** Polly (requires approval)

---

## Testing Strategy

### 1. Unit Tests (MSTest)

**Location:** `PoTool.Tests.Unit/TfsClientTests.cs`

**Coverage:**
- ✅ Work Item parsing (existing)
- ❌ Pull Request parsing (new)
- ❌ Error handling scenarios (new)
- ❌ Retry logic (new)
- ❌ Authentication modes (new)

**New Test Cases Required:**

```csharp
[TestMethod]
public async Task GetPullRequestsAsync_ParsesResponse_Correctly()
{
    // Arrange: Mock HTTP response with PR data
    // Act: Call GetPullRequestsAsync
    // Assert: Verify PR DTOs correctly populated
}

[TestMethod]
public async Task GetPullRequestsAsync_HandlesEmptyRepositories()
{
    // Arrange: Mock empty repo list
    // Act: Call GetPullRequestsAsync
    // Assert: Returns empty collection, no errors
}

[TestMethod]
public async Task GetPullRequestIterationsAsync_HandlesMultipleIterations()
{
    // Arrange: Mock multiple iterations
    // Act: Call GetPullRequestIterationsAsync
    // Assert: All iterations returned with correct data
}

[TestMethod]
public async Task ExecuteWithRetryAsync_RetriesOnTransientFailure()
{
    // Arrange: Mock HTTP 503, then success
    // Act: Call method with retry
    // Assert: Succeeds after retry, logs attempts
}

[TestMethod]
public async Task ValidateConnectionAsync_FailsWithInvalidPat()
{
    // Arrange: Mock 401 response
    // Act: Call ValidateConnectionAsync
    // Assert: Returns false
}
```

### 2. Integration Tests (Reqnroll)

**Location:** `PoTool.Tests.Integration/Features/`

**Mock Strategy:**
- Use `MockTfsClient` for all TFS API calls
- File-based test data in `PoTool.Tests.Integration/TestData/`
- Realistic response scenarios

**New Feature Files Required:**

**PullRequests.feature:**
```gherkin
Feature: Pull Request Synchronization
  As a Product Owner
  I want to sync pull request data from TFS
  So that I can analyze PR metrics

Scenario: Sync pull requests successfully
  Given TFS is configured with valid credentials
  When I request to sync pull requests
  Then pull requests are retrieved from TFS
  And pull requests are stored in the database

Scenario: Sync pull requests with date filter
  Given TFS is configured
  When I request to sync pull requests from 2024-12-01 to 2024-12-31
  Then only pull requests within that date range are retrieved

Scenario: Retrieve pull request iterations
  Given a pull request exists in TFS
  When I request iterations for that pull request
  Then all iterations are returned with file counts

Scenario: Handle TFS connection failure
  Given TFS is unavailable
  When I request to sync pull requests
  Then an appropriate error is returned
  And no data is corrupted
```

### 3. Mock Test Data

**Location:** `PoTool.Tests.Integration/TestData/`

**Files to Create:**
- `pull-requests-response.json` - Sample PR list
- `pr-iterations-response.json` - Sample iterations
- `pr-comments-response.json` - Sample comments
- `pr-file-changes-response.json` - Sample file changes

**Enhanced MockTfsClient:**
```csharp
public class MockTfsClient : ITfsClient
{
    private readonly Dictionary<string, List<PullRequestDto>> _mockPullRequests = new();
    
    public void LoadTestData(string filePath)
    {
        var json = File.ReadAllText(filePath);
        // Deserialize and store mock data
    }
    
    // Implement all PR methods with mock data
}
```

---

## Configuration Management

### Current Configuration
```csharp
public class TfsConfigEntity
{
    public string Url { get; set; }
    public string Project { get; set; }
    // Note: PAT is no longer stored in database (see PAT_STORAGE_BEST_PRACTICES.md)
}
```

### Enhanced Configuration

**New Entity:**
```csharp
public class TfsConfigEntity
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public TfsAuthMode AuthMode { get; set; } = TfsAuthMode.Pat;
    // Note: PAT removed from entity - stored client-side only (see PAT_STORAGE_BEST_PRACTICES.md)
    public bool UseDefaultCredentials { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiVersion { get; set; } = "7.0";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastValidated { get; set; }
}

public enum TfsAuthMode
{
    Pat,
    Ntlm
}
```

**Migration:**
- Create EF Core migration for new fields
- Provide default values for existing records

---

## Implementation Phases

### Phase 1: Foundation (Week 1)
**Duration:** 8-10 hours

**Tasks:**
1. ✅ Create planning document (this document)
2. Create TFS exception hierarchy
3. Implement authentication provider with NTLM support
4. Implement retry logic with exponential backoff
5. Implement HTTP error handling
6. Update TfsConfigEntity with new fields
7. Create EF Core migration
8. Unit tests for error handling and retry

**Deliverables:**
- TFS_ONPREM_INTEGRATION_PLAN.md
- TfsAuthenticationProvider.cs
- TfsException hierarchy
- Enhanced TfsConfigEntity
- 10+ unit tests

**Success Criteria:**
- ✅ All existing tests pass
- ✅ New retry logic tested
- ✅ NTLM auth mode testable
- ✅ Configuration updated

### Phase 2: Pull Requests Implementation (Week 2-3)
**Duration:** 16-20 hours

**Tasks:**
1. Implement `GetPullRequestsAsync()` with pagination
2. Implement `GetPullRequestIterationsAsync()`
3. Implement `GetPullRequestCommentsAsync()`
4. Implement `GetPullRequestFileChangesAsync()`
5. Implement PullRequestMetricsService
6. Create response DTOs for JSON parsing
7. Update MockTfsClient with PR data
8. Create test data files (JSON)
9. Unit tests for all PR methods
10. Integration tests (Reqnroll feature files)

**Deliverables:**
- Full PR implementation in TfsClient
- PullRequestMetricsService
- MockTfsClient with PR support
- TestData/ JSON files
- 20+ unit tests
- 3+ Reqnroll scenarios

**Success Criteria:**
- ✅ All PR methods retrieve data correctly
- ✅ Metrics calculated accurately
- ✅ 100% test coverage for PR code
- ✅ Integration tests pass

### Phase 3: Work Items Enhancement (Week 4)
**Duration:** 8-10 hours

**Tasks:**
1. Implement API version negotiation
2. Implement incremental sync with `since` parameter
3. Implement effort field extraction
4. Implement custom fields support
5. Implement large result set pagination
6. Update unit tests
7. Update integration tests

**Deliverables:**
- Enhanced GetWorkItemsAsync
- API version detection
- Incremental sync support
- 10+ tests

**Success Criteria:**
- ✅ Works with TFS 2019+ (API 5.1+)
- ✅ Incremental sync reduces load
- ✅ Large projects (10K+ work items) handled

### Phase 4: Resilience & Production Readiness (Week 5)
**Duration:** 8-10 hours

**Tasks:**
1. Implement request timeout configuration
2. Implement rate limiting awareness
3. Add comprehensive logging
4. Add performance metrics
5. Add health check enhancements
6. Load testing with large datasets
7. Documentation updates

**Deliverables:**
- Production-ready TfsClient
- Updated documentation
- Performance benchmarks
- Health check enhancements

**Success Criteria:**
- ✅ Handles TFS rate limits gracefully
- ✅ Logs all TFS interactions
- ✅ Handles large datasets (100K+ work items)
- ✅ Health check validates TFS connection

### Phase 5: UI & Feature Integration (Week 6)
**Duration:** 10-12 hours

**Tasks:**
1. Update WorkItemExplorer to use incremental sync
2. Implement PR Insights UI (per feature requirements)
3. Add PR metrics visualization
4. Add error messages to UI
5. Update configuration UI for new auth modes
6. End-to-end testing
7. User documentation

**Deliverables:**
- Updated Work Item Explorer
- PR Insights page
- Configuration UI updates
- User documentation

**Success Criteria:**
- ✅ All features use real TFS integration
- ✅ UI handles errors gracefully
- ✅ Users can switch auth modes
- ✅ End-to-end scenarios work

---

## Dependencies and Risks

### Dependencies
**Required:**
- ✅ No new packages required (REST API approach)
- ✅ .NET 10 SDK
- ✅ Existing packages sufficient

**Optional (Future):**
- ⚠️ Polly for circuit breaker (requires approval)

### Risks

**High Risk:**
1. **TFS Version Compatibility**
   - Mitigation: Test against TFS 2019, 2022, Azure DevOps
   - Fallback: Document minimum version requirement

2. **NTLM Authentication**
   - Mitigation: Test in actual Windows domain environment
   - Fallback: PAT-only mode for initial release

**Medium Risk:**
3. **Rate Limiting**
   - Mitigation: Implement retry with exponential backoff
   - Fallback: User notification to slow down sync frequency

4. **Large Dataset Performance**
   - Mitigation: Pagination and incremental sync
   - Fallback: Configurable page size

**Low Risk:**
5. **API Changes**
   - Mitigation: Use stable API versions (5.1+)
   - Fallback: Quick update if needed

---

## Success Metrics

### Functional Metrics
- ✅ 100% of ITfsClient methods implemented
- ✅ Work Items: Full CRUD operations (read-only for v1)
- ✅ Pull Requests: Full read operations
- ✅ Authentication: PAT + NTLM support
- ✅ Error handling: All HTTP errors mapped to domain exceptions

### Quality Metrics
- ✅ 100% test coverage for TfsClient
- ✅ 100% test coverage for new services
- ✅ Zero NuGet vulnerabilities
- ✅ Zero architecture violations
- ✅ Zero failed integration tests

### Performance Metrics
- ✅ Work Item sync: <5s for 1000 items
- ✅ Pull Request sync: <10s for 100 PRs
- ✅ Incremental sync: <2s for changed items only
- ✅ API calls: <2s average response time

### User Experience Metrics
- ✅ Configuration: <2 minutes to setup
- ✅ Error messages: Clear and actionable
- ✅ Sync feedback: Real-time progress via SignalR
- ✅ Offline mode: Cached data always available

---

## Maintenance and Future Enhancements

### Short-term (3 months)
1. Monitor TFS API performance and errors
2. Gather user feedback on auth modes
3. Optimize slow queries
4. Add more work item fields as needed

### Medium-term (6 months)
1. Implement work item mutations (create, update)
2. Add OAuth 2.0 support
3. Implement circuit breaker pattern
4. Add background sync scheduling

### Long-term (12 months)
1. Support TFS 2015/2017 (older API versions)
2. Implement work item attachments
3. Implement work item links and relations
4. Add Git commit integration
5. Add build pipeline integration

---

## Appendix A: TFS REST API Reference

### Core API Endpoints

**Work Items:**
- WIQL Query: `POST /_apis/wit/wiql?api-version=7.0`
- Batch Get: `GET /_apis/wit/workitems?ids={ids}&$expand=All&api-version=7.0`
- Get Work Item: `GET /_apis/wit/workitems/{id}?api-version=7.0`

**Pull Requests:**
- List PRs: `GET /_apis/git/repositories/{repo}/pullrequests?api-version=7.0`
- Get PR: `GET /_apis/git/repositories/{repo}/pullrequests/{id}?api-version=7.0`
- Get Iterations: `GET /_apis/git/repositories/{repo}/pullrequests/{id}/iterations?api-version=7.0`
- Get Comments: `GET /_apis/git/repositories/{repo}/pullrequests/{id}/threads?api-version=7.0`
- Get Changes: `GET /_apis/git/repositories/{repo}/pullrequests/{id}/iterations/{iterationId}/changes?api-version=7.0`

**Repositories:**
- List Repos: `GET /_apis/git/repositories?api-version=7.0`
- Get Repo: `GET /_apis/git/repositories/{repo}?api-version=7.0`

**Projects:**
- List Projects: `GET /_apis/projects?api-version=7.0`
- Get Project: `GET /_apis/projects/{project}?api-version=7.0`

### Authentication Headers

**PAT (Personal Access Token):**
```http
Authorization: Basic {base64(:{pat})}
```

**NTLM (Windows Auth):**
```http
[Use default credentials via HttpClientHandler]
```

---

## Appendix B: Code Structure

### New Files to Create

**Core Layer:**
- `PoTool.Core/Exceptions/TfsException.cs`
- `PoTool.Core/Exceptions/TfsAuthenticationException.cs`
- `PoTool.Core/Exceptions/TfsAuthorizationException.cs`
- `PoTool.Core/Exceptions/TfsResourceNotFoundException.cs`
- `PoTool.Core/Exceptions/TfsRateLimitException.cs`

**Api Layer:**
- `PoTool.Api/Services/TfsAuthenticationProvider.cs`
- `PoTool.Api/Services/PullRequestMetricsService.cs`
- `PoTool.Api/ResponseModels/` (directory for TFS JSON response models)

**Tests:**
- `PoTool.Tests.Unit/TfsAuthenticationProviderTests.cs`
- `PoTool.Tests.Unit/PullRequestMetricsServiceTests.cs`
- `PoTool.Tests.Integration/Features/PullRequests.feature`
- `PoTool.Tests.Integration/StepDefinitions/PullRequestsSteps.cs`
- `PoTool.Tests.Integration/TestData/pull-requests-response.json`

### Files to Modify

**Existing:**
- `PoTool.Api/Services/TfsClient.cs` - Add PR methods, error handling, retry
- `PoTool.Api/Persistence/Entities/TfsConfigEntity.cs` - Add new fields
- `PoTool.Tests.Integration/Support/MockTfsClient.cs` - Add PR support
- `PoTool.Core/Contracts/ITfsClient.cs` - Enhance method signatures if needed

---

## Conclusion

This comprehensive plan provides a structured approach to implementing real on-premises TFS integration for the PO Companion application. The plan:

1. **Maintains architecture compliance** - All rules in ARCHITECTURE_RULES.md respected
2. **Supports both cloud and on-prem** - Azure DevOps and TFS 2019+
3. **Is fully testable** - 100% test coverage with no real TFS dependencies
4. **Is production-ready** - Error handling, retry logic, resilience
5. **Is incremental** - 5 phases, each delivering value independently

**Total Estimated Effort:** 50-62 hours (6-8 weeks part-time)

**Recommended Start:** Phase 1 (Foundation) to establish error handling and authentication patterns.

---

**Document Status:** ✅ COMPLETE - Ready for Implementation  
**Next Step:** Begin Phase 1 implementation  
**Owner:** Engineering Team  
**Review Date:** After Phase 2 completion
