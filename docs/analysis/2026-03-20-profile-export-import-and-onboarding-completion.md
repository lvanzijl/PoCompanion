# Bug Analysis Report

## Metadata
- Bug: Profile export/import is incomplete and does not fully complete onboarding
- Area: Settings / ImportExport / Onboarding
- Status: Analysis complete

## ROOT_CAUSE

- **Export/import completeness:** I did not find a current defect in the configuration transfer path for state classifications or profile scope.
- `ConfigurationExportDto` already carries `StateClassifications` as a top-level collection (`PoTool.Shared/Settings/ConfigurationTransferDto.cs`).
- `ExportConfigurationService` fills that collection from `WorkItemStateClassifications`, and `ImportConfigurationService` persists it again through `ApplyStateClassificationsAsync` (`PoTool.Api/Services/Configuration/ExportConfigurationService.cs`, `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- Export scope is all profiles because export calls `GetAllProfilesAsync()` without any active-profile filter (`PoTool.Api/Services/Configuration/ExportConfigurationService.cs`).
- The most likely root cause of the completeness observation is issue-framing drift: this is a full configuration export/import, not a single-profile export, and state classifications are configuration-level data rather than data nested inside an individual profile.
- **Onboarding completion after import:** The onboarding wizard and startup readiness use different completion criteria.
- The wizard only becomes explicitly finishable after import by setting `_configurationImported` and showing **Get Started** (`PoTool.Client/Components/Onboarding/OnboardingWizard.razor`).
- App startup does not trust that client-side onboarding flag alone; it routes from backend readiness (`PoTool.Api/Handlers/Settings/GetStartupReadinessQueryHandler.cs`, `PoTool.Client/Services/StartupOrchestratorService.cs`).
- During import, `ApplyImportedTfsConfigurationAsync` explicitly resets `HasTestedConnectionSuccessfully` and `HasVerifiedTfsApiSuccessfully` to `false` (`PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- That is why restart sends the user back to `/settings/tfs` even if profiles and settings were imported successfully.

## CURRENT_BEHAVIOR
- The configuration transfer contract is `ConfigurationExportDto`, which contains `TfsConfiguration`, `Settings`, `EffortEstimationSettings`, `StateClassifications`, `TriageTags`, `Profiles`, `Teams`, and `Products` (`PoTool.Shared/Settings/ConfigurationTransferDto.cs`).
- Export is handled by `SettingsController.ExportConfiguration()` → `ExportConfigurationService.ExportAsync()` (`PoTool.Api/Controllers/SettingsController.cs`, `PoTool.Api/Services/Configuration/ExportConfigurationService.cs`).
- That export service loads **all** profiles, **all** teams, **all** products, the latest TFS config, application settings, effort settings, state classifications, and triage tags, then returns them in one file (`PoTool.Api/Services/Configuration/ExportConfigurationService.cs`).
- Import is handled by `SettingsController.ImportConfiguration()` → `ImportConfigurationService.ImportAsync()` (`PoTool.Api/Controllers/SettingsController.cs`, `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- Import first validates the JSON and temporarily applies the imported TFS configuration so it can validate connection, project, teams, repositories, work item types, and backlog roots against TFS before persisting data (`PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- Import restores profiles, teams, products, repository links, application settings, effort settings, triage tags, and state classifications. State classifications are not ignored in the current code path; `ApplyStateClassificationsAsync()` removes existing classifications for the imported project names and inserts the imported entries (`PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- Export scope is global, not active-profile-only. The only selectivity happens on import, where invalid or non-importable profiles/teams/products can be skipped after validation; the export file itself contains every profile returned by `GetAllProfilesAsync()` (`PoTool.Api/Services/Configuration/ExportConfigurationService.cs`, `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- Onboarding completion is stored only in client preferences by `OnboardingService.MarkOnboardingCompletedAsync()` / `MarkOnboardingSkippedAsync()`. The onboarding page checks that flag to decide whether to show the wizard, but startup routing after that is controlled separately by backend readiness (`PoTool.Client/Services/OnboardingService.cs`, `PoTool.Client/Pages/Index.razor`).
- In the onboarding wizard, a successful import does **not** automatically mark onboarding complete. `HandleImportedConfigurationAsync()` only sets `_configurationImported`, which swaps the footer CTA to **Get Started**. The actual completion flag is written only when the user clicks **Get Started** (or **Skip Wizard**) (`PoTool.Client/Components/Onboarding/OnboardingWizard.razor`).
- Even after the user exits the wizard correctly, restart can still route to `/settings/tfs` because startup readiness in real-data mode requires saved TFS config, successful connection test, successful API verification, at least one profile, and an active profile. Import clears the two TFS validation flags, so the readiness check fails before profile availability is even considered (`PoTool.Api/Handlers/Settings/GetStartupReadinessQueryHandler.cs`, `PoTool.Client/Services/StartupOrchestratorService.cs`, `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the end-to-end flow and the current code does not support the assumption that configuration export/import is only exporting one profile. It also does not support the assumption that state classifications are currently omitted from the transfer payload. The current transfer model is already a full configuration snapshot, and both the export and import services have explicit state-classification handling.

So I would not treat “missing exported/imported state classifications” as the present root defect without first confirming the user was testing this exact configuration-transfer feature and not an older file or a different path.

The onboarding complaint is real, but the strongest evidence says it is not primarily an “onboarding flag not written” bug. The app has two separate notions of completion: local onboarding completion in browser preferences, and backend startup readiness. Import through onboarding can leave the wizard and even be marked completed.

The app still redirects to TFS configuration on restart because the import path deliberately clears the TFS validation flags. Startup routing blocks on those flags before it cares about imported profiles.

So the practical analysis outcome is:
1. configuration export/import already appears to cover all profiles and already includes/restores state classifications in the current codebase; and
2. the restart redirect is explained by the import path bypassing the normal “ready to use” state, because imported TFS configuration is treated as unvalidated and startup routing enforces re-validation.
</comments>
