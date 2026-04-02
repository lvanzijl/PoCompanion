using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PoTool.Api.Configuration;
using PoTool.Api.Controllers;
using PoTool.Api.Filters;
using PoTool.Shared.DataState;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class CacheBackedDataStateContractAuditTests
{
    private static readonly Assembly ApiAssembly = typeof(WorkItemsController).Assembly;

    [TestMethod]
    public void CacheBackedEndpoints_DoNotExposeLegacyStateRoutes()
    {
        var legacyRoutes = GetControllerMethods()
            .Select(method => (method, route: BuildRoute(method)))
            .Where(entry => entry.route is not null && entry.route.Contains("/state/", StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.method.DeclaringType!.Name}.{entry.method.Name} => {entry.route}")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), legacyRoutes);
    }

    [TestMethod]
    public void CacheBackedEndpoints_ResolveToDataStateContract()
    {
        var violations = GetControllerMethods()
            .Select(method => (method, route: BuildRoute(method)))
            .Where(entry => entry.route is not null && DataSourceModeConfiguration.RequiresCache(entry.route))
            .Where(entry => SharedDtoActionResultContractResolver.TryGetDeclaredPayloadType(entry.method, out _))
            .Where(entry =>
            {
                SharedDtoActionResultContractResolver.TryGetExpectedPayloadType(entry.method, entry.route, out var expectedType);
                return !expectedType.IsGenericType || expectedType.GetGenericTypeDefinition() != typeof(DataStateResponseDto<>);
            })
            .Select(entry => $"{entry.method.DeclaringType!.Name}.{entry.method.Name} => {entry.route}")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static IEnumerable<MethodInfo> GetControllerMethods()
        => ApiAssembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));

    private static string? BuildRoute(MethodInfo methodInfo)
    {
        var controllerRoute = methodInfo.DeclaringType?.GetCustomAttribute<RouteAttribute>()?.Template;
        var httpAttribute = methodInfo.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().FirstOrDefault();
        if (httpAttribute is null)
        {
            return null;
        }

        var methodTemplate = httpAttribute.Template ?? string.Empty;
        if (methodTemplate.StartsWith("/", StringComparison.Ordinal))
        {
            return Normalize(methodTemplate);
        }

        var controllerSegment = methodInfo.DeclaringType!.Name.Replace("Controller", string.Empty, StringComparison.OrdinalIgnoreCase);
        var resolvedControllerRoute = (controllerRoute ?? string.Empty)
            .Replace("[controller]", controllerSegment, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(methodTemplate))
        {
            return Normalize(resolvedControllerRoute);
        }

        return Normalize($"{resolvedControllerRoute.TrimEnd('/')}/{methodTemplate.TrimStart('/')}");
    }

    private static string Normalize(string route)
    {
        var normalized = Regex.Replace(route, "//+", "/");
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }
}
