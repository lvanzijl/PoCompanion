# Remaining Work Implementation Plan — PO Companion

**Datum:** 17 december 2025  
**Doel:** Stappenplan voor implementatie van werk dat niet uitgevoerd is tijdens Fase 1, 2 en 3

---

## Status Overzicht

### ✅ Voltooid in Fase 1-3

**Fase 1: Critical Fixes**
- ✅ UI_RULES.md toegevoegd
- ✅ Moq security vulnerability gefixed (4.20.1 → 4.20.72)
- ✅ Client → Core referentie verwijderd (API client layer)
- ✅ CORS restrictions aangescherpt

**Fase 2: Architecture Alignment**
- ✅ PAT encryption geverifieerd + 11 tests
- ✅ TFS parent-child relaties geïmplementeerd + 4 tests
- ✅ Mediator pattern geïmplementeerd (source-generated)

**Fase 3: UI Improvements**
- ✅ MudBlazor component library geïntegreerd
- ✅ Bootstrap JavaScript verwijderd
- ✅ TfsConfig UI geïmplementeerd
- ✅ Dark theme actief

### ❌ Niet Voltooid / Uitgesteld

**Fase 3:**
- ❌ WorkItemExplorer refactoren (565 regels - te complex)
- ⚠️ Dark theme CSS variables (deels gedaan via MudBlazor, maar hardcoded colors blijven)

**Fase 4:**
- ❌ bUnit tests toevoegen
- ❌ Error handling centraliseren
- ❌ Code cleanup

**Fase 5:**
- ❌ MAUI Shell implementeren

---

## Detailed Implementation Plan

## TRACK A: UI Refactoring (Prioriteit: Hoog)

### A1. WorkItemExplorer Component Refactoring ⏱️ 8-10 uur

**Huidige Situatie:**
- WorkItemExplorer.razor is 565 regels
- Mixed concerns: tree rendering, SignalR, filtering, state management
- Moeilijk te testen en onderhouden

**Stappen:**

#### Stap A1.1: TreeNode Model Extractie (1 uur)
**Bestanden:**
- Maak: `PoTool.Client/Models/TreeNode.cs`

**Actie:**
```csharp
public class TreeNode
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public List<TreeNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; }
    public int Level { get; set; }
}
```

**Validatie:**
- Build succesvol
- TreeNode class compiliert

#### Stap A1.2: SignalR Service Extractie (2 uur)
**Bestanden:**
- Maak: `PoTool.Client/Services/WorkItemSyncHubService.cs`

**Actie:**
- Extract SignalR connectie logica uit WorkItemExplorer
- Implementeer IAsyncDisposable
- Event handlers voor SyncStatus updates
- Connection state management

**Interface:**
```csharp
public interface IWorkItemSyncHubService
{
    event Action<string, string>? OnSyncStatusChanged;
    Task StartAsync(string hubUrl);
    Task StopAsync();
    bool IsConnected { get; }
}
```

**Validatie:**
- Build succesvol
- Service is registreerbaar in DI
- Unit test voor connection lifecycle

#### Stap A1.3: Tree Building Service (2 uur)
**Bestanden:**
- Maak: `PoTool.Client/Services/TreeBuilderService.cs`

**Actie:**
- Extract hiërarchie building logica
- Implementeer parent-child linking
- Implementeer expand/collapse state
- Filter functionaliteit

**Interface:**
```csharp
public interface ITreeBuilderService
{
    List<TreeNode> BuildTree(IEnumerable<WorkItemDto> items);
    void ApplyFilter(List<TreeNode> tree, string filter);
    void ApplyExpandState(List<TreeNode> tree, Dictionary<int, bool> expandedState);
    Dictionary<int, bool> GetExpandedState(List<TreeNode> tree);
}
```

**Validatie:**
- Build succesvol
- Unit tests voor tree building
- Unit tests voor filtering
- Unit tests voor expand state

#### Stap A1.4: Sub-Component Creation (3 uur)
**Bestanden:**
- Maak: `PoTool.Client/Components/WorkItems/WorkItemTreeView.razor`
- Maak: `PoTool.Client/Components/WorkItems/WorkItemTreeNode.razor`
- Maak: `PoTool.Client/Components/WorkItems/WorkItemDetailPanel.razor`
- Maak: `PoTool.Client/Components/WorkItems/WorkItemToolbar.razor`

**WorkItemTreeView.razor:**
- Receives List<TreeNode>
- Renders tree structure
- Delegates to WorkItemTreeNode

**WorkItemTreeNode.razor:**
- Recursive component voor tree nodes
- Expand/collapse functionaliteit
- Click events voor selectie
- MudBlazor TreeView componenten

**WorkItemDetailPanel.razor:**
- Shows selected item details
- Uses MudCard, MudChip voor state
- JSON payload viewer

**WorkItemToolbar.razor:**
- Search/filter input (MudTextField)
- Sync button (MudButton)
- Clear filter button

**Validatie:**
- Elk component build succesvol
- Components zijn isolated testbaar
- MudBlazor components correct gebruikt

#### Stap A1.5: WorkItemExplorer Refactor (2 uur)
**Bestanden:**
- Update: `PoTool.Client/Components/WorkItems/WorkItemExplorer.razor`

**Actie:**
- Reduce to container component (~100-150 regels)
- Inject services (WorkItemService, TreeBuilderService, IWorkItemSyncHubService)
- Compose sub-components
- Coordinate state tussen components

**Structuur:**
```razor
<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <WorkItemToolbar @bind-Filter="_filter" OnSync="HandleSync" />
    <MudGrid>
        <MudItem xs="12" md="8">
            <WorkItemTreeView Items="_treeNodes" 
                             OnNodeSelected="HandleNodeSelected" />
        </MudItem>
        <MudItem xs="12" md="4">
            <WorkItemDetailPanel SelectedItem="_selectedItem" />
        </MudItem>
    </MudGrid>
</MudContainer>
```

**Validatie:**
- Build succesvol
- Functionaliteit identiek aan origineel
- Tree rendering werkt
- Sync functionaliteit werkt
- Filter werkt
- Detail panel toont data

---

### A2. Dark Theme CSS Variables ⏱️ 2-3 uur

**Huidige Situatie:**
- MudBlazor dark theme actief
- Nog steeds hardcoded colors in custom CSS
- CSS variabelen niet consistent gebruikt

**Stappen:**

#### Stap A2.1: CSS Variables Definitie (1 uur)
**Bestanden:**
- Update: `PoTool.Client/wwwroot/css/app.css`

**Actie:**
```css
:root {
    /* Color Palette */
    --color-primary: #7c4dff;
    --color-secondary: #00bcd4;
    --color-background: #121212;
    --color-surface: #1e1e1e;
    --color-text-primary: rgba(255, 255, 255, 0.87);
    --color-text-secondary: rgba(255, 255, 255, 0.60);
    --color-border: rgba(255, 255, 255, 0.12);
    
    /* Spacing */
    --spacing-xs: 4px;
    --spacing-sm: 8px;
    --spacing-md: 16px;
    --spacing-lg: 24px;
    --spacing-xl: 32px;
    
    /* Typography */
    --font-family: 'Roboto', sans-serif;
    --font-size-sm: 0.875rem;
    --font-size-md: 1rem;
    --font-size-lg: 1.25rem;
    --font-size-xl: 1.5rem;
    
    /* Shadows */
    --shadow-sm: 0 1px 3px rgba(0,0,0,0.12);
    --shadow-md: 0 4px 6px rgba(0,0,0,0.16);
    --shadow-lg: 0 10px 20px rgba(0,0,0,0.19);
}
```

**Validatie:**
- CSS valideert zonder errors
- Variables zijn toegankelijk

#### Stap A2.2: Hardcoded Colors Replacement (1.5 uur)
**Bestanden:**
- Update: alle `.razor.css` bestanden
- Update: `PoTool.Client/wwwroot/css/app.css`

**Actie:**
- Zoek alle `#` color codes
- Replace met `var(--color-*)`
- Replace alle hardcoded spacing met variabelen
- Replace font definities met variabelen

**Validatie:**
- Grep voor hardcoded colors toont minimaal resultaat
- UI ziet er identiek uit
- Dark theme consistent

#### Stap A2.3: Component CSS Isolation Check (0.5 uur)
**Actie:**
- Verify dat component-specific styles geïsoleerd zijn
- Verify dat global styles alleen in app.css staan
- Check dat er geen lekkage is tussen components

**Validatie:**
- CSS isolation werkt correct
- Geen style conflicts
- Build succesvol

---

## TRACK B: Testing & Quality (Prioriteit: Medium)

### B1. bUnit Test Project Setup ⏱️ 8-12 uur

**Stappen:**

#### Stap B1.1: bUnit Project Creation (1 uur)
**Actie:**
```bash
cd /path/to/PoTool
dotnet new mstest -n PoTool.Tests.Blazor
cd PoTool.Tests.Blazor
dotnet add package bUnit --version 1.28.9
dotnet add package bUnit.web --version 1.28.9
dotnet add package MudBlazor --version 8.0.0
dotnet add reference ../PoTool.Client/PoTool.Client.csproj
```

**Bestanden:**
- Maak: `PoTool.Tests.Blazor/PoTool.Tests.Blazor.csproj`
- Maak: `PoTool.Tests.Blazor/TestContext.cs`

**Validatie:**
- Project build succesvol
- bUnit packages installed
- Test project references Client

#### Stap B1.2: WorkItemTreeNode Tests (2 uur)
**Bestanden:**
- Maak: `PoTool.Tests.Blazor/Components/WorkItemTreeNodeTests.cs`

**Test Cases:**
- Render met mock data
- Expand/collapse functionaliteit
- Click event triggers
- Children rendering
- CSS classes correct applied

**Validatie:**
- Alle tests groen
- 100% code coverage voor WorkItemTreeNode

#### Stap B1.3: WorkItemToolbar Tests (2 uur)
**Bestanden:**
- Maak: `PoTool.Tests.Blazor/Components/WorkItemToolbarTests.cs`

**Test Cases:**
- Filter input updates binding
- Sync button triggers event
- Clear button resets filter
- Loading state disables buttons

**Validatie:**
- Alle tests groen
- User interaction scenarios covered

#### Stap B1.4: TfsConfig Page Tests (3 uur)
**Bestanden:**
- Maak: `PoTool.Tests.Blazor/Pages/TfsConfigTests.cs`

**Test Cases:**
- Form validation werkt
- Save button disabled when invalid
- API calls worden gemaakt
- Snackbar messages shown
- Password field obscured
- Loading states correct

**Validatie:**
- Alle tests groen
- Form submission flow tested
- Error handling tested

---

### B2. Error Handling & Resilience ⏱️ 4-6 uur

**Stappen:**

#### Stap B2.1: Global Error Boundary (1.5 uur)
**Bestanden:**
- Maak: `PoTool.Client/Components/Shared/ErrorBoundary.razor`
- Update: `PoTool.Client/App.razor`

**Actie:**
```razor
<ErrorBoundary>
    <ChildContent>
        <Router ... />
    </ChildContent>
    <ErrorContent Context="exception">
        <MudContainer>
            <MudAlert Severity="Severity.Error">
                An error occurred: @exception.Message
            </MudAlert>
        </MudContainer>
    </ErrorContent>
</ErrorBoundary>
```

**Validatie:**
- Error boundary catches exceptions
- User-friendly error getoond
- Application blijft responsive

#### Stap B2.2: Correlation IDs (1.5 uur)
**Bestanden:**
- Maak: `PoTool.Core/Diagnostics/CorrelationContext.cs`
- Maak: `PoTool.Api/Middleware/CorrelationIdMiddleware.cs`

**Actie:**
- Generate correlation ID per request
- Add to response headers
- Log correlation ID
- Pass via API client

**Validatie:**
- Correlation IDs in logs
- Traceable across services
- Client includes in requests

#### Stap B2.3: Polly Retry Policies (2 uur)
**Bestanden:**
- Update: `PoTool.Client/Program.cs`
- Maak: `PoTool.Client/Services/ResilientHttpClient.cs`

**Actie:**
```csharp
services.AddHttpClient<IWorkItemsClient, WorkItemsClient>()
    .AddTransientHttpErrorPolicy(policy => 
        policy.WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddTransientHttpErrorPolicy(policy =>
        policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

**Validatie:**
- Transient errors worden retry'd
- Circuit breaker voorkomt cascade failures
- Logging toont retry attempts

---

### B3. Code Cleanup ⏱️ 4-5 uur

**Stappen:**

#### Stap B3.1: Nullability Warnings Fix (1.5 uur)
**Bestanden:**
- Fix: `PoTool.Tests.Unit/WorkItemExplorerTests.cs:23`
- Scan alle projects voor nullability warnings

**Actie:**
```bash
dotnet build /warnaserror:CS8620,CS8600,CS8601,CS8602,CS8603,CS8604
```

**Validatie:**
- Zero nullability warnings
- Alle nullable references correct geannoteerd

#### Stap B3.2: Home.razor Diagnostics Cleanup (0.5 uur)
**Bestanden:**
- Update: `PoTool.Client/Pages/Home.razor`

**Actie:**
- Remove diagnostics code (163 regels → ~50 regels)
- Maak simple landing page
- Gebruik MudBlazor components
- Add navigation cards

**Validatie:**
- Home page clean en professioneel
- Geen debug/diagnostic code

#### Stap B3.3: Hardcoded Values Extraction (1 uur)
**Bestanden:**
- Maak: `PoTool.Client/Configuration/AppSettings.cs`
- Update: `PoTool.Client/wwwroot/appsettings.json`

**Actie:**
- Extract "DefaultAreaPath" naar config
- Extract API base URLs naar config
- Extract timeouts naar config

**Validatie:**
- Geen hardcoded values in code
- Configuration via appsettings.json

#### Stap B3.4: XML Documentation (1.5 uur)
**Bestanden:**
- Alle publieke classes in alle projects

**Actie:**
- Add `<summary>` tags
- Add `<param>` tags
- Add `<returns>` tags
- Enable XML doc generation in csproj

**Validatie:**
- Zero doc warnings
- Intellisense toont documentation
- XML files gegenereerd

---

## TRACK C: MAUI Shell (Prioriteit: Laag)

### C1. MAUI Project Setup ⏱️ 40+ uur

**Waarschuwing:** Dit is een groot project. Alleen starten als Tracks A en B voltooid zijn.

**Stappen:**

#### Stap C1.1: MAUI Project Creation (2 uur)
**Actie:**
```bash
dotnet new maui -n PoTool.Shell
```

**Bestanden:**
- Setup project structure
- Add required dependencies
- Configure build targets (Windows, macOS, Linux)

#### Stap C1.2: WebView Integration (8 uur)
**Actie:**
- Embed Blazor Client in MAUI WebView
- Handle navigation
- Handle authentication
- Local file serving

#### Stap C1.3: Backend Lifecycle Management (10 uur)
**Actie:**
- Start/stop API process
- Health monitoring
- Auto-restart on failure
- Clean shutdown

#### Stap C1.4: In-Process Mode (8 uur)
**Actie:**
- Run API in same process
- Shared DI container
- Direct method calls

#### Stap C1.5: Out-of-Process Mode (8 uur)
**Actie:**
- Run API as separate process
- IPC communication
- Process monitoring

#### Stap C1.6: Platform-Specific Features (4 uur)
**Actie:**
- System tray integration
- File associations
- Auto-update mechanism

---

## Implementation Priority & Schedule

### Week 1-2: Track A (UI Refactoring)
**Prioriteit:** ⭐⭐⭐ HOOG

**Dag 1-2:** WorkItemExplorer Refactoring
- A1.1: TreeNode Model (1 uur)
- A1.2: SignalR Service (2 uur)
- A1.3: Tree Builder Service (2 uur)

**Dag 3-4:** Component Splitting
- A1.4: Sub-Components (3 uur)
- A1.5: Container Refactor (2 uur)

**Dag 5:** Dark Theme CSS
- A2.1: CSS Variables (1 uur)
- A2.2: Replace Hardcoded Colors (1.5 uur)
- A2.3: Isolation Check (0.5 uur)

**Deliverable:** 
- Maintainable component structure
- Consistent dark theme
- ~8-10 uur totaal

---

### Week 3-4: Track B (Testing & Quality)
**Prioriteit:** ⭐⭐ MEDIUM

**Dag 1-2:** bUnit Setup & Tests
- B1.1: Project Setup (1 uur)
- B1.2: TreeNode Tests (2 uur)
- B1.3: Toolbar Tests (2 uur)
- B1.4: TfsConfig Tests (3 uur)

**Dag 3:** Error Handling
- B2.1: Error Boundary (1.5 uur)
- B2.2: Correlation IDs (1.5 uur)
- B2.3: Polly Policies (2 uur)

**Dag 4-5:** Code Cleanup
- B3.1: Nullability Fix (1.5 uur)
- B3.2: Home.razor Cleanup (0.5 uur)
- B3.3: Config Extraction (1 uur)
- B3.4: XML Docs (1.5 uur)

**Deliverable:**
- Test coverage >60%
- Resilient error handling
- Clean, documented code
- ~20 uur totaal

---

### Week 5+: Track C (MAUI Shell) - OPTIONEEL
**Prioriteit:** ⭐ LAAG

**Alleen starten als:**
- Track A voltooid
- Track B voltooid
- Business case voor desktop app exists

**Deliverable:**
- Desktop application
- ~40+ uur totaal

---

## Success Criteria

### Track A Success
✅ WorkItemExplorer < 150 regels  
✅ 4+ reusable sub-components  
✅ TreeNode model geïsoleerd  
✅ Services testbaar  
✅ Zero hardcoded colors  
✅ CSS variables consistent  

### Track B Success
✅ bUnit test project setup  
✅ >10 Blazor component tests  
✅ Global error boundary  
✅ Correlation IDs in logging  
✅ Polly retry policies active  
✅ Zero nullability warnings  
✅ Zero hardcoded config values  
✅ XML documentation compleet  

### Track C Success
✅ MAUI app runs op Windows/macOS/Linux  
✅ WebView hosts Blazor Client  
✅ Backend lifecycle managed  
✅ In-process en out-of-process modes  
✅ Health monitoring actief  

---

## Risk Assessment

### Track A Risks
- **Medium:** Component splitting kan breaking changes introduceren
- **Mitigation:** Incremental refactoring met tests na elke stap

### Track B Risks
- **Low:** bUnit learning curve
- **Mitigation:** Start met simpele tests, build expertise

### Track C Risks
- **High:** MAUI is complex, platform-specific issues
- **Mitigation:** Start klein, test op één platform eerst

---

## Dependencies

### Track A Dependencies
- MudBlazor ✅ (already installed)
- No blockers

### Track B Dependencies
- Track A completion (for testing refactored components)
- bUnit package (nieuwe dependency)
- Polly package (nieuwe dependency)

### Track C Dependencies
- Track A + B completion
- .NET MAUI workload
- Platform SDKs (Windows SDK, Xcode, etc.)

---

## Estimated Total Time

- **Track A:** 10-13 uur (1-2 weken part-time)
- **Track B:** 16-23 uur (2-3 weken part-time)
- **Track C:** 40+ uur (4-6 weken part-time)

**Total (A+B):** ~26-36 uur werk  
**Total (A+B+C):** ~66-76 uur werk

---

## Recommended Approach

### Scenario 1: Maximum Value, Minimum Risk
**Execute:** Track A only  
**Time:** 1-2 weken  
**Result:** Maintainable, testable UI components

### Scenario 2: Production-Ready Quality
**Execute:** Track A + B  
**Time:** 3-4 weken  
**Result:** Tested, resilient, documented codebase

### Scenario 3: Full Vision
**Execute:** Track A + B + C  
**Time:** 7-10 weken  
**Result:** Complete desktop application

---

## Getting Started

### Voor Track A:
```bash
cd /path/to/PoCompanion
git checkout -b feature/workitem-explorer-refactor
# Start met A1.1: TreeNode Model
mkdir -p PoTool.Client/Models
touch PoTool.Client/Models/TreeNode.cs
```

### Voor Track B:
```bash
cd /path/to/PoCompanion
git checkout -b feature/bunit-tests
dotnet new mstest -n PoTool.Tests.Blazor
cd PoTool.Tests.Blazor
dotnet add package bUnit --version 1.28.9
```

### Voor Track C:
```bash
cd /path/to/PoCompanion
git checkout -b feature/maui-shell
dotnet new maui -n PoTool.Shell
```

---

## Conclusie

Dit plan biedt een **gestructureerde, incrementele aanpak** voor het voltooien van het resterende werk. 

**Aanbeveling:** Start met **Track A** (UI Refactoring) voor directe waarde en verbeterde onderhoudbaarheid. Track B (Testing) voegt robuustheid toe. Track C (MAUI) is optioneel en alleen relevant als desktop deployment nodig is.

Elk track is onafhankelijk uitvoerbaar en levert zelfstandige waarde op.
