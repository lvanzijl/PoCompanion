# Contract boundary stress-test recovery report

Timestamp: 2026-03-31T21:21:55.665Z

## Detected prior progress

Recovery was based on repository state, recent commit history, and GitHub Actions evidence rather than restarting the full exercise.

Observed facts at recovery start:

- `HEAD` was commit `aafd4bee` with message `Changes before error encountered`.
- That commit changed exactly one file: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/FilteringDtos.cs`.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/Settings/ProductDto.cs` was already back at baseline, so the additive optional-field scenario had already been completed and restored.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/BuildQuality/BuildQualityPageDto.cs` was already back at baseline with `Summary`, so the rename scenario had already been completed and restored.
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/FilteringDtos.cs` still contained `public HashSet<int>? TargetIds { get; set; }`, so the nullability scenario was the only mutation left in progress.
- GitHub Actions run `23817636934` / job `69421371090` confirmed the interrupted state: NSwag completed successfully, then the build failed at `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/FilteringController.cs(49,79)` with `error CS8604` for nullable `targetIds`.

## Reconstructed current-state classification

### COMPLETED before recovery

1. **Shared DTO used across multiple pages: `ProductDto`**
   - Additive optional-field probe had already been executed.
   - Repository state showed the contract restored to baseline.
   - No recovery rerun was needed.

2. **API endpoint response DTO: `BuildQualityPageDto`**
   - Rename probe had already been executed.
   - Repository state showed the contract restored to baseline with `Summary` present.
   - No recovery rerun was needed.

### IN PROGRESS at recovery start

3. **Filter/request model: `FilterByValidationRequest`**
   - Nullability probe had been applied and committed.
   - Build/test cycle had not been cleanly completed on the branch baseline.
   - This was the only mutation cycle continued during recovery.

### NOT STARTED at recovery start

4. **Explicit ownership-violation proof after recovery**
   - A temporary guardrail probe against `nswag.json` had not been preserved in repository state.
   - This was executed during recovery because it was still missing evidence.

## Remaining steps executed

### 1. Stabilized the in-progress nullability mutation

Recovered mutation:

- File: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/WorkItems/FilteringDtos.cs`
- Mutation under recovery: `FilterByValidationRequest.TargetIds` changed from required/non-null to nullable.

Observed recovery-start failure after restore/build:

- `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --nologo`
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo`

Failure was immediate and compile-time:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Controllers/FilteringController.cs(49,79): error CS8604: Possible null reference argument for parameter 'targetIds'`

Temporary adaptation performed to complete the mutation cycle without shims:

- Backend updated to validate `request.TargetIds` explicitly before passing it into `WorkItemFilterer`.
- Test verification in `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Services/WorkItemFilteringServiceClientTests.cs` was adjusted to tolerate nullable request metadata while still asserting the client sends populated IDs.

Validation after adaptation:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo` ✅
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~NswagGovernanceTests|FullyQualifiedName~WorkItemFilteringServiceClientTests"` ✅

### 2. Executed the missing ownership-violation probe

To verify guardrails without re-running the whole stress matrix, recovery temporarily removed `FilterByValidationRequest` from the NSwag exclusion list in:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json`

Validation sequence:

- Baseline governance check:
  - `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~NswagGovernanceTests"` ✅
- Temporary violation check:
  - same command with the exclusion intentionally removed ❌

Observed failing assertion:

- `NSwag must exclude shared contract type 'FilterByValidationRequest'.`
- Failing test: `CanonicalNswagConfiguration_IsSingleAndUsesGovernedSnapshotSource`
- Source: `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/Audits/NswagGovernanceTests.cs:44`

This confirmed the guardrail catches ownership drift before silent regeneration can happen.

### 3. Restored clean baseline

After completing the nullability cycle and the ownership probe:

- Temporary backend/test adaptations were removed.
- `FilterByValidationRequest.TargetIds` was restored to required/non-null baseline.
- `nswag.json` was restored.

Final code validation on restored baseline:

- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore --nologo` ✅
- `dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --nologo -v minimal --filter "FullyQualifiedName~NswagGovernanceTests|FullyQualifiedName~WorkItemFilteringServiceClientTests"` ✅

## Guardrail behavior observed

### NSwag/shared ownership guardrails

Confirmed again during recovery:

- Shared contract types remain governed by `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/nswag.json` exclusions.
- The generated client location remains `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/ApiClient/Generated/ApiClient.g.cs`.
- `NswagGovernanceTests` fail immediately when a shared type exclusion is removed.
- No shared DTO was regenerated as part of recovery.

### Failure quality

Recovered failures were explicit and traceable:

- compile-time `CS8604` in `FilteringController` for the nullability mutation
- compile-time test failure in `WorkItemFilteringServiceClientTests` after the mutation propagated into Moq assertions
- explicit audit assertion when ownership exclusion was removed from `nswag.json`

No silent runtime-only failures were needed to detect drift.

## Any failures or weaknesses

Weak points discovered during recovery:

1. **Nullability changes can propagate into test expression trees after the main backend compile issue is fixed.**
   - This is acceptable, but it means the full adaptation surface for a nullable request model includes tests, not just production code.

2. **The interrupted run persisted the in-progress nullability mutation in a commit.**
   - Recovery was straightforward because the mutation was isolated to one file, but this is still a reminder that interrupted contract experiments should be restored before an intermediate progress commit whenever possible.

3. **Governance proof is stronger through tests than through regeneration attempts alone.**
   - The most reliable ownership-drift signal came from `NswagGovernanceTests`, which failed immediately and named the missing shared type directly.

## Final stability assessment

Recovery completed without restarting the full stress test and without repeating already restored mutation scenarios.

Final assessment:

- `ProductDto` additive probe: **completed earlier and confirmed restored**
- `BuildQualityPageDto` rename probe: **completed earlier and confirmed restored**
- `FilterByValidationRequest` nullability probe: **recovered, completed, validated, and restored**
- NSwag ownership guardrail proof: **executed during recovery and confirmed**
- Repository code baseline: **restored and stable**

Conclusion: the contract ownership boundary remains robust under controlled change. Shared DTO ownership stays enforced, drift is caught early by audit tests, and breakages surface at compile/test time rather than as silent runtime ambiguity.
