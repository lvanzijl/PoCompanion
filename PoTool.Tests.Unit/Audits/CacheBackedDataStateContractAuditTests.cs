using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PoTool.Api.Configuration;
using PoTool.Api.Controllers;
using PoTool.Api.Filters;
using PoTool.Shared.DataState;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
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

    [TestMethod]
    public void CacheBackedOpenApiSnapshot_UsesDataStateWrapperSchemas_AndRemovesNormalizedStatuses()
    {
        var repositoryRoot = GetRepositoryRoot();
        var snapshotPath = Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient", "OpenApi", "swagger.json");
        using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
        var paths = document.RootElement.GetProperty("paths");

        var violations = GetControllerMethods()
            .Select(method => (method, route: BuildRoute(method)))
            .Where(entry => entry.route is not null && DataSourceModeConfiguration.RequiresCache(entry.route))
            .Where(entry => SharedDtoActionResultContractResolver.TryGetDeclaredPayloadType(entry.method, out _))
            .Select(entry => ValidateSnapshotContract(paths, entry.method, entry.route!))
            .Where(result => result is not null)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }

    [TestMethod]
    public void GeneratedClient_UsesWrapperTypes_ForRepresentativeCacheBackedFamilies()
    {
        var repositoryRoot = GetRepositoryRoot();
        var generatedPath = Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient", "Generated", "ApiClient.g.cs");
        var generated = File.ReadAllText(generatedPath);

        var expectedSignatures = new[]
        {
            "Task<DataStateResponseDtoOfDeliveryQueryResponseDtoOfBuildQualityPageDto> GetRollingAsync(",
            "Task<DataStateResponseDtoOfSprintQueryResponseDtoOfSprintMetricsDto> GetSprintMetricsAsync(",
            "Task<DataStateResponseDtoOfPipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto> GetMetricsAsync(",
            "Task<DataStateResponseDtoOfPullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto> GetMetricsAsync(",
            "Task<DataStateResponseDtoOfProjectPlanningSummaryDto> GetPlanningSummaryAsync(",
            "Task<DataStateResponseDtoOfIEnumerableOfWorkItemWithValidationDto> GetAllWithValidationAsync(",
            "Task<DataStateResponseDtoOfReleasePlanningBoardDto> GetBoardAsync(",
            "Task<DataStateResponseDtoOfFilterByValidationResponse> FilterByValidationWithAncestorsAsync("
        };

        foreach (var signature in expectedSignatures)
        {
            StringAssert.Contains(generated, signature);
        }
    }

    private static IEnumerable<MethodInfo> GetControllerMethods()
        => ApiAssembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }

    private static string? ValidateSnapshotContract(JsonElement paths, MethodInfo methodInfo, string route)
    {
        if (!paths.TryGetProperty(route, out var pathEntry) && !paths.TryGetProperty(route.Replace("/api/", "/api/", StringComparison.Ordinal), out pathEntry))
        {
            return $"{methodInfo.DeclaringType!.Name}.{methodInfo.Name} => missing OpenAPI path {route}";
        }

        var httpAttribute = methodInfo.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().First();
        var verb = httpAttribute.HttpMethods.Single().ToLowerInvariant();
        if (!pathEntry.TryGetProperty(verb, out var operation))
        {
            return $"{methodInfo.DeclaringType!.Name}.{methodInfo.Name} => missing OpenAPI verb {verb} for {route}";
        }

        if (!operation.TryGetProperty("responses", out var responses) ||
            !responses.TryGetProperty("200", out var successResponse) ||
            !successResponse.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("application/json", out var mediaType) ||
            !mediaType.TryGetProperty("schema", out var schema) ||
            !schema.TryGetProperty("$ref", out var schemaRef) ||
            !schemaRef.GetString()!.Contains("DataStateResponseDto", StringComparison.Ordinal))
        {
            return $"{methodInfo.DeclaringType!.Name}.{methodInfo.Name} => {route} does not use a DataState wrapper schema in OpenAPI";
        }

        if (responses.TryGetProperty("204", out _) || responses.TryGetProperty("404", out _))
        {
            return $"{methodInfo.DeclaringType!.Name}.{methodInfo.Name} => {route} still exposes normalized 204/404 statuses in OpenAPI";
        }

        foreach (var response in responses.EnumerateObject())
        {
            if (int.TryParse(response.Name, out var statusCode) && statusCode >= 500 && statusCode <= 599)
            {
                return $"{methodInfo.DeclaringType!.Name}.{methodInfo.Name} => {route} still exposes normalized 5xx statuses in OpenAPI";
            }
        }

        return null;
    }

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
