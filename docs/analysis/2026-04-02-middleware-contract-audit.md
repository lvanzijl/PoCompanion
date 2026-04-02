# Middleware Contract Audit

## Summary of findings

- Middleware classes discovered: 2
- Middleware classes with violations: 1
- Middleware classes audited:
  - `PoTool.Api.Middleware.DataSourceModeMiddleware`
  - `PoTool.Api.Middleware.WorkspaceGuardMiddleware`
- Registrations audited:
  - `app.UseMiddleware<PoTool.Api.Middleware.DataSourceModeMiddleware>()`
  - `app.UseMiddleware<PoTool.Api.Middleware.WorkspaceGuardMiddleware>()`
- Validation performed:
  - `dotnet build PoTool.sln --configuration Release --nologo` ✅
  - `dotnet test PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --nologo --filter "FullyQualifiedName~Middleware|FullyQualifiedName~MiddlewareContractAuditTests"` ✅
  - Runtime startup check: the previous middleware ambiguity exception no longer occurs; runtime now progresses past middleware binding and fails later in mock-data seeding because of an unrelated SQLite foreign-key issue.

## Middleware inventory and fixes

| Class name | Location | Detected issue | Fix applied |
| --- | --- | --- | --- |
| `DataSourceModeMiddleware` | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/DataSourceModeMiddleware.cs` | Two public `InvokeAsync` overloads caused `UseMiddleware<T>` reflection ambiguity. | Removed the extra public overload so the class now exposes exactly one public `InvokeAsync(HttpContext, IDataSourceModeProvider)` entrypoint. |
| `WorkspaceGuardMiddleware` | `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/Middleware/WorkspaceGuardMiddleware.cs` | No violation. Single public middleware entrypoint already present. | No runtime behavior change; covered by the new audit standard. |

## Discovery results

The repository-wide discovery covered:

- all `UseMiddleware<>` registrations in `PoTool.Api`
- classes under `PoTool.Api/Middleware`
- public `Invoke`/`InvokeAsync` methods on discovered middleware types
- middleware unit tests in `PoTool.Tests.Unit/Middleware`

No additional custom middleware classes or alternate `UseMiddleware<>` registrations were found outside the two API middleware classes above.

## Before/after examples

### Fix 1: remove the ambiguous public overload from `DataSourceModeMiddleware`

**Before**

```csharp
public async Task InvokeAsync(
    HttpContext context,
    IDataSourceModeProvider modeProvider)
{
    // middleware logic
}

public Task InvokeAsync(
    HttpContext context,
    IDataSourceModeProvider modeProvider,
    ICurrentProfileProvider _)
    => InvokeAsync(context, modeProvider);
```

**After**

```csharp
public async Task InvokeAsync(
    HttpContext context,
    IDataSourceModeProvider modeProvider)
{
    // middleware logic
}
```

### Fix 2: add a repository-wide middleware contract audit

**Before**

```csharp
// No repository-wide reflection audit existed for middleware entrypoint contracts.
```

**After**

```csharp
[TestMethod]
public void MiddlewareTypes_ExposeExactlyOnePublicInvokeAsyncEntrypoint()
{
    var middlewareTypes = ApiAssembly
        .GetTypes()
        .Where(type =>
            type is { IsClass: true, IsAbstract: false } &&
            type.Namespace?.Contains(".Middleware", StringComparison.Ordinal) == true &&
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Any(ctor => ctor.GetParameters().FirstOrDefault()?.ParameterType == typeof(RequestDelegate)))
        .ToList();

    // Fails if any middleware exposes multiple public Invoke* methods,
    // a non-InvokeAsync name, a missing HttpContext parameter, or a non-Task return type.
}
```

## Enforced middleware contract standard

The enforced middleware standard is now:

1. Middleware discovery is based on concrete middleware classes with a public constructor whose first parameter is `RequestDelegate`.
2. Each middleware class must expose exactly one public instance method whose name starts with `Invoke`.
3. That single public method must be named `InvokeAsync`.
4. The first parameter of `InvokeAsync` must be `HttpContext`.
5. `InvokeAsync` must return `Task`.
6. No additional public `Invoke*` overloads are allowed, even if they only add DI parameters.
7. Supporting logic must live in constructor-injected services or non-public helper methods.

## Risks and edge cases discovered

- ASP.NET Core middleware reflection is sensitive to public `Invoke`/`InvokeAsync` overloads; even seemingly harmless DI-only overloads can break startup.
- The new audit scans all non-abstract classes in the API middleware namespace whose public constructor begins with `RequestDelegate`, so future custom middleware added to that namespace will automatically be checked.
- The startup validation surfaced a separate pre-existing issue in `MockConfigurationSeedHostedService` (`SQLite Error 19: FOREIGN KEY constraint failed`). That problem is unrelated to middleware contract resolution but still blocks a full local API startup.
- Middleware unit tests previously called the removed overload directly. They were normalized to exercise the single public entrypoint so the test suite now matches the runtime contract.

## Confirmation status

- The middleware ambiguity bug class is eliminated for the discovered custom middleware in this repository.
- A regression safeguard now fails tests if any future middleware exposes multiple public `Invoke`/`InvokeAsync` methods or otherwise violates the enforced contract.
- What remains: a separate mock-data startup failure in `MockConfigurationSeedHostedService` still prevents a clean local API run, but the specific `"Multiple public 'Invoke' or 'InvokeAsync' methods are available"` class of bug is no longer present in the audited middleware set.
