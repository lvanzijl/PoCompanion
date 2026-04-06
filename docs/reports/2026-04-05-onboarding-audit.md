# Onboarding Wizard Audit

## 1. Entry Points & Structure

### Routes
- Primary onboarding route: `/onboarding` in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor:1-36`.
- Root route `/` redirects to `/onboarding` when startup readiness is `SetupRequired` and onboarding is not marked complete/skipped; it also resets the onboarding flag when no saved TFS config exists (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Index.razor:12-37`).
- Settings can re-open onboarding via the “Run Getting Started Wizard” button, which resets onboarding state and force-loads `/onboarding` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/GettingStartedSection.razor:5-29`).
- Startup guard explicitly exempts `/onboarding`, `/sync-gate`, `/settings`, `/profiles`, and `/` from readiness blocking (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Common/StartupGuard.razor:56-66,101-108`).

### Components
- Host page: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor`.
- Wizard dialog: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor`.
- Embedded import UI on step 1: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/ConfigurationImportExportSection.razor:12-156,269-343`.

### Client services used
- Onboarding completion/skip state: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingService.cs:7-55`.
- In-memory TFS verification gate: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingWizardState.cs:10-77`.
- Saved TFS config load: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TfsConfigService.cs:12-180`.
- Profile creation/activation: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProfileService.cs:9-182`.
- Product creation/linking/repository APIs: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductService.cs:10-226`.
- Team creation: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/TeamService.cs:10-129`.
- Configuration import/export client wrapper: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ConfigurationTransferService.cs:7-70`.
- Startup readiness/routing: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/StartupOrchestratorService.cs:10-213`.

### API endpoints and server pieces involved
- Startup readiness and live discovery:
  - `GET /api/startup/readiness`
  - `GET /api/startup/tfs-projects`
  - `GET /api/startup/tfs-teams`
  - `GET /api/startup/git-repositories`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/StartupController.cs:25-85`
- TFS config and verification minimal APIs:
  - `GET /api/tfsconfig`
  - `POST /api/tfsconfig`
  - `GET /api/tfsvalidate`
  - `POST /api/tfsverify`
  - `POST /api/tfsconfig/save-and-verify`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:213-423`
- SignalR progress hub:
  - `/hubs/tfsconfig`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Hubs/TfsConfigHub.cs:9-79`
- Persistence APIs:
  - Profiles: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProfilesController.cs:24-156`
  - Products and repository creation: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/ProductsController.cs:25-284`
  - Teams: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/TeamsController.cs:24-157`
  - Configuration import/export: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/SettingsController.cs:110-144`

### Models / DTOs used
- Startup/readiness: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/StartupReadinessDto.cs:7-43`
- TFS project/team/repository discovery:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TfsProjectDto.cs:7-11`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TfsTeamDto.cs:7-13`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/GitRepositoryInfoDto.cs:7-10`
- TFS progress stream: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Contracts/TfsConfigProgressUpdate.cs:6-33`
- Import/export contracts: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ConfigurationTransferDto.cs:5-62`
- Persisted entities surfaced by onboarding:
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProfileDto.cs:23-38`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs:24-45`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/TeamDto.cs:23-37`
  - `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/RepositoryDto.cs:7-19`

## 2. Step-by-Step Flow

The wizard has 5 fixed steps and uses `_currentStep` values `0..4` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:604-665,745-760`).

### Step 1 — Configure Azure DevOps Connection
- **Purpose**
  - Collect TFS organization URL and project, then save + validate + verify the connection (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:39-205,910-994`).
- **Required inputs**
  - Organization URL.
  - Project name or selected project (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:46-102`).
- **Optional inputs**
  - Import an existing configuration JSON instead of manually completing the wizard (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:189-205`).
- **Actions**
  - `Save`
  - `Next`
  - `Skip Wizard`
  - Import existing configuration (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:113-128,568-585,874-880`)
- **Persistence behavior**
  - Existing saved TFS config is loaded on wizard startup (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:721-743`).
  - `Save` immediately persists config in phase 1, then tests connection, then verifies API capabilities (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:333-412`).
  - Successful import can finish onboarding immediately; the callback calls `Complete()` and closes the wizard (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:874-880,1458-1463`).
- **Data source**
  - Saved config: local API/database via `GET /api/tfsconfig`.
  - Project list: direct TFS via `GET /api/startup/tfs-projects`.
  - Save/test/verify: direct TFS through `POST /api/tfsconfig/save-and-verify` plus SignalR `/hubs/tfsconfig`.
  - Import: local configuration import API (`/api/settings/configuration-import`) (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ConfigurationTransferService.cs:38-68`).

### Step 2 — Create Product Owner Profile
- **Purpose**
  - Create and activate a Product Owner profile (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:211-265,1077-1100`).
- **Required inputs**
  - Profile name (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:221-247`).
- **Optional inputs**
  - None in this wizard step.
- **Actions**
  - `Save Profile`
  - `Previous`
  - `Next`
  - `Skip Wizard`
- **Persistence behavior**
  - `Save Profile` immediately creates the profile and sets it active (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1084-1089`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProfileService.cs:144-163`).
  - No auto-save on `Next`.
- **Data source**
  - Local API/database only (`POST /api/profiles`, then `POST /api/profiles/active` through `ProfileService`).

### Step 3 — Create Product
- **Purpose**
  - Create a product and assign one backlog root work item ID (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:269-329,1114-1150`).
- **Required inputs**
  - Product name.
  - Backlog root work item ID (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:279-312`).
- **Optional inputs**
  - Product owner link is optional because the step uses `_createdProfileId`, which is null if profile creation was skipped (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1128-1134`).
- **Actions**
  - `Save Product`
  - `Previous`
  - `Next`
  - `Skip Wizard`
- **Persistence behavior**
  - `Save Product` immediately creates the product and stores its ID in `_createdProductId` for later team/repository steps (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1130-1139`).
  - The UI only accepts one root ID, then wraps it as `new List<int> { workItemId }` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1130-1133`).
  - No auto-save on `Next`.
- **Data source**
  - Local API/database only (`POST /api/products` through `ProductService`).

### Step 4 — Select Your Team
- **Purpose**
  - Load TFS teams, let the user pick one, optionally override the derived area path, create the team locally, and optionally link it to the product created earlier in the same wizard run (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:334-455,1164-1265`).
- **Required inputs**
  - TFS team selection is required only for `Save Team`.
  - Area path becomes required if “Override area path” is checked (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:353-431`).
- **Optional inputs**
  - Override area path.
  - Team picture choice (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:374-408`).
- **Actions**
  - `Save Team`
  - `Previous`
  - `Next`
  - `Skip Wizard`
- **Persistence behavior**
  - Team list is loaded when entering step 4, but only if `_configCompleted == true` in this wizard session (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:888-892`).
  - `Save Team` immediately creates the team.
  - Product linking only runs when both `_createdProductId` and `_createdTeamId` exist in the current wizard session (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1232-1254`).
  - No auto-save on `Next`.
- **Data source**
  - Team list: direct TFS via `GET /api/startup/tfs-teams`.
  - Team persistence/linking: local API/database via `POST /api/teams` and `POST /api/products/{productId}/teams/{teamId}`.

### Step 5 — Configure Repositories
- **Purpose**
  - Load Git repositories from TFS and attach selected repository names to the product created earlier in the wizard (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:460-563,1297-1437`).
- **Required inputs**
  - None to continue; repository selection is optional.
  - At least one selected repository is required only for `Save Repositories` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1366-1369`).
- **Optional inputs**
  - Multiple repository selections via autocomplete + chips (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:479-533,1324-1356`).
- **Actions**
  - `Save Repositories`
  - `Previous`
  - `Get Started`
  - `Skip Wizard`
- **Persistence behavior**
  - Repository list is loaded when entering step 5, but only if `_configCompleted == true` in this wizard session (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:894-898`).
  - `Save Repositories` immediately posts each selected repository name to `POST /api/products/{productId}/repositories`.
  - Saves run in parallel and can partially succeed (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1377-1426`).
  - `Get Started` does not require repository save (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:587-591,1458-1463`).
- **Data source**
  - Repository list: direct TFS via `GET /api/startup/git-repositories`.
  - Repository persistence: local API/database via `POST /api/products/{productId}/repositories`.

### Wizard completion / exit
- `Skip Wizard` marks onboarding skipped and closes the dialog (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1451-1456`).
- `Get Started` marks onboarding completed and closes the dialog (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1458-1463`).
- The host page ignores the dialog result and always navigates to `/sync-gate?returnUrl=%2Fhome` after the dialog closes (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor:30-35`).

## 3. Interaction Model Analysis

### Save vs Next behavior
- Step 1 is the only step where `Next` is gated. `IsNextDisabled()` only checks `_currentStep == 0` and requires `WizardState.TfsVerified` plus unchanged URL/project fields (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:763-771`).
- Steps 2-5 always allow `Next`/`Get Started`; there is no requirement to save profile, product, team, or repositories before moving on (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:763-771,578-591`).
- Save behavior is immediate on every `Save*` button:
  - TFS config save + verify: immediate (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:333-412`)
  - Profile: immediate create + activate (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1084-1089`)
  - Product: immediate create (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1130-1139`)
  - Team: immediate create, optional immediate product link (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1218-1254`)
  - Repositories: immediate per-item creates (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1380-1426`)

### Risk of data loss
- Unsaved input on steps 2-5 exists only in component state; it is not persisted anywhere until the corresponding `Save*` action runs.
- `Skip Wizard` closes immediately with no unsaved-change check (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1451-1456`).
- `Get Started` also closes immediately with no check for unsaved team/repository selections or unsaved profile/product edits (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1458-1463`).
- Back/Next inside the open dialog keeps values in memory because the component instance stays alive; loss happens when the wizard closes or the page reloads.

### Multi-item handling clarity
- **Supported in the wizard**
  - Repository selection supports multiple items via autocomplete plus removable chips (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:482-533,1342-1356`).
- **Not supported in the wizard UI**
  - Product creation accepts only one backlog root input even though the product API and DTO support lists of root IDs (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:287-293,1130-1133`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/ProductService.cs:53-76`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs:24-45`).
- **Clarity**
  - Repository chips and a count are shown before save (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:499-563`).
  - The UI does not explicitly say that repository selections are only in-memory until `Save Repositories`.
  - The product step does not explain that only one backlog root can be entered even though the underlying API is list-based.

## 4. Data Validation & Feedback

### What is validated
- Step 1 requires non-empty URL and project fields in the form (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:46-102`).
- Step 1 verifies connection and API capabilities against TFS only when `Save` is executed (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:910-994`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:346-412`).
- Step 2 validates profile name via the backend validator (required, max 200, allowed characters) (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateProfileCommandValidator.cs:14-33`).
- Step 3 validates:
  - Client side: required name and required root ID field (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:279-312`)
  - Save-time parse: root ID must parse as an integer (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1123-1126`)
  - Backend: name rules and root IDs `> 0` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateProductCommandValidator.cs:14-37`)
- Step 4 validates:
  - TFS team selection required for save (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:353-370`)
  - Backend area path format and metadata constraints (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateTeamCommandValidator.cs:14-57`)
- Step 5 validates:
  - repository save requires `_createdProductId`
  - at least one repository selected
  - backend repository name rules and duplicate name check per product (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1358-1426`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/Settings/Validators/CreateRepositoryCommandValidator.cs:14-29`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/RepositoryRepository.cs:69-91`)
- Import file selection only checks “is valid JSON” on the client before calling the import API (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Helpers/ConfigurationImportUiHelper.cs:46-65`).

### What is not validated
- URL format is not validated beyond “required” on the client (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:47-53`).
- Backlog root work item ID is not verified against TFS in the wizard. The step never calls any work-item validation or lookup endpoint and shows no work item title/details (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:287-293,1114-1145`).
- Product creation does not verify that the optional profile ID refers to an existing profile in the wizard flow; it just passes `_createdProfileId` when present (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1128-1134`).
- Team and repository selections are loaded from TFS lists, but save-time persistence does not re-check current TFS existence; it persists the selected values locally.
- The wizard does not load existing profiles/products/teams/repositories for review or reuse; only TFS config is preloaded (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:667-743`).

### Where user cannot verify selections
- Project selection:
  - Dropdown items show name/description while searching, but there is no post-selection confirmation panel beyond the autocomplete field (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:64-81`).
- Backlog root work item:
  - Only a numeric ID is shown; no fetched title, type, or existence check is displayed (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:287-293`).
- Team selection:
  - The selected team’s derived area path is shown, but there is no separate confirmation of team ID/project beyond the selected item and read-only area path (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:372-392`).
- Repository selection:
  - Selected repository names are shown as chips, but IDs are not shown and there is no persisted-state confirmation until after save (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:499-533`).
- Successful import:
  - The import component can show detailed results, but onboarding immediately completes/closes on successful import, so the wizard does not keep the user on a final review screen (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/ConfigurationImportExportSection.razor:313-330`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:874-880`).

### Silent failures or unclear states
- If project loading fails, the wizard logs the error and falls back to manual project entry; the UI does not show the actual failure reason (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:826-855`).
- Team/repository load failures surface as snackbars plus generic “No teams found” / “No Git repositories found” alerts, without retaining detailed diagnostics on the screen (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1164-1188,1297-1321,446-453,549-553`).
- Team-link failure sets `_teamSaveResult.Success = true`, so the inline alert renders as success even when product linking failed (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:433-437,1243-1248`).
- Partial repository save also renders as success if at least one repository succeeded because `_repositorySaveResult.Success` is set to `successCount > 0` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:536-540,1414-1420`).

## 5. Data Source & Responsibility Analysis

### Cache vs direct TFS usage
- The wizard itself does not use cache-backed analytical endpoints.
- Direct TFS/live calls used by onboarding:
  - Project discovery (`GET /api/startup/tfs-projects`)
  - Team discovery (`GET /api/startup/tfs-teams`)
  - Repository discovery (`GET /api/startup/git-repositories`)
  - Connection validation / API verification (`POST /api/tfsconfig/save-and-verify`, SignalR progress)
  - These routes are explicitly live-allowed in `DataSourceModeConfiguration` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/DataSourceModeConfiguration.cs:28-95`).
- Local persisted data used by onboarding:
  - Saved TFS config (`GET /api/tfsconfig`)
  - Profiles/products/teams/repositories via their local controllers/repositories
  - Configuration import/export APIs
- Cache use appears only after the wizard closes:
  - `/onboarding` always routes to `/sync-gate`
  - `/sync-gate` loads the active profile and cache status, then triggers cache sync (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor:30-35`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/SyncGate.razor:141-183`).

### Mixed responsibilities per screen
- **Step 1 mixes four concerns in one screen**
  - saved-config preload
  - live TFS project discovery
  - save/test/verify orchestration with SignalR progress
  - configuration import (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:39-205`)
- **The wizard component mixes UI and transport responsibilities**
  - It injects typed services for profile/product/team
  - It also injects raw `HttpClient` and performs direct HTTP calls from the component for startup discovery, save-and-verify, and repository saves (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:12-23,834-855,935-942,1171-1177,1304-1310,1385-1388`)
- **Step 4 mixes team creation with product linking**
  - Team persistence and product-team linking are both triggered from the same save action (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1218-1254`)
- **Step 5 mixes live repository discovery with local per-product repository persistence**
  - Repositories are fetched from TFS, then each selected name is posted individually to the product API (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1297-1437`)

## 6. UX Structural Issues

### Cognitive overload points
- Step 1 is the densest screen:
  - TFS URL input
  - project discovery/manual fallback
  - auth explanation
  - config summary
  - save button
  - streaming progress
  - error display
  - informational skip messaging
  - full import flow (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:39-205`)
- The wizard file is a single large component (~1500 lines) containing UI, state, HTTP orchestration, SignalR wiring, and local DTOs (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1-1503`).

### Confusing screens / missing clarity
- Step 1 shows “Activity source: not configured” as a static summary even while the user is configuring URL/project; it does not summarize the actual entered values (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:108-111`).
- Steps 2-5 all display “You can skip this step and configure later”, but the wizard does not explicitly say that typed-but-unsaved input will be discarded on navigation or completion (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:262-264,327-329,453-455,556-563`).
- The wizard reuses existing TFS config but does not surface existing profiles/products/teams/repositories, so rerunning the wizard does not show what is already configured (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:667-743`).
- Step 5 lets the user browse and select repositories even when save will fail unless a product was created earlier in the same wizard run (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:499-533,1358-1364`).

### Inconsistent patterns
- Step 1 requires save-before-next; steps 2-5 do not (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:763-771`).
- Some steps use typed services; others call raw HTTP directly from the component (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:12-23,1077-1100,1114-1150,1207-1265,1358-1437`).
- Completion routing does not depend on dialog outcome; skip and complete both close into the same host-page navigation path (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor:30-35`).

## 7. Failure Modes & Risks

### Concrete, reproducible failure scenarios
1. **Unsaved profile/product/team/repository input can be abandoned while onboarding still completes**
   - Reproduce: enter data on steps 2-5, do not press `Save`, use `Next` or `Get Started`.
   - Result: onboarding is marked complete/skipped, but the entered data was never persisted (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:763-771,1451-1463`).

2. **TFS config can be persisted even when verification fails**
   - Reproduce: on step 1, enter URL/project and click `Save`; make connection/API verification fail.
   - Result: phase 1 saves config before connection/API verification runs, leaving partial backend state (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs:333-412`).

3. **Rerunning onboarding can create duplicate profiles, products, or teams**
   - Reproduce: run onboarding from Settings after setup already exists; save the same profile/product/team again.
   - Result: the wizard does not load existing entities, and the repositories show no duplicate checks for profile/product/team creation (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/GettingStartedSection.razor:19-29`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:667-743`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProfileRepository.cs:42-65`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/ProductRepository.cs:74-137`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Repositories/TeamRepository.cs:49-86`).

4. **Repository step fails for existing products not created in the current wizard session**
   - Reproduce: rerun onboarding with an existing product already in the database, go to step 5, select repositories, click `Save Repositories`.
   - Result: save fails with “No product created yet” because the step only uses `_createdProductId` from the current wizard run (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:638,1360-1364`).

5. **Team step can create a team without linking it to any product**
   - Reproduce: skip step 3 or rerun onboarding without creating a product in the current session, then save a team on step 4.
   - Result: team is persisted, but no product link is created because `_createdProductId` is null (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:1232-1254`).

6. **Inline success feedback can hide a failed team link**
   - Reproduce: make `LinkTeamAsync` return false after team creation.
   - Result: snackbar warns, but inline alert still renders as success because `_teamSaveResult.Success` is set to `true` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:433-437,1243-1248`).

7. **Inline success feedback can hide partial repository failures**
   - Reproduce: save multiple repositories where at least one succeeds and at least one fails.
   - Result: snackbar warns, but inline alert shows success when `successCount > 0` (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:536-540,1414-1420`).

8. **Successful import exits the wizard immediately**
   - Reproduce: import a valid configuration that returns `CanImport=true` and `ImportExecuted=true`.
   - Result: onboarding completes and closes immediately; there is no final wizard step to review imported entities (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Settings/ConfigurationImportExportSection.razor:313-330`, `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:874-880`).

9. **Dialog outcome is ignored by the host page**
   - Reproduce: use either `Skip Wizard` or `Get Started`.
   - Result: `/onboarding` always navigates to `/sync-gate?returnUrl=%2Fhome` after the dialog closes (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Onboarding.razor:30-35`).

10. **Backlog root work item can be invalid but still looks acceptable until save or later use**
    - Reproduce: enter a numeric ID that does not exist in TFS.
    - Result: the wizard accepts the number format, creates the product, and shows no work item lookup/title confirmation because there is no TFS validation in this step (`/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingWizard.razor:287-293,1123-1145`).
