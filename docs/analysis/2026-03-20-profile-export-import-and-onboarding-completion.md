# Bug Analysis Report

## Metadata
- Bug: Import through onboarding does not fully complete startup readiness; export/import completeness needs verification
- Area: Settings / ImportExport / Onboarding
- Status: Analysis complete

## ROOT_CAUSE

- The confirmed issue is the onboarding/import readiness gap, not missing export content. Import through onboarding can succeed, but the wizard only becomes manually finishable after `HandleImportedConfigurationAsync()` sets `_configurationImported`; onboarding is not auto-completed by the import itself, and the actual completion flag is only written when the user clicks **Get Started** or **Skip Wizard** (`PoTool.Client/Components/Onboarding/OnboardingWizard.razor`, `PoTool.Client/Services/OnboardingService.cs`).
- Restart still routes to `/settings/tfs` because startup readiness is enforced separately from the client-side onboarding flag. `GetStartupReadinessQueryHandler` and `StartupOrchestratorService` require saved TFS config, successful connection test, successful API verification, at least one profile, and an active profile; `ApplyImportedTfsConfigurationAsync()` explicitly resets `HasTestedConnectionSuccessfully` and `HasVerifiedTfsApiSuccessfully` to `false`, so the import path leaves the app in a not-ready state until those checks are re-run (`PoTool.Api/Handlers/Settings/GetStartupReadinessQueryHandler.cs`, `PoTool.Client/Services/StartupOrchestratorService.cs`, `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).

## CURRENT_BEHAVIOR
- The configuration transfer contract already includes `TfsConfiguration`, `Settings`, `EffortEstimationSettings`, `StateClassifications`, `TriageTags`, `Profiles`, `Teams`, and `Products` in `ConfigurationExportDto` (`PoTool.Shared/Settings/ConfigurationTransferDto.cs`).
- Export is `SettingsController.ExportConfiguration()` → `ExportConfigurationService.ExportAsync()`, and it loads all profiles via `GetAllProfilesAsync()`, all teams, all products, and all stored state classifications; I did not find evidence that export is limited to only the active profile or that state classifications are omitted (`PoTool.Api/Controllers/SettingsController.cs`, `PoTool.Api/Services/Configuration/ExportConfigurationService.cs`).
- Import is `SettingsController.ImportConfiguration()` → `ImportConfigurationService.ImportAsync()`. It validates the JSON, temporarily applies the imported TFS configuration for live validation, restores importable profiles/teams/products, restores the active profile when the imported profile id can be mapped, and applies state classifications through `ApplyStateClassificationsAsync()` (`PoTool.Api/Controllers/SettingsController.cs`, `PoTool.Api/Services/Configuration/ImportConfigurationService.cs`).
- State classifications are stored and used per TFS project. Import removes existing rows for the imported project names and inserts the imported rows, while `WorkItemStateClassificationService` later reads classifications only for the currently configured project name. That means the current code supports project-scoped restore and lookup after import (`PoTool.Api/Services/Configuration/ImportConfigurationService.cs`, `PoTool.Api/Services/WorkItemStateClassificationService.cs`, `PoTool.Api/Persistence/Entities/WorkItemStateClassificationEntity.cs`).
- The onboarding flow still feels incomplete after a successful import because import only changes the footer action to **Get Started**; the user must still explicitly finish or skip the wizard (`PoTool.Client/Components/Onboarding/OnboardingWizard.razor`).
- Even after the user exits onboarding, a restart can route to `/settings/tfs` because the imported TFS configuration is treated as unvalidated. The startup page checks onboarding separately, then defers to readiness routing, which sends the user to `/settings/tfs` when TFS validation flags are false (`PoTool.Client/Pages/Index.razor`, `PoTool.Client/Services/StartupOrchestratorService.cs`, `PoTool.Api/Handlers/Settings/GetStartupReadinessQueryHandler.cs`).

## Comments on the Issue (you are @copilot in this section)

<comments>
I traced the export/import flow end to end and only the onboarding-completion/readiness problem is confirmed as a current defect.

For the verification questions:
1. **Does export include all profiles?** Yes. Export calls `GetAllProfilesAsync()` and writes every returned profile into `ConfigurationExportDto`.
2. **Does export include state classifications?** Yes. `ConfigurationExportDto` has a top-level `StateClassifications` collection, and `ExportConfigurationService` fills it from `WorkItemStateClassifications`.
3. **Does import restore all profiles?** It restores all profiles that pass the import validation rules. The import path is not active-profile-only, but invalid or non-importable profiles can still be skipped.
4. **Does import restore state classifications?** Yes. `ApplyStateClassificationsAsync()` deletes existing rows for the imported project names and inserts the imported rows.
5. **Are state classifications scoped and used correctly after import?** In the current code, yes. They are stored with `TfsProjectName`, and `WorkItemStateClassificationService` resolves classifications by the currently configured project.

The confirmed defect is that onboarding import does not leave the app in a fully usable post-import state. There are effectively two gates:
- a client-side onboarding-completed preference; and
- a backend startup-readiness check.

Import through onboarding can satisfy neither gate automatically: it does not complete onboarding on its own, and it also resets TFS validation readiness. That is why the experience feels incomplete immediately after import and why a later restart still routes to `/settings/tfs`.
</comments>
