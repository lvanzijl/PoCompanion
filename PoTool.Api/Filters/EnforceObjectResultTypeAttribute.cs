using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PoTool.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class EnforceObjectResultTypeAttribute : ResultFilterAttribute
{
    public EnforceObjectResultTypeAttribute(Type expectedType)
    {
        ExpectedType = expectedType ?? throw new ArgumentNullException(nameof(expectedType));
    }

    public Type ExpectedType { get; }

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

        var actualType = value.GetType();
        if (actualType == ExpectedType)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Response contract violation for '{context.ActionDescriptor.DisplayName ?? "unknown action"}'. " +
            $"Expected exact payload type '{ExpectedType.FullName}' but got '{actualType.FullName}'.");
    }
}
