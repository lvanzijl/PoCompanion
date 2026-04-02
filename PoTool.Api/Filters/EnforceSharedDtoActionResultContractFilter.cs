using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PoTool.Api.Configuration;
using PoTool.Shared.DataState;
using PoTool.Shared.Health;

namespace PoTool.Api.Filters;

public sealed class EnforceSharedDtoActionResultContractFilter : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Result is not ObjectResult { Value: { } value } objectResult)
        {
            return;
        }

        var statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
        if (statusCode < StatusCodes.Status200OK || statusCode >= StatusCodes.Status300MultipleChoices)
        {
            return;
        }

        if (context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
        {
            return;
        }

        if (!SharedDtoActionResultContractResolver.TryGetExpectedPayloadType(
                actionDescriptor.MethodInfo,
                context.HttpContext.Request.Path.Value,
                out var expectedType))
        {
            return;
        }

        ObjectResultTypeContractValidator.EnsureCompatible(expectedType, value, actionDescriptor.DisplayName);
    }
}

public static class SharedDtoActionResultContractResolver
{
    private static readonly Assembly SharedAssembly = typeof(CalculateHealthScoreResponse).Assembly;

    public static bool TryGetExpectedPayloadType(MethodInfo methodInfo, string? requestPath, out Type expectedType)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        if (!TryGetDeclaredPayloadType(methodInfo, out var candidateType))
        {
            expectedType = null!;
            return false;
        }

        expectedType = DataSourceModeConfiguration.RequiresCache(requestPath)
            ? typeof(DataStateResponseDto<>).MakeGenericType(candidateType)
            : candidateType;
        return true;
    }

    public static bool TryGetExpectedPayloadType(MethodInfo methodInfo, out Type expectedType)
        => TryGetExpectedPayloadType(methodInfo, requestPath: null, out expectedType);

    public static bool TryGetDeclaredPayloadType(MethodInfo methodInfo, out Type expectedType)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        var actionReturnType = UnwrapTask(methodInfo.ReturnType);
        if (!actionReturnType.IsGenericType || actionReturnType.GetGenericTypeDefinition() != typeof(ActionResult<>))
        {
            expectedType = null!;
            return false;
        }

        var candidateType = actionReturnType.GetGenericArguments()[0];
        if (!ContainsSharedContractType(candidateType))
        {
            expectedType = null!;
            return false;
        }

        expectedType = candidateType;
        return true;
    }

    private static Type UnwrapTask(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    private static bool ContainsSharedContractType(Type type)
    {
        if (type.Assembly == SharedAssembly)
        {
            return true;
        }

        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            return ContainsSharedContractType(elementType);
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericArguments().Any(ContainsSharedContractType);
    }
}
