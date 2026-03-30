> **NOTE:** This document reflects a historical state prior to Batch 3 cleanup.

# TFS Access Boundary Sealed

## Summary
The TFS access boundary is now **sealed repository-wide**.

This change removes the remaining bypass paths that were identified in the verification report:
- the validator tool no longer resolves `ITfsClient` directly to `RealTfsClient`
- raw TFS clients are no longer directly registered in application DI
- raw client usage is structurally constrained and guarded by architectural tests

Result: **all production `ITfsClient` resolution now flows through `TfsAccessGateway`**.

---

## Changes
### Validator tool fix
Updated `PoTool.Tools.TfsRetrievalValidator/Program.cs` so the tool now registers:
- `ITfsAccessGateway`
- `ITfsClient -> ITfsAccessGateway`

instead of:
- `ITfsClient -> RealTfsClient`

The tool now creates the real transport through `RealTfsClientFactory.Create(...)`, validates runtime mode with `TfsRuntimeModeGuard`, and wraps the result in `TfsAccessGateway`.

Relevant files:
- `PoTool.Tools.TfsRetrievalValidator/Program.cs`
- `PoTool.Tools.TfsRetrievalValidator/PoTool.Tools.TfsRetrievalValidator.csproj`

### DI changes
Updated API DI so raw clients are no longer registered as normal container services.

Before:
- `services.AddScoped<MockTfsClient>()`
- `services.AddScoped<RealTfsClient>()`
- gateway resolved those concrete services back out of DI

After:
- API configures the transport pieces it needs
- `ITfsAccessGateway` creates the inner client itself
- `ITfsClient` resolves only through `ITfsAccessGateway`

Relevant file:
- `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### Client visibility changes
- `RealTfsClient` is now `internal`
- `MockTfsClient` is now `internal`
- `PoTool.Integrations.Tfs` no longer exposes internals to `PoTool.Tools.TfsRetrievalValidator`
- added `RealTfsClientFactory` as the only public creation path for the real transport implementation

Relevant files:
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClient.*.cs`
- `PoTool.Integrations.Tfs/Clients/RealTfsClientFactory.cs`
- `PoTool.Integrations.Tfs/AssemblyInfo.cs`
- `PoTool.Api/Services/MockTfsClient.cs`

---

## Enforcement Guarantees
The following are now enforced structurally:

1. **`ITfsClient` resolves through the gateway in API and tool code**
   - `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
   - `PoTool.Tools.TfsRetrievalValidator/Program.cs`

2. **Production DI no longer exposes raw clients as normal services**
   - `RealTfsClient` is not directly resolvable from API DI
   - `MockTfsClient` is not directly resolvable from API DI
   - covered by `ServiceCollectionTests`

3. **Validator tool can no longer directly reference the real client internals**
   - `InternalsVisibleTo("PoTool.Tools.TfsRetrievalValidator")` was removed from `PoTool.Integrations.Tfs`

4. **Future direct constructor-based raw client dependencies fail tests**
   - architectural test scans production assemblies and fails if any production class directly depends on `RealTfsClient` or `MockTfsClient`

5. **Future forbidden DI registrations and direct resolutions fail tests**
   - architectural test scans production source for forbidden patterns such as:
     - `AddScoped<ITfsClient, RealTfsClient>`
     - `AddScoped<RealTfsClient>`
     - `GetRequiredService<RealTfsClient>`
     - `new RealTfsClient(`
     - corresponding `MockTfsClient` patterns

6. **Even approved raw-client creation paths are restricted to gateway-registration files**
   - `RealTfsClientFactory.Create(...)` is only allowed in:
     - `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`
     - `PoTool.Tools.TfsRetrievalValidator/Program.cs`
   - `ActivatorUtilities.CreateInstance<MockTfsClient>(...)` is only allowed in:
     - `PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs`

### What is now impossible
With these changes in place, the repository no longer allows the previous accidental paths where production code could:
- inject `RealTfsClient` directly from DI
- inject `MockTfsClient` directly from DI
- register `ITfsClient` directly to `RealTfsClient` in the validator tool
- add a production constructor dependency on raw TFS clients without failing tests

---

## Architectural Test
Added:
- `PoTool.Tests.Unit/Architecture/TfsAccessBoundaryArchitectureTests.cs`

### What it checks
1. **Production assembly dependency scan**
   - loads:
     - `PoTool.Api`
     - `PoTool.Tools.TfsRetrievalValidator`
   - inspects constructors, fields, and properties
   - fails if any production type directly references:
     - `RealTfsClient`
     - `MockTfsClient`

2. **Production source scan for forbidden DI/resolution patterns**
   - scans:
     - `PoTool.Api`
     - `PoTool.Tools.TfsRetrievalValidator`
   - fails on direct registrations/resolutions/instantiations of raw clients

3. **Restricted factory usage scan**
   - allows raw-client creation only inside the approved gateway registration files
   - fails if those factory patterns appear anywhere else

### How it fails
The test fails loudly with the violating type or source file path and the forbidden pattern that was detected.

---

## Validation
Validated with:

```text
dotnet build PoTool.sln --configuration Release

dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~TfsAccessGatewayTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~DataSourceModeMiddlewareTests|FullyQualifiedName~DataSourceModeConfigurationTests|FullyQualifiedName~LivePipelineReadProviderDataSourceEnforcementTests|FullyQualifiedName~TfsAccessBoundaryArchitectureTests" -v minimal

dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~TfsAccessBoundaryArchitectureTests|FullyQualifiedName~ServiceCollectionTests|FullyQualifiedName~TfsAccessGatewayTests" -v minimal
```

Observed result:
- build succeeded
- focused TFS gateway / DI / middleware / architecture tests passed

---

## Final Status
**Fully enforced**

The remaining repository-wide bypasses identified in the verification report have been closed:
- validator tool now goes through the gateway
- raw clients are no longer normal DI targets
- regression tests now fail if direct bypass is reintroduced
