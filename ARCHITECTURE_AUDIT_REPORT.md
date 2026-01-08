# Architecture Audit Report
## PoCompanion Codebase Review

**Date:** 2026-01-08  
**Auditor:** Senior Software Developer  
**Scope:** Architectural compliance, code duplication, compilation status, recent NSwag changes

---

## Executive Summary

The PoCompanion codebase is in a **non-compilable state** with **89 build errors**. Critical architectural violations have been identified, primarily:

1. **Systematic enum duplication** between Client and Core projects (12 duplicated enums)
2. **Violation of the shared project's intent** - shared types are not being placed in PoTool.Shared
3. **NSwag-generated client issues** - type mismatches and missing methods
4. **Architecture boundary confusion** - Client contains duplicates of Core types despite explicit prohibition on Client→Core references

The system requires immediate intervention to restore architectural integrity and compilation capability.

---

## 1. Compilation Status: FAILING

### Build Result
- **Status:** FAILED
- **Error Count:** 89 errors
- **Affected Project:** PoTool.Client

### Error Categories

#### 1.1 Type Conversion Errors (NSwag-Generated Code)
**Count:** 3 errors in `ApiClient.g.cs`

```
ApiClient.g.cs(15002,53): error CS0029: Cannot implicitly convert type 'string' to 'int'
ApiClient.g.cs(14742,51): error CS0029: Cannot implicitly convert type 'string' to 'int'
ApiClient.g.cs(11784,53): error CS0029: Cannot implicitly convert type 'string' to 'int'
```

**Root Cause:** NSwag code generator produced incorrect type mappings. OpenAPI specification likely defines these as strings when they should be integers, or vice versa.

#### 1.2 Missing API Client Methods
**Count:** 31 errors across multiple service files

Missing methods on generated clients:
- `IWorkItemsClient`: GetAllAsync, GetFilteredAsync, GetByTfsIdAsync, GetAllGoalsAsync, GetGoalHierarchyAsync, GetAllWithValidationAsync, GetWorkItemRevisionsAsync, GetValidationHistoryAsync, GetStateTimelineAsync, GetDependencyGraphAsync, GetValidationImpactAnalysisAsync
- `IMetricsClient`: GetVelocityTrendAsync, GetMultiIterationBacklogHealthAsync, GetEpicForecastAsync, GetEffortDistributionAsync, GetEffortImbalanceAsync, GetEffortDistributionTrendAsync, GetEffortConcentrationRiskAsync
- `IHealthCalculationClient`: CalculateHealthScoreAsync
- `IFilteringClient`: FilterByValidationWithAncestorsAsync, GetWorkItemIdsByValidationFilterAsync, CountWorkItemsByValidationFilterAsync
- `IClient`: PostApiTfsconfigAsync, PostApiWorkitemsSyncAsync
- `ISettingsClient`: GetSettingsAsync
- `IPullRequestsClient`: GetAllAsync, GetByIdAsync, GetMetricsAsync, GetFilteredAsync
- `IProfilesClient`: GetAllProfilesAsync, GetProfileByIdAsync, GetActiveProfileAsync, CreateProfileAsync, UpdateProfileAsync, DeleteProfileAsync, SetActiveProfileAsync
- `IPipelinesClient`: GetAllAsync, GetMetricsAsync, GetRunsAsync

**Root Cause:** OpenAPI specification is incomplete or controllers are not properly decorated for OpenAPI generation. The NSwag-generated client is missing most of the API surface.

#### 1.3 Enum Type Conversion Errors
**Count:** 13 errors

Enums from API responses cannot convert to Client enum types:
- `int` → `TrendDirection`
- `int` → `BottleneckSeverity`
- `int` → `PipelineType`
- `int?` → `PipelineRunResult?`
- `int` → `ForecastConfidence`
- `int` → `DependencyChainRisk`
- `int` → `DependencyLinkType`
- `int` → `ImbalanceRiskLevel`
- `int` → `EffortTrendDirection`
- `int` → `ConcentrationRiskLevel`

**Root Cause:** NSwag is generating DTO types with enum properties as `int` instead of the actual enum type. This suggests:
1. Enums are not properly serialized in OpenAPI spec
2. Or NSwag is not configured to preserve enum types
3. Or enums are defined in Core but not exposed through API contracts

#### 1.4 Missing Enum Types in Client
**Count:** 4 errors

```
EffortDistribution.razor(676,13): error CS0103: The name 'CapacityStatus' does not exist
```

**Root Cause:** `CapacityStatus` enum exists in `PoTool.Core.Metrics.EffortDistributionDto` but has no corresponding type in Client.

#### 1.5 TimeSpan vs String Type Mismatches
**Count:** 13 errors

Properties treated as `string` when code expects `TimeSpan`:
- `.TotalDays` called on `string` (should be `TimeSpan`)
- `.HasValue` and `.Value` called on `string` (should be `TimeSpan?`)

**Root Cause:** NSwag configuration or OpenAPI spec serializes TimeSpan as string (ISO 8601 duration), but Client code expects native TimeSpan objects.

#### 1.6 DTO Type Mismatches
**Count:** 1 error

```
TfsConfig.razor(226,33): error CS0029: Cannot implicitly convert type 
  'PoTool.Client.Services.StartupReadinessDto' to 
  'PoTool.Client.ApiClient.StartupReadinessDto'
```

**Root Cause:** `StartupReadinessDto` defined twice - once in Client.Services and once in generated ApiClient. This is duplication that NSwag generation didn't account for.

---

## 2. Architectural Violations

### 2.1 Enum Duplication Between Client and Core

**Severity:** CRITICAL  
**Rule Violated:** Process Rules §5 (Duplication Rules)

#### Duplicated Enums

| Enum Name | Client Location | Core Location | Members Match |
|-----------|----------------|---------------|---------------|
| `DataMode` | `Client/Models/DataMode.cs` | `Core/Settings/DataMode.cs` | ✓ |
| `DependencyLinkType` | `Client/Models/DependencyLinkType.cs` | `Core/WorkItems/DependencyGraphDto.cs` | ✓ |
| `PipelineType` | `Client/Models/PipelineType.cs` | `Core/Pipelines/PipelineDto.cs` | ✓ |
| `PipelineRunResult` | `Client/Models/PipelineRunResult.cs` | `Core/Pipelines/PipelineRunDto.cs` | ✓ |
| `TrendDirection` | `Client/Models/TrendDirection.cs` | `Core/Metrics/MultiIterationBacklogHealthDto.cs` | ✓ |
| `BottleneckSeverity` | `Client/Models/BottleneckSeverity.cs` | `Core/WorkItems/WorkItemStateTimelineDto.cs` | ✓ |
| `DependencyChainRisk` | `Client/Models/DependencyChainRisk.cs` | `Core/WorkItems/DependencyGraphDto.cs` | ✓ |
| `ImbalanceRiskLevel` | `Client/Models/ImbalanceRiskLevel.cs` | `Core/Metrics/EffortImbalanceDto.cs` | ✓ |
| `EffortTrendDirection` | `Client/Models/EffortTrendDirection.cs` | `Core/Metrics/EffortDistributionTrendDto.cs` | ✓ |
| `ConcentrationRiskLevel` | `Client/Models/ConcentrationRiskLevel.cs` | `Core/Metrics/EffortConcentrationRiskDto.cs` | ✓ |
| `ForecastConfidence` | `Client/Models/ForecastConfidence.cs` | `Core/Metrics/EpicCompletionForecastDto.cs` | ✓ |
| `ProfilePictureType` | `Client/Models/ProfilePictureType.cs` | `Core/Settings/ProfileDto.cs` | ✓ |

**Total Duplicated Enums:** 12

#### Evidence of Known Duplication

Client enum files contain explicit acknowledgment of duplication:

```csharp
// From Client/Models/DependencyLinkType.cs
/// <summary>
/// Type of dependency link between work items.
/// Client-side copy that matches PoTool.Core.WorkItems.DependencyLinkType.
/// </summary>
public enum DependencyLinkType
```

This comment proves that the duplication was intentional but violates process rules.

#### Missing Client Enums

Additional enums exist in Core but NOT in Client:
- `CapacityStatus` (Core/Metrics)
- `WarningLevel` (Core/Metrics)
- `MitigationStrategy` (Core/Metrics)
- `RecommendationType` (Core/Metrics)
- `ReviewerStatus` (Core/PullRequests)
- `PipelineRunTrigger` (Core/Pipelines)
- `FailureCategory` (Core/Contracts/TfsVerification)
- `CleanupStatus` (Core/Contracts/TfsVerification)
- `MutationType` (Core/Contracts/TfsVerification)

**Impact:** Missing `CapacityStatus` causes 4 build errors in EffortDistribution.razor.

---

### 2.2 Violation of Shared Project Intent

**Severity:** CRITICAL  
**Rule Violated:** COPILOT_ARCHITECTURE_CONTRACT.md - Layering Rules

#### Current Project References

```
PoTool.Client → PoTool.Shared ✓
PoTool.Core → PoTool.Shared ✓
PoTool.Api → PoTool.Core + PoTool.Client + PoTool.Shared ✓
```

#### The Problem

The architecture explicitly prohibits `Client → Core` references. However:

1. **Enums belong in Shared:** Types used by both Client and Core should be in PoTool.Shared, not duplicated.
2. **Shared is underutilized:** PoTool.Shared contains only 6 files, mostly DTOs. It should contain all shared enums and contracts.
3. **Workaround created duplication:** Because enums were placed in Core DTOs (inside the same file), Client had to duplicate them to avoid referencing Core.

#### Architectural Intent

From COPILOT_ARCHITECTURE_CONTRACT.md:

> * Core MUST NOT reference: ASP.NET Core, EF Core, SignalR, HTTP, TFS APIs, UI frameworks
> * Api MAY reference Core.
> * Frontend MAY reference generated API clients only.

The intent of PoTool.Shared is to hold types that are truly shared across layers without introducing layering violations.

---

### 2.3 NSwag Configuration Issues

**Severity:** HIGH  
**Rule Violated:** Implicit - code must compile

#### Configuration Inconsistencies

1. **File Reference Mismatch:**
   - `nswag.json` references: `"json": "openapi.json"`
   - `.csproj` build target uses: `swagger.json`
   - **Impact:** Unclear which file is source of truth

2. **Enum Handling:**
   - Config setting: `"enforceFlagEnums": false`
   - Config setting: `"generateDtoTypes": true`
   - **Result:** Enums are not being generated as enum types in DTOs
   - **Expected:** Enums should be preserved as enum types

3. **TimeSpan Handling:**
   - Config setting: `"timeSpanType": "System.TimeSpan"`
   - **Result:** TimeSpans are being generated as strings
   - **Expected:** Native TimeSpan types

4. **Missing Methods:**
   - Most API endpoints are not appearing in generated client
   - **Possible causes:**
     - Controllers missing `[ApiController]` attribute
     - Methods missing HTTP verb attributes
     - OpenAPI generation not running on build
     - OpenAPI spec not being updated before NSwag runs

---

## 3. Compliance with Architecture Rules

### 3.1 Clean Architecture Principles (ARCHITECTURE_RULES.md)

| Rule | Status | Evidence |
|------|--------|----------|
| Separation of Concerns | ⚠️ PARTIAL | Layers exist but boundaries are blurred by enum duplication |
| Dependency Rule | ✓ PASS | No upward dependencies detected in project references |
| Domain layer isolation | ✓ PASS | No Core → Api or Core → Client references |

### 3.2 CQRS Pattern

| Rule | Status | Evidence |
|------|--------|----------|
| Command/Query separation | ✓ PASS | Commands and Queries properly structured in Core |
| MediatR Integration | ✗ FAIL | Using source-generated Mediator (correct per contract) |
| No business logic in controllers | ⚠️ PARTIAL | Cannot verify - NSwag generation suggests incomplete API |

### 3.3 UI Rules

| Rule | Status | Evidence |
|------|--------|----------|
| Client MUST NOT reference Core | ✓ PASS | No project reference exists |
| Client MUST NOT reference ASP.NET | ✓ PASS | Only Blazor WASM packages |
| Typed frontend services only | ✓ PASS | Services use generated API clients |
| No direct HttpClient usage | ✓ PASS | All services use abstracted clients |

### 3.4 Process Rules - Duplication

| Rule | Status | Evidence |
|------|--------|----------|
| No UI duplication | ✓ PASS | Components appear properly factored |
| No backend duplication | ✓ PASS | No duplicated logic detected in Core |
| **No type duplication** | ✗ **FAIL** | **12 enums duplicated between Client and Core** |

---

## 4. Impact of Recent Changes

### 4.1 Last 5 Pull Requests Analysis

Based on commit history, most recent work involved:
- PR #189: "Complete PR #187 Next Steps: OpenAPI automation, NSwag configuration..."
- NSwag integration was recently added or modified

### 4.2 NSwag Integration Issues

The NSwag integration appears incomplete:

1. **Configuration added but not working:** Build target exists in `.csproj` but generated client has errors
2. **Partial generation:** Some DTOs generated but missing most controller methods
3. **Type mapping broken:** Enums, TimeSpans, and some primitives not mapping correctly
4. **No enum reuse strategy:** Generated DTOs contain inline enum definitions as ints instead of referencing shared enums

---

## 5. Root Cause Analysis

### Why is the code in this state?

1. **Enum placement in Core DTOs:** Enums were co-located with DTOs in Core as nested or adjacent types. This is convenient but creates the duplication problem.

2. **Client needs these types:** Client needs enum types to:
   - Deserialize API responses
   - Provide type safety in components
   - Display enum-based UI (badges, colors, filters)

3. **Cannot reference Core:** Architecture correctly prohibits Client → Core reference.

4. **Workaround: Duplication:** Instead of moving enums to Shared, they were duplicated in Client. Comments even acknowledge this: "Client-side copy that matches PoTool.Core".

5. **NSwag didn't solve it:** Recent NSwag integration generated DTOs with enums as `int` instead of reusing existing enum types. This created type conversion errors.

6. **OpenAPI spec incomplete:** Many endpoints missing from generated client suggests OpenAPI spec generation is broken or incomplete.

---

## 6. Systemic Risks

### 6.1 Maintenance Burden

- **12 enums must be kept in sync manually:** Any change to an enum in Core must be manually replicated in Client
- **No compile-time safety:** If enums diverge, bugs will only appear at runtime during deserialization
- **High risk of drift:** Already seeing drift - CapacityStatus missing from Client

### 6.2 Onboarding Confusion

- **Unclear which enum to use:** Developers must know to use Client enums in Client, Core enums in Core
- **Breaking the pattern is easy:** Adding a new enum "just in Core" will cause immediate Client errors
- **Architecture rules violated:** Process rules explicitly forbid duplication but code contains systematic duplication

### 6.3 Build Fragility

- **System does not compile:** Cannot ship or test current codebase
- **NSwag errors obscure other issues:** 89 errors make it hard to see other problems
- **No CI safety net:** If CI doesn't catch this, future PRs could break builds again

---

## 7. Recommended Refactoring Steps

### Phase 1: Restore Compilation (IMMEDIATE)

**Goal:** Get the solution to compile so further work is possible.

#### Step 1.1: Fix NSwag Configuration
1. Determine correct OpenAPI spec file (openapi.json vs swagger.json)
2. Configure NSwag to preserve enum types:
   ```json
   "enforceFlagEnums": false,
   "generateDtoTypes": true,
   "typeNameGeneratorType": "CustomEnumTypeNameGenerator"  // If available
   ```
3. Configure proper TimeSpan handling or use string with parsing helpers

#### Step 1.2: Regenerate API Client
1. Ensure all controllers are properly decorated for OpenAPI
2. Run OpenAPI generation: `dotnet build` or explicit swagger gen
3. Verify openapi.json/swagger.json contains all endpoints
4. Run NSwag generation
5. Verify generated client compiles

#### Step 1.3: Short-term Enum Fix (if NSwag still generates int)
If NSwag cannot be configured to use enum types:
1. Keep duplicated Client enums temporarily
2. Add explicit conversion layer in services:
   ```csharp
   var clientEnum = (ClientEnumType)(int)apiResponseEnum;
   ```
3. Document this as technical debt to be removed in Phase 2

---

### Phase 2: Eliminate Enum Duplication (HIGH PRIORITY)

**Goal:** Move all shared enums to PoTool.Shared to eliminate duplication.

#### Step 2.1: Move Core Enums to Shared

For each duplicated enum:
1. Create enum file in `PoTool.Shared/[Category]/`
   - Example: `PoTool.Shared/WorkItems/DependencyLinkType.cs`
   - Example: `PoTool.Shared/Metrics/TrendDirection.cs`
   - Example: `PoTool.Shared/Settings/DataMode.cs`

2. Update Core DTOs to use Shared enums:
   ```csharp
   // Before:
   public record DependencyLink(..., DependencyLinkType LinkType, ...);
   public enum DependencyLinkType { ... }  // In same file
   
   // After:
   using PoTool.Shared.WorkItems;
   public record DependencyLink(..., DependencyLinkType LinkType, ...);
   // Enum now in PoTool.Shared
   ```

3. Update Client to use Shared enums:
   - Delete `PoTool.Client/Models/[EnumName].cs`
   - Add `using PoTool.Shared.[Category];` to components
   - Update namespace references throughout Client

4. Update NSwag configuration:
   ```json
   "additionalNamespaceUsages": [
     "PoTool.Shared.WorkItems",
     "PoTool.Shared.Metrics",
     "PoTool.Shared.Settings"
   ]
   ```

#### Step 2.2: Add Missing Enums

Move remaining Core-only enums to Shared:
- `CapacityStatus`
- `WarningLevel`
- `MitigationStrategy`
- `RecommendationType`
- `ReviewerStatus`
- `PipelineRunTrigger`

#### Step 2.3: Verification

1. Ensure no `PoTool.Client/Models/*Enum*.cs` files remain
2. Ensure all enums are in `PoTool.Shared/[Category]/`
3. Run full solution build
4. Run all tests
5. Verify no runtime serialization issues

---

### Phase 3: Strengthen Architecture Guardrails (MEDIUM PRIORITY)

**Goal:** Prevent duplication from recurring.

#### Step 3.1: Linting Rules

Add Roslyn analyzers or custom rules:
1. **Rule:** No public enums in `PoTool.Core` except nested private enums
2. **Rule:** No public enums in `PoTool.Client/Models`
3. **Rule:** All shared enums must be in `PoTool.Shared`

#### Step 3.2: Documentation

Update architecture docs:
1. **ARCHITECTURE_RULES.md:** Add section on enum placement
2. **PROCESS_RULES.md:** Add specific rule about shared types
3. **README.md:** Add developer quick reference

#### Step 3.3: PR Template

Add checklist items:
- [ ] New enums added to PoTool.Shared, not Client or Core
- [ ] No type duplication introduced
- [ ] NSwag client regenerated if DTOs changed

---

### Phase 4: Fix OpenAPI Generation (MEDIUM PRIORITY)

**Goal:** Ensure complete and correct OpenAPI specification.

#### Step 4.1: Controller Audit

For each controller:
1. Verify `[ApiController]` attribute
2. Verify HTTP verb attributes (`[HttpGet]`, `[HttpPost]`, etc.)
3. Verify return types are documented: `[ProducesResponseType(typeof(MyDto), 200)]`
4. Verify route templates are correct

#### Step 4.2: OpenAPI Configuration

In API project:
1. Ensure `AddOpenApi()` or `AddSwaggerGen()` is configured correctly
2. Ensure XML documentation is enabled:
   ```xml
   <GenerateDocumentationFile>true</GenerateDocumentationFile>
   ```
3. Configure enum serialization:
   ```csharp
   options.JsonSerializerOptions.Converters.Add(
       new JsonStringEnumConverter());
   ```

#### Step 4.3: Build Integration

1. Add explicit OpenAPI generation step before NSwag:
   ```xml
   <Target Name="GenerateOpenApi" BeforeTargets="NSwag">
     <Exec Command="dotnet swagger tofile --output openapi.json" />
   </Target>
   ```

2. Or use NSwag's document generation from assembly:
   ```json
   "documentGenerator": {
     "aspNetCoreToOpenApi": {
       "project": "../PoTool.Api/PoTool.Api.csproj",
       "msBuildProjectExtensionsPath": null,
       "configuration": null,
       "runtime": null,
       "targetFramework": "net10.0",
       "noBuild": false,
       "verbose": true,
       "workingDirectory": null,
       "requireParametersWithoutDefault": true,
       "apiGroupNames": null,
       "defaultPropertyNameHandling": "Default",
       "defaultReferenceTypeNullHandling": "Null",
       "defaultDictionaryValueReferenceTypeNullHandling": "NotNull",
       "defaultResponseReferenceTypeNullHandling": "NotNull",
       "defaultEnumHandling": "Integer",
       "flattenInheritanceHierarchy": false,
       "generateKnownTypes": true,
       "generateEnumMappingDescription": false,
       "generateXmlObjects": false,
       "generateAbstractProperties": false,
       "generateAbstractSchemas": true,
       "ignoreObsoleteProperties": false,
       "allowReferencesWithProperties": false,
       "excludedTypeNames": [],
       "serviceHost": null,
       "serviceBasePath": null,
       "serviceSchemes": [],
       "infoTitle": "PoCompanion API",
       "infoDescription": null,
       "infoVersion": "1.0.0",
       "documentTemplate": null,
       "documentProcessors": [],
       "operationProcessors": [],
       "typeNameGenerator": null,
       "schemaNameGenerator": null,
       "contractResolver": null,
       "serializerSettings": null,
       "useRouteNameAsOperationId": false,
       "aspNetCoreEnvironment": null,
       "createWebHostBuilderMethod": null,
       "startupType": null,
       "allowNullableBodyParameters": true,
       "output": "openapi.json",
       "outputType": "OpenApi3",
       "assemblyPaths": [],
       "assemblyConfig": null,
       "referencePaths": [],
       "useNuGetCache": false
     }
   }
   ```

---

## 8. Priority Matrix

| Issue | Severity | Effort | Priority |
|-------|----------|--------|----------|
| Non-compilable solution | CRITICAL | HIGH | P0 - IMMEDIATE |
| Enum duplication | CRITICAL | MEDIUM | P1 - HIGH |
| Missing API methods in client | HIGH | MEDIUM | P1 - HIGH |
| NSwag type mapping issues | HIGH | MEDIUM | P1 - HIGH |
| OpenAPI spec incomplete | MEDIUM | LOW | P2 - MEDIUM |
| Missing CapacityStatus enum | HIGH | LOW | P1 - HIGH |
| Architecture documentation gaps | LOW | LOW | P3 - LOW |

---

## 9. Estimated Effort

| Phase | Effort | Duration |
|-------|--------|----------|
| Phase 1: Restore Compilation | 4-8 hours | 1 day |
| Phase 2: Eliminate Duplication | 6-12 hours | 1-2 days |
| Phase 3: Architecture Guardrails | 2-4 hours | 0.5 days |
| Phase 4: Fix OpenAPI Generation | 4-6 hours | 1 day |
| **Total** | **16-30 hours** | **3.5-4.5 days** |

---

## 10. Recommended Immediate Actions

### For Repository Owner

1. **Stop merging PRs** until solution compiles
2. **Review last 5 PRs** that introduced these issues
3. **Approve refactoring work** for enum consolidation
4. **Allocate time** for technical debt resolution

### For Development Team

1. **Do not add more enums** until duplication is resolved
2. **Do not duplicate any more types** between Client and Core
3. **All new shared types** must go in PoTool.Shared
4. **Test builds locally** before pushing

### For This Audit

The next step is to execute **Phase 1: Restore Compilation** followed by **Phase 2: Eliminate Enum Duplication**.

---

## 11. Conclusion

The PoCompanion codebase has **critical architectural violations** that must be addressed:

1. **12 enums are systematically duplicated** between Client and Core
2. **The solution does not compile** with 89 build errors
3. **NSwag integration is incomplete** and generating incorrect types
4. **PoTool.Shared is underutilized** - not fulfilling its architectural purpose

These issues are **fixable** but require **immediate and focused effort**. The recommended refactoring path is:

1. ✅ Fix NSwag to restore compilation
2. ✅ Move all shared enums to PoTool.Shared
3. ✅ Strengthen architecture guardrails
4. ✅ Complete OpenAPI generation

**Estimated total effort: 3.5-4.5 days of focused development work.**

The architecture rules are sound; they simply need to be followed consistently.

---

## Appendix A: Complete List of Duplicated Enums

### 1. DataMode
- **Client:** `PoTool.Client/Models/DataMode.cs`
- **Core:** `PoTool.Core/Settings/DataMode.cs`
- **Members:** Mock, Tfs

### 2. DependencyLinkType
- **Client:** `PoTool.Client/Models/DependencyLinkType.cs`
- **Core:** `PoTool.Core/WorkItems/DependencyGraphDto.cs`
- **Members:** RelatedTo, DependsOn, Blocks, Parent, Child

### 3. PipelineType
- **Client:** `PoTool.Client/Models/PipelineType.cs`
- **Core:** `PoTool.Core/Pipelines/PipelineDto.cs`
- **Members:** Build, Release, Unknown

### 4. PipelineRunResult
- **Client:** `PoTool.Client/Models/PipelineRunResult.cs`
- **Core:** `PoTool.Core/Pipelines/PipelineRunDto.cs`
- **Members:** Succeeded, Failed, Canceled, PartiallySucceeded, Unknown

### 5. TrendDirection
- **Client:** `PoTool.Client/Models/TrendDirection.cs`
- **Core:** `PoTool.Core/Metrics/MultiIterationBacklogHealthDto.cs`
- **Members:** Improving, Stable, Declining

### 6. BottleneckSeverity
- **Client:** `PoTool.Client/Models/BottleneckSeverity.cs`
- **Core:** `PoTool.Core/WorkItems/WorkItemStateTimelineDto.cs`
- **Members:** None, Low, Medium, High, Critical

### 7. DependencyChainRisk
- **Client:** `PoTool.Client/Models/DependencyChainRisk.cs`
- **Core:** `PoTool.Core/WorkItems/DependencyGraphDto.cs`
- **Members:** None, Low, Medium, High

### 8. ImbalanceRiskLevel
- **Client:** `PoTool.Client/Models/ImbalanceRiskLevel.cs`
- **Core:** `PoTool.Core/Metrics/EffortImbalanceDto.cs`
- **Members:** None, Low, Medium, High

### 9. EffortTrendDirection
- **Client:** `PoTool.Client/Models/EffortTrendDirection.cs`
- **Core:** `PoTool.Core/Metrics/EffortDistributionTrendDto.cs`
- **Members:** Improving, Stable, Worsening

### 10. ConcentrationRiskLevel
- **Client:** `PoTool.Client/Models/ConcentrationRiskLevel.cs`
- **Core:** `PoTool.Core/Metrics/EffortConcentrationRiskDto.cs`
- **Members:** None, Low, Medium, High

### 11. ForecastConfidence
- **Client:** `PoTool.Client/Models/ForecastConfidence.cs`
- **Core:** `PoTool.Core/Metrics/EpicCompletionForecastDto.cs`
- **Members:** Low, Medium, High

### 12. ProfilePictureType
- **Client:** `PoTool.Client/Models/ProfilePictureType.cs`
- **Core:** `PoTool.Core/Settings/ProfileDto.cs`
- **Members:** Initial, Gravatar, Custom

---

## Appendix B: Files Requiring Changes

### Phase 2 - Files to Create in PoTool.Shared

```
PoTool.Shared/
├── WorkItems/
│   ├── DependencyLinkType.cs (NEW)
│   └── DependencyChainRisk.cs (NEW)
├── Metrics/
│   ├── TrendDirection.cs (NEW)
│   ├── BottleneckSeverity.cs (NEW)
│   ├── ImbalanceRiskLevel.cs (NEW)
│   ├── EffortTrendDirection.cs (NEW)
│   ├── ConcentrationRiskLevel.cs (NEW)
│   ├── ForecastConfidence.cs (NEW)
│   ├── CapacityStatus.cs (NEW)
│   ├── WarningLevel.cs (NEW)
│   ├── MitigationStrategy.cs (NEW)
│   └── RecommendationType.cs (NEW)
├── Pipelines/
│   ├── PipelineType.cs (NEW)
│   ├── PipelineRunResult.cs (NEW)
│   └── PipelineRunTrigger.cs (NEW)
├── PullRequests/
│   └── ReviewerStatus.cs (NEW)
├── Settings/
│   ├── DataMode.cs (NEW)
│   └── ProfilePictureType.cs (NEW)
└── Contracts/
    └── TfsVerification/
        ├── FailureCategory.cs (NEW)
        ├── CleanupStatus.cs (NEW)
        └── MutationType.cs (NEW)
```

### Phase 2 - Files to Delete from PoTool.Client

```
PoTool.Client/Models/
├── DependencyLinkType.cs (DELETE)
├── PipelineType.cs (DELETE)
├── ImbalanceRiskLevel.cs (DELETE)
├── DependencyChainRisk.cs (DELETE)
├── PipelineRunResult.cs (DELETE)
├── BottleneckSeverity.cs (DELETE)
├── DataMode.cs (DELETE)
├── TrendDirection.cs (DELETE)
├── ConcentrationRiskLevel.cs (DELETE)
├── EffortTrendDirection.cs (DELETE)
├── ProfilePictureType.cs (DELETE)
└── ForecastConfidence.cs (DELETE)
```

### Phase 2 - Files to Modify in PoTool.Core

All DTO files that currently define enums inline:
- `PoTool.Core/Settings/DataMode.cs`
- `PoTool.Core/Settings/ProfileDto.cs`
- `PoTool.Core/WorkItems/DependencyGraphDto.cs`
- `PoTool.Core/WorkItems/WorkItemStateTimelineDto.cs`
- `PoTool.Core/Pipelines/PipelineDto.cs`
- `PoTool.Core/Pipelines/PipelineRunDto.cs`
- `PoTool.Core/Metrics/MultiIterationBacklogHealthDto.cs`
- `PoTool.Core/Metrics/EffortDistributionTrendDto.cs`
- `PoTool.Core/Metrics/EffortConcentrationRiskDto.cs`
- `PoTool.Core/Metrics/EpicCompletionForecastDto.cs`
- `PoTool.Core/Metrics/EffortImbalanceDto.cs`
- `PoTool.Core/Metrics/EffortDistributionDto.cs`
- `PoTool.Core/Metrics/SprintCapacityPlanDto.cs`
- `PoTool.Core/PullRequests/PRReviewBottleneckDto.cs`
- `PoTool.Core/Contracts/TfsVerification/FailureCategory.cs`
- `PoTool.Core/Contracts/TfsVerification/CleanupStatus.cs`
- `PoTool.Core/Contracts/TfsVerification/MutationType.cs`

---

**End of Audit Report**
