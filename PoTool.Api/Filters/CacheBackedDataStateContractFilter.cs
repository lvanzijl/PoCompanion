using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PoTool.Api.Configuration;
using PoTool.Api.Services;
using PoTool.Shared.DataState;

namespace PoTool.Api.Filters;

public sealed class CacheBackedDataStateContractFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly CacheReadinessStateService _cacheReadinessStateService;
    private readonly ILogger<CacheBackedDataStateContractFilter> _logger;

    public CacheBackedDataStateContractFilter(
        CacheReadinessStateService cacheReadinessStateService,
        ILogger<CacheBackedDataStateContractFilter> logger)
    {
        _cacheReadinessStateService = cacheReadinessStateService;
        _logger = logger;
    }

    public int Order => int.MinValue;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ShouldApply(context.HttpContext.Request.Path.Value, context.ActionDescriptor))
        {
            await next();
            return;
        }

        if (!TryGetExpectedPayloadType(context.ActionDescriptor, out var payloadType))
        {
            await next();
            return;
        }

        var readiness = await _cacheReadinessStateService.GetCurrentStateAsync(context.HttpContext.RequestAborted);
        if (readiness.State is DataStateDto.NotReady or DataStateDto.Failed)
        {
            context.Result = CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                readiness.State,
                data: null,
                readiness.Reason,
                readiness.RetryAfterSeconds);
            return;
        }

        var executedContext = await next();
        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            _logger.LogError(executedContext.Exception, "Cache-backed action failed for {Path}", context.HttpContext.Request.Path);
            executedContext.Result = CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                DataStateDto.Failed,
                data: null,
                "The request could not be completed right now.");
            executedContext.ExceptionHandled = true;
            return;
        }

        executedContext.Result = WrapResult(
            context.HttpContext.Request.Path.Value,
            payloadType,
            executedContext.Result ?? new EmptyResult());
    }

    private static bool ShouldApply(string? path, ActionDescriptor actionDescriptor)
    {
        if (string.IsNullOrWhiteSpace(path) || !DataSourceModeEndpointMetadataResolver.RequiresCache(actionDescriptor, path))
        {
            return false;
        }

        if (actionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetExpectedPayloadType(ActionDescriptor actionDescriptor, out Type payloadType)
    {
        if (actionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
        {
            return SharedDtoActionResultContractResolver.TryGetDeclaredPayloadType(controllerActionDescriptor.MethodInfo, out payloadType);
        }

        payloadType = null!;
        return false;
    }

    private static IActionResult WrapResult(string? path, Type payloadType, IActionResult result)
    {
        if (result is ObjectResult { Value: { } value } &&
            CacheBackedDataStateObjectResultFactory.IsDataStateResponse(value.GetType()))
        {
            return result;
        }

        return result switch
        {
            NotFoundObjectResult notFoundObject => CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                DataStateDto.Empty,
                data: null,
                ExtractMessage(notFoundObject.Value)),
            NotFoundResult => CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                DataStateDto.Empty,
                data: null,
                "No data matched the requested scope."),
            NoContentResult => CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                DataStateDto.Empty,
                data: null,
                "No data matched the requested scope."),
            ObjectResult serverObjectResult when (serverObjectResult.StatusCode ?? StatusCodes.Status200OK) >= StatusCodes.Status500InternalServerError => CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                DataStateDto.Failed,
                data: null,
                ExtractMessage(serverObjectResult.Value) ?? "The request could not be completed right now."),
            ObjectResult successObjectResult when (successObjectResult.StatusCode ?? StatusCodes.Status200OK) is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices => CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                CacheBackedDataStatePayloadInspector.IsEmpty(path, successObjectResult.Value) ? DataStateDto.Empty : DataStateDto.Available,
                successObjectResult.Value,
                CacheBackedDataStatePayloadInspector.IsEmpty(path, successObjectResult.Value) ? "No data matched the requested scope." : null),
            StatusCodeResult statusCodeResult when statusCodeResult.StatusCode >= StatusCodes.Status500InternalServerError => CacheBackedDataStateObjectResultFactory.Create(
                payloadType,
                DataStateDto.Failed,
                data: null,
                "The request could not be completed right now."),
            _ => result
        };
    }

    private static string? ExtractMessage(object? value)
        => value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            _ => null
        };
}

internal static class CacheBackedDataStateObjectResultFactory
{
    public static ObjectResult Create(
        Type payloadType,
        DataStateDto state,
        object? data,
        string? reason = null,
        int? retryAfterSeconds = null)
    {
        var responseType = typeof(DataStateResponseDto<>).MakeGenericType(payloadType);
        var response = Activator.CreateInstance(responseType)
            ?? throw new InvalidOperationException($"Could not create {responseType.Name}.");

        responseType.GetProperty(nameof(DataStateResponseDto<object>.State))!.SetValue(response, state);
        responseType.GetProperty(nameof(DataStateResponseDto<object>.Data))!.SetValue(response, data);
        responseType.GetProperty(nameof(DataStateResponseDto<object>.Reason))!.SetValue(response, reason);
        responseType.GetProperty(nameof(DataStateResponseDto<object>.RetryAfterSeconds))!.SetValue(response, retryAfterSeconds);

        return new ObjectResult(response)
        {
            StatusCode = StatusCodes.Status200OK
        };
    }

    public static bool IsDataStateResponse(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DataStateResponseDto<>);
}

internal static class CacheBackedDataStatePayloadInspector
{
    public static bool IsEmpty(string? path, object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        if (value is IEnumerable enumerable && value is not IDictionary)
        {
            return !enumerable.Cast<object?>().Any();
        }

        if (TryGetBoolean(value, "HasData", out var hasData))
        {
            return !hasData;
        }

        if (TryGetPropertyValue(value, "Data", out var data))
        {
            return IsEmpty(path, data);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (path.Contains("/validation-queue", StringComparison.OrdinalIgnoreCase) &&
                TryGetEnumerableCount(value, "RuleGroups", out var ruleGroupCount))
            {
                return ruleGroupCount == 0;
            }

            if (path.Contains("/validation-fix", StringComparison.OrdinalIgnoreCase) &&
                TryGetEnumerableCount(value, "Items", out var itemCount))
            {
                return itemCount == 0;
            }

            if (path.Contains("/validation-triage", StringComparison.OrdinalIgnoreCase))
            {
                return SumTotalItemCounts(value) == 0;
            }

            if (path.Contains("/work-item-activity", StringComparison.OrdinalIgnoreCase) &&
                TryGetEnumerableCount(value, "Activities", out var activityCount))
            {
                return activityCount == 0;
            }

            if (path.Contains("/portfolio-progress-trend", StringComparison.OrdinalIgnoreCase) &&
                TryGetEnumerableCount(value, "Sprints", out var sprintCount))
            {
                return sprintCount == 0;
            }
        }

        return false;
    }

    private static int SumTotalItemCounts(object value)
    {
        return value.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.PropertyType.GetProperty("TotalItemCount") is not null)
            .Select(property => property.GetValue(value))
            .Where(category => category is not null)
            .Sum(category => (int)(category!.GetType().GetProperty("TotalItemCount")!.GetValue(category) ?? 0));
    }

    private static bool TryGetBoolean(object value, string propertyName, out bool propertyValue)
    {
        if (TryGetPropertyValue(value, propertyName, out var rawValue) && rawValue is bool typedValue)
        {
            propertyValue = typedValue;
            return true;
        }

        propertyValue = default;
        return false;
    }

    private static bool TryGetEnumerableCount(object value, string propertyName, out int count)
    {
        if (TryGetPropertyValue(value, propertyName, out var rawValue) &&
            rawValue is IEnumerable enumerable &&
            rawValue is not string)
        {
            count = enumerable.Cast<object?>().Count();
            return true;
        }

        count = default;
        return false;
    }

    private static bool TryGetPropertyValue(object value, string propertyName, out object? propertyValue)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null)
        {
            propertyValue = null;
            return false;
        }

        propertyValue = property.GetValue(value);
        return true;
    }
}
