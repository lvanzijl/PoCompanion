using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Filters;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public class IActionResultUntypedResponseAuditTests
{
    [TestMethod]
    public void AllIActionResultEndpoints_AreExplicitlyAllowed()
    {
        var violations = typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(IsHttpAction)
            .Where(UsesUntypedIActionResult)
            .Where(method => !IsExplicitlyAllowed(method))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}")
            .OrderBy(name => name)
            .ToList();

        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            violations,
            "Every IActionResult endpoint must be explicitly annotated with AllowUntypedResponseAttribute.");
    }

    private static bool IsHttpAction(MethodInfo methodInfo)
    {
        return methodInfo.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any();
    }

    private static bool UsesUntypedIActionResult(MethodInfo methodInfo)
    {
        var returnType = methodInfo.ReturnType;
        if (returnType == typeof(IActionResult))
        {
            return true;
        }

        return returnType.IsGenericType
            && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            && returnType.GetGenericArguments()[0] == typeof(IActionResult);
    }

    private static bool IsExplicitlyAllowed(MethodInfo methodInfo)
    {
        return methodInfo.IsDefined(typeof(AllowUntypedResponseAttribute), inherit: true)
            || methodInfo.DeclaringType?.IsDefined(typeof(AllowUntypedResponseAttribute), inherit: true) == true;
    }
}
