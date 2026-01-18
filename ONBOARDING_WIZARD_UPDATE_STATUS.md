# Onboarding Wizard Update - Implementation Status

## Completed Work (Backend)

### Phase 1: TFS Teams API
**Status:** ✅ Complete

#### Changes Made:
1. **New DTO**: Created `TfsTeamDto` in `PoTool.Shared/Settings/TfsTeamDto.cs`
   - Contains: Id, Name, ProjectName, Description, DefaultAreaPath

2. **ITfsClient Interface**: Added `GetTfsTeamsAsync()` method
   - Location: `PoTool.Core/Contracts/ITfsClient.cs`

3. **RealTfsClient Implementation**:
   - Calls TFS API: `/_apis/projects/{project}/teams`
   - For each team, retrieves team field values to get default area path
   - API: `/{project}/{team}/_apis/work/teamsettings/teamfieldvalues`
   - Falls back to project default or config area path if not available

4. **Mock Implementations**:
   - `MockTfsClient`: Returns test teams for unit testing
   - `BattleshipMockDataFacade`: Returns Battleship-themed mock teams
   - `Tests.Integration/MockTfsClient`: Returns integration test teams

5. **API Endpoint**: Added to `StartupController`
   - Endpoint: `GET /api/startup/tfs-teams`
   - Returns: `IEnumerable<TfsTeamDto>`
   - Fetches teams live from TFS (no caching)

### Phase 2: Save-and-Verify Streaming Endpoint
**Status:** ✅ Complete

#### Changes Made:
1. **Progress Model**: Created `TfsConfigProgressUpdate` and `ProgressState` enum
   - Location: `PoTool.Shared/Contracts/TfsConfigProgressUpdate.cs`
   - States: Running, Succeeded, Failed
   - Properties: Phase, State, Message, PercentComplete (optional), Details (optional)

2. **Streaming Endpoint**: Added to `ApiApplicationBuilderExtensions.cs`
   - Endpoint: `POST /api/tfsconfig/save-and-verify`
   - Content-Type: `application/json` (newline-delimited JSON)
   - Combines three operations:
     1. Save TFS configuration (10-20%)
     2. Test connection (30-40%)
     3. Verify API capabilities with per-check progress (50-90%)
     4. Complete (100%)
   
3. **Progress Streaming**:
   - Streams JSON-encoded progress updates line-by-line
   - Each line is a JSON object with phase, state, message, percent, details
   - Client can read lines as they arrive for real-time UI updates
   - No buffering (Cache-Control: no-cache, X-Accel-Buffering: no)

4. **Error Handling**:
   - If connection test fails, stops and reports failure
   - Individual API check failures are reported but don't stop verification
   - Final success/failure based on overall verification report
   - Updates TFS config entity with validation status

#### Example Progress Stream:
```json
{"phase":"Saving Configuration","state":"Running","message":"Saving TFS configuration...","percentComplete":10,"details":null}
{"phase":"Saving Configuration","state":"Succeeded","message":"Configuration saved successfully","percentComplete":20,"details":null}
{"phase":"Testing Connection","state":"Running","message":"Validating TFS connection...","percentComplete":30,"details":null}
{"phase":"Testing Connection","state":"Succeeded","message":"Connection validated successfully","percentComplete":40,"details":null}
{"phase":"Verifying API","state":"Running","message":"Running TFS API capability checks...","percentComplete":50,"details":null}
{"phase":"Verifying API - WorkItemQuery","state":"Succeeded","message":"✓ WorkItemQuery","percentComplete":55,"details":null}
{"phase":"Verifying API - WorkItemBatch","state":"Succeeded","message":"✓ WorkItemBatch","percentComplete":60,"details":null}
...
{"phase":"Verifying API","state":"Succeeded","message":"All 12 API checks passed","percentComplete":90,"details":null}
{"phase":"Complete","state":"Succeeded","message":"TFS configuration and verification complete","percentComplete":100,"details":null}
```

## Remaining Work (Frontend)

### Phase 3: Update Onboarding Wizard - TFS Config Step
**Status:** ❌ Not Started

#### Required Changes:
1. Replace "Save & Test Connection" button with single "Save" button
2. Implement progress UI component:
   - Show current phase
   - Display progress bar (determinate when percent available)
   - Show status messages
   - Display detailed error messages on failure
3. Connect to streaming endpoint:
   - Use `fetch()` with `ReadableStream` reader
   - Parse newline-delimited JSON
   - Update UI for each progress update
4. Handle completion:
   - On success: enable "Next" button
   - On failure: keep form editable, allow retry
   - Clear previous progress on new attempt

#### Implementation Approach:
```csharp
// In OnboardingWizard.razor
private async Task SaveAndVerifyConfig()
{
    _isSaving = true;
    _progressUpdates.Clear();
    
    var request = new TfsConfigRequest
    {
        Url = _url,
        Project = _project,
        DefaultAreaPath = _defaultAreaPath,
        UseDefaultCredentials = true,
        TimeoutSeconds = 30,
        ApiVersion = "7.0"
    };
    
    var response = await HttpClient.PostAsJsonAsync("/api/tfsconfig/save-and-verify", request);
    response.EnsureSuccessStatusCode();
    
    var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);
    
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(line)) continue;
        
        var update = JsonSerializer.Deserialize<TfsConfigProgressUpdate>(line);
        _progressUpdates.Add(update);
        StateHasChanged(); // Update UI
    }
    
    _isSaving = false;
    _configCompleted = _progressUpdates.Last().State == ProgressState.Succeeded;
}
```

### Phase 4: Update Onboarding Wizard - Team Selection Step
**Status:** ❌ Not Started

#### Required Changes:
1. Replace Step 2 content (currently "Setup Profile")
2. Fetch TFS teams on step load:
   ```csharp
   var teams = await HttpClient.GetFromJsonAsync<List<TfsTeamDto>>("/api/startup/tfs-teams");
   ```
3. Add MudSelect or MudAutocomplete for team selection
4. Display selected team's derived area path (read-only)
5. Update wizard completion:
   - Instead of creating profile immediately
   - Either: create a default Team entity in local DB
   - Or: store selection in wizard state for later use

#### UI Mockup:
```razor
<MudSelect T="TfsTeamDto" 
           Label="Select Team" 
           @bind-Value="_selectedTeam"
           Required="true">
    @foreach (var team in _teams)
    {
        <MudSelectItem Value="@team">@team.Name</MudSelectItem>
    }
</MudSelect>

@if (_selectedTeam != null)
{
    <CompactTextField Label="Area Path (Derived)" 
                      Value="@_selectedTeam.DefaultAreaPath" 
                      ReadOnly="true"
                      HelperText="This area path is derived from the selected team" />
}
```

### Phase 5: Data Model Updates
**Status:** ⚠️  Clarification Needed

#### Questions:
1. **Profile vs Team vs Product**:
   - Current: Profile doesn't have area path (it's on Team/Product)
   - Do we need to update Profile model at all?
   - Or should we create a Team entity during onboarding?

2. **Wizard Goal**:
   - What should the wizard actually create?
   - Option A: Just configure TFS, let user create profiles later
   - Option B: Create Profile + Team + Product combo during wizard
   - Current wizard creates Profile with GoalIds

3. **Backward Compatibility**:
   - How to handle existing profiles/teams without TFS team info?
   - Migration strategy if schema changes needed

#### Recommendation:
Keep wizard minimal - only configure TFS connection in Step 1.
Step 2 can demonstrate team selection but doesn't need to persist anything yet.
Let user create profiles/teams through main UI after onboarding.

## Testing Strategy

### Backend Testing (Can Start Now):
1. **Unit Tests**: Add tests for `GetTfsTeamsAsync` in existing test files
2. **Integration Tests**: Test streaming endpoint with mock TFS client
3. **Manual Testing**: 
   - Start API with mock mode
   - Use curl or Postman to call:
     - `GET /api/startup/tfs-teams`
     - `POST /api/tfsconfig/save-and-verify`
   - Verify progress streaming works

### Frontend Testing (After UI Implementation):
1. Test progress UI with mock backend
2. Test team picker with mock teams
3. Test error scenarios (connection failures, API failures)
4. Screenshot testing for PR

## Build Status
✅ All projects build successfully
✅ No compilation errors
✅ Backend implementation complete

## Next Steps
1. **Decision**: Clarify what Step 2 should create/persist
2. **Frontend**: Implement progress UI for Step 1
3. **Frontend**: Implement team picker for Step 2
4. **Testing**: Add unit/integration tests
5. **Manual Testing**: Verify with mock and real TFS
6. **Documentation**: Update user documentation if needed
