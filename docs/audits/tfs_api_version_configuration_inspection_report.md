# TFS API Version Configuration Inspection Report

## 1. Current configuration model

- The main application uses one shared persisted API-version value: `TfsConfigEntity.ApiVersion` in `PoTool.Shared/Settings/TfsConfigEntity.cs`.
- `RealTfsClient` centralizes most URL construction through two helpers in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Core.cs`:
  - `CollectionUrl(...)` appends `api-version={config.ApiVersion}`
  - `ProjectUrl(...)` appends `api-version={config.ApiVersion}`
- BuildQuality retrieval follows that shared model. Its build, test-run, and coverage requests all use `ProjectUrl(...)`, so they all consume the same stored `ApiVersion` value.
- There is not a separate endpoint-specific configuration model for build, test-run, or coverage requests today.
- There is one hardcoded runtime exception outside that shared model: `GetTfsProjectsAsync(...)` in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs` builds `/_apis/projects?api-version=7.0` directly.

## 2. Where versions are defined

### Main application runtime

- Persisted configuration class:
  - `TfsConfigEntity.ApiVersion` in `PoTool.Shared/Settings/TfsConfigEntity.cs`
  - Default: `"7.0"`
- API service DTO:
  - `TfsConfig.ApiVersion` in `PoTool.Api/Services/TfsConfigurationService.cs`
  - Default: `"7.0"`
- API request model:
  - `TfsConfigRequest.ApiVersion` in `PoTool.Api/Configuration/ApiApplicationBuilderExtensions.cs`
  - Default: `"7.0"`
- Client DTO:
  - `TfsConfigDto.ApiVersion` in `PoTool.Client/Services/TfsConfigService.cs`
  - Default: `"7.0"`

### Appsettings keys

- The main API runtime does **not** define a checked-in `ApiVersion` appsettings key in `PoTool.Api/appsettings.json`.
- The checked-in API config only includes `TfsIntegration.UseMockClient`; the live TFS API version is stored via the persisted TFS configuration record instead of appsettings.
- There is one appsettings-based consumer outside the main runtime:
  - Key: `Tfs:ApiVersion`

### Defaults and fallback behavior

- `TfsConfigEntity.ApiVersion` defaults to `"7.0"` when a new entity is created.
- `TfsConfigurationService.SaveConfigAsync(...)` falls back to `"7.0"` via `apiVersion ?? "7.0"` when saving configuration.
- The TFS configuration endpoints also fall back to `"7.0"` by passing `req.ApiVersion ?? "7.0"` into `SaveConfigAsync(...)`.
- BuildQuality retrieval itself does **not** have a separate fallback path. It requires a stored TFS configuration and then uses that configuration as-is after `ValidateTfsConfiguration(...)`.
- `GetTfsProjectsAsync(...)` does not read the stored version at all; it hardcodes `7.0`.

## 3. BuildQuality endpoint usage

### Supporting build endpoint

- Method: `GetBuildMetadataAsync(...)`
- File: `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`
- Exact endpoint path:
  - `/_apis/build/builds?buildIds={comma-separated ids}&$top={batch.Length}`
- Exact api-version source:
  - `ProjectUrl(config, ...)` in `RealTfsClient.Core.cs`
  - Effective query parameter: `api-version={config.ApiVersion}`
- Preview suffix support/configurability:
  - No build-specific preview flag or suffix handling exists.
  - If the stored global `ApiVersion` string contains a preview suffix, that exact string is used.
- Hardcoded?
  - No

### Test-run retrieval

- Method: `GetTestRunsByBuildIdsAsync(...)`
- File: `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`
- Exact endpoint path:
  - `/_apis/testresults/runs?minLastUpdatedDate={window.Start:O}&maxLastUpdatedDate={window.End:O}&buildIds={comma-separated ids}`
- Exact api-version source:
  - `ProjectUrl(config, ...)` in `RealTfsClient.Core.cs`
  - Effective query parameter: `api-version={config.ApiVersion}`
- Preview suffix support/configurability:
  - No test-run-specific preview option exists.
  - A preview suffix is only configurable indirectly by changing the single stored `ApiVersion` string.
- Hardcoded?
  - No

### Coverage retrieval

- Method: `GetCoverageByBuildIdsAsync(...)` -> `GetCoverageForBuildAsync(...)`
- File: `PoTool.Integrations.Tfs/Clients/RealTfsClient.BuildQuality.cs`
- Exact endpoint path:
  - `/_apis/testresults/codecoverage?buildId={buildId}`
- Exact api-version source:
  - `ProjectUrl(config, ...)` in `RealTfsClient.Core.cs`
  - Effective query parameter: `api-version={config.ApiVersion}`
- Preview suffix support/configurability:
  - No coverage-specific preview option exists.
  - A preview suffix is only configurable indirectly by changing the single stored `ApiVersion` string.
- Hardcoded?
  - No

## 4. Hardcoded vs configured usage

### Configured usage

- The dominant pattern is one configured shared version sourced from `TfsConfigEntity.ApiVersion`.
- That shared version is applied centrally by `CollectionUrl(...)` and `ProjectUrl(...)`.
- BuildQuality retrieval uses configured versioning for:
  - build metadata (`_apis/build/builds`)
  - test runs (`_apis/testresults/runs`)
  - coverage (`_apis/testresults/codecoverage`)

### Hardcoded usage

- One runtime endpoint is hardcoded:
  - `GetTfsProjectsAsync(...)` in `PoTool.Integrations.Tfs/Clients/RealTfsClient.Teams.cs`
  - Exact path: `/_apis/projects?api-version=7.0`
- The rest of the runtime client does not expose endpoint-family-specific version overrides.

### Answer to “Is one shared version used for all TFS endpoints?”

- Almost yes.
- In practice, the codebase uses one shared configured version for the main TFS runtime client, with one hardcoded discovery exception for project listing.
- There is no dedicated BuildQuality version, no dedicated build version, and no dedicated test-results version.

## 5. Risks in current setup

- **Shared-version coupling risk:** changing `ApiVersion` for BuildQuality also changes work items, pipelines, git/pull requests, verification, and most team endpoints because they all flow through the same stored value.
- **No endpoint-family escape hatch:** if `_apis/testresults/runs` or `_apis/testresults/codecoverage` require a different or preview version, there is no scoped override; the only current option is to change the single global `ApiVersion`.
- **Hardcoded discovery drift risk:** `GetTfsProjectsAsync(...)` ignores the configured version entirely, so project discovery can drift from the version used by the rest of the client.
- **Repeated default literal risk:** `"7.0"` is repeated in several layers (`TfsConfigEntity`, `TfsConfig`, `TfsConfigRequest`, API save endpoints, client DTO, and `GetTfsProjectsAsync(...)`), so a future default change would require multi-file coordination.

## 6. Minimal safe improvement

- Keep the current global `ApiVersion` as the default behavior.
- Introduce one small central version-resolution point for BuildQuality-related requests so build/test-results/coverage endpoints can continue to default to the global value while allowing a future localized override if needed.
- At minimum, also remove the lone hardcoded `GetTfsProjectsAsync(...)` literal so project discovery stops bypassing the same central version source.

This is the smallest safe structural improvement because it preserves the current one-version model, avoids a broad redesign, and localizes the only two current problems: shared-version inflexibility for BuildQuality and one direct hardcoded `7.0` call.
