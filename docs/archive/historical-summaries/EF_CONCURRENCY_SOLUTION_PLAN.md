# EF Core Concurrency Solution - Complete Plan

## PHASE 1: DISCOVERY - COMPLETE ✅

### EF Core Usage Map

#### Services with Direct EF Access
1. **TfsConfigurationService** (Scoped)
   - Line 40: `_db.TfsConfigs.ToListAsync()` - READ
   - Line 70: `_db.TfsConfigs.ToListAsync()` - READ  
   - Line 101: `_db.SaveChangesAsync()` - WRITE
   - Line 109: `_db.TfsConfigs.ToListAsync()` - READ
   - Line 117: `_db.SaveChangesAsync()` - WRITE
   - **Called from**: Every method in RealTfsClient via `GetConfigEntityAsync()`

2. **WorkItemSyncService** (Singleton with Scoped resolution)
   - Line 216: `dbContext.Products.ToListAsync()` - READ
   - Line 392: `dbContext.Products.ToListAsync()` - READ
   - Line 496: `dbContext.Products.ToListAsync()` - READ
   - Line 503: `dbContext.SaveChangesAsync()` - WRITE
   - **Called from**: Background service + manual trigger (not in parallel paths)

3. **EffortEstimationNotificationService** (Background service)
   - Line 73: `.ToListAsync()` - READ
   - **Called from**: Background service (not in parallel paths)

#### RealTfsClient - Parallelism Entry Points
- **Line 3243-3257**: `GetPullRequestsWithDetailsAsync()`
  ```csharp
  var prTasks = new List<Task<...>>();
  foreach (var pr in repoGroup)
  {
      prTasks.Add(_throttler.ExecuteReadAsync(
          () => FetchPrDetailsAsync(pr.Id, repo, cancellationToken),
          cancellationToken));
  }
  var prResults = await Task.WhenAll(prTasks);
  ```
  
- **Line 3273-3287**: File changes fetch (parallel)
  ```csharp
  var fileChangeTasks = new List<Task<...>>();
  foreach (var iteration in prResult.Iterations)
  {
      fileChangeTasks.Add(_throttler.ExecuteReadAsync(
          () => GetPullRequestFileChangesAsync(...),
          cancellationToken));
  }
  var fileChangeResults = await Task.WhenAll(fileChangeTasks);
  ```

- **Line 3317-3327**: `FetchPrDetailsAsync()` - calls:
  - `GetPullRequestIterationsAsync()` → may call `_configService.GetConfigEntityAsync()`
  - `GetPullRequestCommentsAsync()` → may call `_configService.GetConfigEntityAsync()`

#### Methods That Call TfsConfigurationService.GetConfigEntityAsync()
Every method below calls EF indirectly and CAN be called from parallel code:
- Line 214: `ValidateConnectionAsync()`
- Line 304: `GetAreaPathsAsync()`
- Line 386: `GetWorkItemByIdAsync()`
- Line 539: `GetWorkItemsAsync()`
- Line 808: `GetWorkItemsByRootIdsAsync()`
- Line 1341: `GetPullRequestsAsync()` ⚠️ **CALLED FROM PARALLEL**
- Line 1444: `GetPullRequestIterationsAsync()` ⚠️ **CALLED FROM PARALLEL**
- Line 1507: `GetPullRequestCommentsAsync()` ⚠️ **CALLED FROM PARALLEL**
- Line 1592: `GetPullRequestFileChangesAsync()` ⚠️ **CALLED FROM PARALLEL**
- Line 1653: `GetPullRequestThreadCommentsAsync()`
- Line 2036: `GetPullRequestWorkItemLinksAsync()`
- Line 2104: `GetBranchesAsync()`
- Line 2171: `GetPipelinesWithRunsAsync()`
- ... and more

### Confirmed Root Cause
**TfsConfigurationService.GetConfigEntityAsync()** performs EF query on a **scoped DbContext** and is called from **RealTfsClient methods** that execute in **parallel via Task.WhenAll + throttler**.

Result: Multiple parallel tasks access same DbContext → `InvalidOperationException`

---

## PHASE 2: SOLUTION DESIGN

### Strategy: Structural Separation + Hard Safety Net

#### Principle
> **Parallelism is allowed for network/CPU work.**  
> **EF Core access must be serialized OR fully removed from parallel paths.**

### Three-Layer Solution

#### Layer 1: Structural Separation (Primary Fix)
**Load TFS config ONCE before parallel execution, pass as value**

Changes needed:
1. **TfsConfigurationService**: Add method to return TfsConfigEntity as value
2. **RealTfsClient**: Refactor to accept optional preloaded config parameter
3. **RealTfsClient methods**: Accept `TfsConfigEntity?` parameter, use it if provided
4. **Callers** (SyncPullRequestsCommandHandler, WorkItemSyncService): Preload config once, pass to all calls

Benefits:
- No EF access inside parallel loops
- Clean architecture: config is external dependency
- Testable: can pass mock config

#### Layer 2: EF Concurrency Gate (Safety Net)
**Introduce scoped semaphore for all EF-touching services**

Implementation:
1. Create `IEfConcurrencyGate` interface with `ExecuteAsync<T>()` method
2. Implement `EfConcurrencyGate` with `SemaphoreSlim(1,1)` - scoped lifetime
3. Wrap TfsConfigurationService EF calls with gate
4. Register gate as Scoped in DI

Benefits:
- Hard guarantee: Even if future refactor re-introduces overlap, gate prevents exception
- Minimal overhead: Only serializes within same scope (same request)
- No cross-request blocking

#### Layer 3: Verification
**Ensure no IQueryable escapes, all queries materialized**

Checks:
- ✅ TfsConfigurationService: Already uses `ToListAsync()` everywhere
- ✅ No `IQueryable` return types in service layer
- ✅ All queries materialized before leaving repository/service

---

## PHASE 3: IMPLEMENTATION PLAN

### Step 1: Create EF Concurrency Gate
**Files**: 
- `PoTool.Core/Contracts/IEfConcurrencyGate.cs` (new)
- `PoTool.Api/Services/EfConcurrencyGate.cs` (new)

```csharp
// PoTool.Core/Contracts/IEfConcurrencyGate.cs
public interface IEfConcurrencyGate
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}

// PoTool.Api/Services/EfConcurrencyGate.cs
public sealed class EfConcurrencyGate : IEfConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<EfConcurrencyGate> _logger;
    
    // Implementation with try-finally pattern
}
```

**Register in DI** (ApiServiceCollectionExtensions.cs):
```csharp
services.AddScoped<IEfConcurrencyGate, EfConcurrencyGate>();
```

### Step 2: Apply Gate to TfsConfigurationService
**File**: `PoTool.Api/Services/TfsConfigurationService.cs`

Inject `IEfConcurrencyGate` and wrap all EF operations:
- `GetConfigAsync()` - wrap `ToListAsync()`
- `SaveConfigAsync()` - wrap `ToListAsync()` + `SaveChangesAsync()`
- `GetConfigEntityAsync()` - wrap `ToListAsync()`
- `SaveConfigEntityAsync()` - wrap `SaveChangesAsync()`

### Step 3: Add Config Preload to RealTfsClient
**File**: `PoTool.Api/Services/RealTfsClient.cs`

Add private method:
```csharp
private async Task<TfsConfigEntity> GetOrUsePreloadedConfigAsync(
    TfsConfigEntity? preloadedConfig,
    CancellationToken cancellationToken)
{
    if (preloadedConfig != null) return preloadedConfig;
    
    var config = await _configService.GetConfigEntityAsync(cancellationToken);
    ValidateTfsConfiguration(config);
    return config!;
}
```

Refactor all methods to accept optional `TfsConfigEntity? preloadedConfig = null` parameter.

### Step 4: Preload Config in Sync Handlers
**Files**:
- `PoTool.Api/Handlers/PullRequests/SyncPullRequestsCommandHandler.cs`
- `PoTool.Api/Services/WorkItemSyncService.cs`

Pattern:
```csharp
// Before parallel execution starts
var tfsConfig = await tfsConfigService.GetConfigEntityAsync(cancellationToken);

// Pass to methods that will run in parallel
await tfsClient.GetPullRequestsWithDetailsAsync(
    repositoryName: repo,
    preloadedConfig: tfsConfig, // NEW
    cancellationToken: cancellationToken);
```

### Step 5: Add Concurrency Regression Test
**File**: `PoTool.Tests.Unit/Services/TfsClientConcurrencyTests.cs` (new)

Test:
```csharp
[TestMethod]
public async Task GetPullRequestsWithDetails_ParallelExecution_DoesNotThrowConcurrencyException()
{
    // Arrange: Real DbContext + TfsClient configured for parallel PR fetch
    // Act: Call GetPullRequestsWithDetailsAsync with multiple repos
    // Assert: No InvalidOperationException
}
```

---

## PHASE 4: VERIFICATION & TESTING

### Verification Checklist
- [ ] All EF operations in TfsConfigurationService are gated
- [ ] RealTfsClient accepts preloaded config in all public methods
- [ ] Sync handlers preload config before parallel execution
- [ ] No `await _configService.GetConfigEntityAsync()` inside Task.WhenAll loops
- [ ] Concurrency test passes
- [ ] Existing tests still pass

### Test Plan
1. Run existing PullRequestRepository concurrency tests → should pass
2. Run new TfsClient concurrency test → should pass
3. Manual test: Sync multiple products with multiple repos → no exceptions
4. Stress test: Run 10 concurrent sync operations → no exceptions

---

## PHASE 5: ROLLOUT STRATEGY

### Minimal Change Principle
This solution requires changes to:
1. **New files** (2): IEfConcurrencyGate, EfConcurrencyGate
2. **Modified services** (3): TfsConfigurationService, RealTfsClient, WorkItemSyncService  
3. **Modified handler** (1): SyncPullRequestsCommandHandler
4. **DI registration** (1): ApiServiceCollectionExtensions
5. **New test** (1): TfsClientConcurrencyTests

Total: 8 files

### Risk Assessment
- **Low risk**: Changes are surgical, well-isolated
- **No schema changes**: No migrations needed
- **No API changes**: Existing callers still work (optional parameters)
- **Backward compatible**: Old code paths still functional

### Rollback Plan
If issues occur:
1. Revert gate from TfsConfigurationService (remove gate parameter)
2. Revert preload changes to RealTfsClient (remove optional parameter)
3. System returns to current state (with concurrency risk)

---

## SUCCESS CRITERIA

### Must Have ✅
1. No `InvalidOperationException` during PR sync
2. All existing tests pass
3. New concurrency test passes
4. TFS config is preloaded before parallel execution
5. EF concurrency gate protects all TfsConfigurationService EF calls

### Nice to Have 🎯
1. Performance metrics: No slowdown vs. current implementation
2. Logging: Gate wait times visible in logs for debugging
3. Documentation: Clear comments explaining pattern

---

## APPENDIX: Architecture Contract

### Rule: EF Access in Parallel Code
**NEVER** call EF operations (ToListAsync, SaveChangesAsync) from inside:
- Task.WhenAll loops
- Throttled operations (ExecuteReadAsync, ExecuteWriteAsync)
- Parallel.ForEach
- Any code that may execute concurrently

### Pattern: Config Preload
```csharp
// CORRECT: Load once, pass to parallel operations
var config = await configService.GetConfigEntityAsync(ct);
var tasks = items.Select(item => 
    ProcessAsync(item, config, ct)); // config passed
await Task.WhenAll(tasks);

// WRONG: Load inside parallel operation
var tasks = items.Select(item => 
    ProcessAsync(item, ct)); // will load config internally
await Task.WhenAll(tasks);
```

### Pattern: EF Concurrency Gate
```csharp
// Wrap all EF operations in services that may be called from parallel code
public async Task<T> GetDataAsync(CancellationToken ct)
{
    return await _efGate.ExecuteAsync(async () =>
    {
        var entities = await _db.Items.ToListAsync(ct);
        return Map(entities);
    }, ct);
}
```

---

## TIMELINE ESTIMATE

- **Phase 1 (Discovery)**: ✅ Complete
- **Phase 2 (Design)**: ✅ Complete  
- **Phase 3 (Implementation)**: 2-3 hours
- **Phase 4 (Testing)**: 1 hour
- **Phase 5 (Review)**: 30 min

**Total**: ~4 hours end-to-end
