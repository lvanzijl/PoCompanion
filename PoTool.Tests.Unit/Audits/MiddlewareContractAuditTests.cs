using System.Reflection;
using Microsoft.AspNetCore.Http;
using PoTool.Api.Configuration;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class MiddlewareContractAuditTests
{
    private static readonly Assembly ApiAssembly = typeof(ApiApplicationBuilderExtensions).Assembly;

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
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.IsNotEmpty(middlewareTypes, "Expected to discover at least one middleware type.");

        var violations = middlewareTypes
            .Select(ValidateMiddlewareContract)
            .Where(result => result is not null)
            .ToList();

        if (violations.Count == 0)
        {
            return;
        }

        Assert.Fail(string.Join(Environment.NewLine + Environment.NewLine, violations));
    }

    private static string? ValidateMiddlewareContract(Type middlewareType)
    {
        var publicInvokeLikeMethods = middlewareType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name.StartsWith("Invoke", StringComparison.Ordinal))
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ThenBy(method => method.GetParameters().Length)
            .ToList();

        if (publicInvokeLikeMethods.Count != 1)
        {
            return BuildViolation(
                middlewareType,
                $"Expected exactly one public Invoke* method but found {publicInvokeLikeMethods.Count}.",
                publicInvokeLikeMethods);
        }

        var entryMethod = publicInvokeLikeMethods[0];
        if (!string.Equals(entryMethod.Name, "InvokeAsync", StringComparison.Ordinal))
        {
            return BuildViolation(
                middlewareType,
                $"Expected the single public entrypoint to be named InvokeAsync but found {entryMethod.Name}.",
                publicInvokeLikeMethods);
        }

        var parameters = entryMethod.GetParameters();
        if (parameters.Length == 0 || parameters[0].ParameterType != typeof(HttpContext))
        {
            return BuildViolation(
                middlewareType,
                "Expected InvokeAsync to accept HttpContext as the first parameter.",
                publicInvokeLikeMethods);
        }

        if (!typeof(Task).IsAssignableFrom(entryMethod.ReturnType))
        {
            return BuildViolation(
                middlewareType,
                $"Expected InvokeAsync to return Task but found {entryMethod.ReturnType.Name}.",
                publicInvokeLikeMethods);
        }

        return null;
    }

    private static string BuildViolation(
        Type middlewareType,
        string message,
        IReadOnlyCollection<MethodInfo> methods)
    {
        var formattedMethods = methods.Count == 0
            ? "none"
            : string.Join(
                ", ",
                methods.Select(method =>
                    $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})"));

        return $"{middlewareType.FullName}: {message} Public Invoke* methods: {formattedMethods}.";
    }
}
