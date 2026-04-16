using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PoTool.Api.Configuration;
using PoTool.Api.Controllers;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class DataSourceRouteClassificationAuditTests
{
    private static readonly Assembly ApiAssembly = typeof(WorkItemsController).Assembly;

    [TestMethod]
    public void ManagedControllerRoutes_AreExplicitlyClassified()
    {
        var violations = GetControllerMethods()
            .Select(method => (method, route: BuildConcreteRoute(method)))
            .Where(entry => entry.route is not null && !DataSourceModeConfiguration.ShouldBypassMiddleware(entry.route))
            .Where(entry => DataSourceModeConfiguration.GetRouteIntent(entry.route) == DataSourceModeConfiguration.RouteIntent.Unknown)
            .Select(entry => $"{entry.method.DeclaringType!.Name}.{entry.method.Name} => {entry.route}")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    private static IEnumerable<MethodInfo> GetControllerMethods()
        => ApiAssembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));

    private static string? BuildConcreteRoute(MethodInfo methodInfo)
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
            return NormalizeWithConcreteValues(methodTemplate);
        }

        var controllerSegment = methodInfo.DeclaringType!.Name.Replace("Controller", string.Empty, StringComparison.OrdinalIgnoreCase);
        var resolvedControllerRoute = (controllerRoute ?? string.Empty)
            .Replace("[controller]", controllerSegment, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(methodTemplate))
        {
            return NormalizeWithConcreteValues(resolvedControllerRoute);
        }

        return NormalizeWithConcreteValues($"{resolvedControllerRoute.TrimEnd('/')}/{methodTemplate.TrimStart('/')}");
    }

    private static string NormalizeWithConcreteValues(string route)
    {
        var normalized = Regex.Replace(route, "//+", "/");
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = Regex.Replace(
            normalized,
            @"\{([^}:]+)(?::([^}]+))?\}",
            match => string.Equals(match.Groups[2].Value, "int", StringComparison.OrdinalIgnoreCase) ? "123" : "sample",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return normalized;
    }
}
