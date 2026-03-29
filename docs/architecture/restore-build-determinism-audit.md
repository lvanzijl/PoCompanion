# Restore & Build Determinism Audit

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Environment audited: local clone at `/home/runner/work/PoCompanion/PoCompanion` on Ubuntu 24.04 with .NET SDK `10.0.201`

## Summary

Overall determinism status: **partially deterministic**.

Current repository state is **stable for restore and build in the audited environment**, but it is **not yet fully deterministic or reproducible across machines and over time**.

Observed baseline:

- `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -m:1 --force-evaluate -v minimal` succeeded.
- `dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore -m:1 --nologo` succeeded with `0` warnings and `0` errors.
- No restore warnings were reproduced for:
  - version conflicts
  - missing sources
  - authentication
- Determinism gaps are structural, not currently active local failures:
  - no `global.json`
  - no `packages.lock.json`
  - no locked restore mode
  - no central package version management file

## Restore Behavior

### Commands run

- `dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -m:1 --force-evaluate -v minimal`
- `dotnet list /home/runner/work/PoCompanion/PoCompanion/PoTool.sln package --include-transitive --format json`
- `dotnet nuget list source`
- `dotnet --info`

### Findings

- Restore succeeded for the full solution in the current environment.
- No intermittent failure was reproduced during this audit.
- No restore warnings were emitted about:
  - package version conflicts
  - missing package sources
  - feed authentication
  - unavailable private feeds
- The repository uses a simple source configuration and therefore does not currently show source-order or duplicate-feed nondeterminism.

### CI / workflow signal

GitHub Actions inspection showed that the repository currently exposes dynamic agent workflows rather than a conventional restore/build pipeline:

- Recent run inspected: `23715058150` (`Running Copilot coding agent`)
- Job inspected: `69080721339` (`copilot`)
- Result: `success`

The available job log tail did not represent a normal restore/build CI gate. It contained agent cleanup/output rather than a repository restore/build verification step. As a result, GitHub Actions currently provides **no authoritative CI signal** for restore/build determinism.

### Restore assessment

- **Current local restore reliability:** stable
- **Cross-machine reproducibility:** not guaranteed
- **Time-stable transitive restore reproducibility:** not guaranteed

## Dependency Analysis

### Repository-level dependency configuration

Not present:

- `Directory.Packages.props`
- `global.json`
- `packages.lock.json`
- `RestorePackagesWithLockFile`
- `RestoreLockedMode`
- `ManagePackageVersionsCentrally`

### Project dependency style

All audited projects target `net10.0` and use inline `PackageReference` versions in each `.csproj`.

Direct package versions are explicit. No floating or ranged versions were found in the audited project files.

### Version mismatch analysis

No direct `PackageReference` conflicts were found across the repository for the same package declared with different explicit versions.

However, transitive package resolution is not uniform across all project graphs. The audit found multiple resolved versions of the following packages across the solution:

| Package | Resolved versions observed | Notes |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.1`, `6.0.0` | `PoTool.Core` resolves `6.0.0`; other projects resolve `10.0.1` |
| `Microsoft.Extensions.DependencyModel` | `10.0.1`, `6.0.2` | `PoTool.Core.Domain.Tests` resolves `6.0.2`; API/tests/tools resolve `10.0.1` |

### Interpretation

These transitive version differences are **not currently causing restore or build failure** in the audited environment:

- no `NU1107`
- no `NU1605`
- no restore failure
- no build failure

But they still matter for determinism because, without lock files, the exact transitive graph is not frozen.

### Dependency assessment

- **Direct dependency pinning:** mostly good
- **Transitive dependency determinism:** insufficient
- **Conflict severity right now:** low to moderate
- **Conflict risk over time / across environments:** real

## NuGet Configuration

### Repository configuration

File audited: `/home/runner/work/PoCompanion/PoCompanion/nuget.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### Findings

- The repository explicitly clears inherited package sources.
- The only configured package source is `nuget.org`.
- No private feeds are configured.
- No authenticated feeds are configured.
- No duplicate or conflicting sources were found.

### NuGet source assessment

This configuration is clean and deterministic with respect to source selection.

There is **no evidence that NuGet source configuration or feed authentication is the root cause** of nondeterministic restore behavior in the current repository state.

## SDK Configuration

### Installed SDKs observed

`dotnet --info` showed multiple installed SDKs, including:

- `10.0.105`
- `10.0.201`
- several `8.x` and `9.x` SDKs

The active SDK during this audit was `10.0.201`.

### Repository SDK pinning

No `global.json` file is present in the repository.

### Impact

Because the repository does not pin an SDK version:

- SDK selection depends on the local machine or runner image
- different .NET 10 patch SDKs may be used on different machines
- MSBuild/NuGet resolver behavior can change with SDK patch upgrades
- a machine without a compatible .NET 10 SDK will fail to restore/build entirely

### SDK assessment

This is a direct reproducibility defect.

Even though restore/build succeed locally, the repository does **not** currently guarantee the same SDK/toolchain will be used elsewhere.

## Root Causes

### Exact causes of restore instability

1. **No SDK pinning (`global.json` missing)**
   - Restore uses whichever compatible SDK is installed.
   - This makes restore behavior environment-dependent.

2. **No lock file strategy (`packages.lock.json` absent)**
   - Transitive package graphs are not frozen.
   - Restore is not guaranteed to be identical over time or across machines.

3. **No locked restore mode**
   - The repository does not enforce a previously approved dependency graph.
   - Resolver output can drift if upstream dependency metadata changes.

4. **Cross-project transitive version skew exists**
   - Not currently failing, but it increases sensitivity to dependency graph changes.

### Exact causes of build instability

1. **No SDK pinning (`global.json` missing)**
   - Build toolchain selection is not deterministic.

2. **Restore output is not fully reproducible**
   - Build stability depends on whatever restore resolved in the environment.
   - Without lock files, build inputs are not completely fixed.

### Explicit non-causes in the current audited state

The following were investigated and are **not** current root causes based on available evidence:

- failing package source authentication
- missing private feed credentials
- duplicate NuGet sources
- floating direct package versions
- currently reproduced restore conflicts
- currently reproduced build failures

## Fix Plan

Minimal, concrete steps to make restore and build deterministic:

1. **Add `global.json` and pin the .NET SDK**
   - Pin the repository to the approved .NET 10 SDK version.
   - This removes environment-dependent SDK selection.

2. **Enable NuGet lock files and commit them**
   - Introduce `packages.lock.json` for the solution/projects.
   - This freezes transitive dependency resolution.

3. **Use locked restore in validation/CI**
   - Restore should fail if the resolved graph differs from the committed lock files.
   - This turns dependency drift into an explicit change instead of silent variation.

4. **Keep `nuget.config` as-is unless requirements change**
   - The current source configuration is already minimal and deterministic.
   - No source/auth fix is required based on current evidence.

5. **Optionally normalize the small transitive version skew as follow-up hygiene**
   - Not required for the minimal determinism fix.
   - Worth reviewing if future restore conflicts appear.

### Whether lock files are required

**Yes**, if the goal is truly “fully deterministic and reproducible” restore.

Direct `PackageReference` version pinning alone is not enough because it does not freeze transitive dependency resolution.

### Whether version pinning is required

- **SDK version pinning:** **required**
- **Direct package version pinning:** already mostly present
- **Transitive dependency pinning through lock files:** **required**
- **Central package management (`Directory.Packages.props`):** optional for maintainability, not required for the minimal determinism fix

## Final Verdict

**Partially deterministic**

The repository is currently:

- deterministic enough to restore/build in the audited environment
- not deterministic enough to guarantee the same results across machines, runner images, and future restores

### Final classification

- **Restore behavior:** locally stable, globally not fully reproducible
- **Build behavior:** locally stable, globally not fully reproducible
- **Overall verdict:** **Partially deterministic**

To reach a fully deterministic and reproducible state, the repository needs:

1. pinned SDK selection via `global.json`
2. committed NuGet lock files
3. locked restore enforcement in CI/validation
