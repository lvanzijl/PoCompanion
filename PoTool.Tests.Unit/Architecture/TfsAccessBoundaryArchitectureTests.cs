using System.Reflection;
using System.Runtime.CompilerServices;
using PoTool.Api.Services;
using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Architecture;

[TestClass]
public class TfsAccessBoundaryArchitectureTests
{
    private static readonly string[] ProductionAssemblyNames =
    [
        "PoTool.Api"
    ];

    private static readonly string[] ForbiddenSourcePatterns =
    [
        "AddScoped<ITfsClient, RealTfsClient>",
        "AddScoped<ITfsClient, MockTfsClient>",
        "AddTransient<ITfsClient, RealTfsClient>",
        "AddTransient<ITfsClient, MockTfsClient>",
        "AddSingleton<ITfsClient, RealTfsClient>",
        "AddSingleton<ITfsClient, MockTfsClient>",
        "AddScoped<RealTfsClient>",
        "AddScoped<MockTfsClient>",
        "AddTransient<RealTfsClient>",
        "AddTransient<MockTfsClient>",
        "AddSingleton<RealTfsClient>",
        "AddSingleton<MockTfsClient>",
        "GetRequiredService<RealTfsClient>",
        "GetRequiredService<MockTfsClient>",
        "GetService<RealTfsClient>",
        "GetService<MockTfsClient>",
        "new RealTfsClient(",
        "new MockTfsClient("
    ];

    private static readonly (string Pattern, string[] AllowedFiles)[] RestrictedFactoryPatterns =
    [
        (
            "RealTfsClientFactory.Create(",
            [
                "PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs"
            ]
        ),
        (
            "ActivatorUtilities.CreateInstance<MockTfsClient>",
            [
                "PoTool.Api/Configuration/ApiServiceCollectionExtensions.cs"
            ]
        )
    ];

    private static readonly HashSet<string> RawClientTypeNames =
    [
        typeof(RealTfsClient).FullName!,
        typeof(MockTfsClient).FullName!
    ];

    [TestMethod]
    public void ProductionAssemblies_DoNotDependDirectlyOnRawTfsClients()
    {
        var violations = new List<string>();

        foreach (var assembly in LoadProductionAssemblies())
        {
            foreach (var type in assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)))
            {
                if (RawClientTypeNames.Contains(type.FullName ?? string.Empty))
                {
                    continue;
                }

                foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    foreach (var parameter in constructor.GetParameters())
                    {
                        if (ReferencesRawClient(parameter.ParameterType))
                        {
                            violations.Add($"{type.FullName} constructor parameter {parameter.Name}: {parameter.ParameterType}");
                        }
                    }
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (ReferencesRawClient(field.FieldType))
                    {
                        violations.Add($"{type.FullName} field {field.Name}: {field.FieldType}");
                    }
                }

                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (ReferencesRawClient(property.PropertyType))
                    {
                        violations.Add($"{type.FullName} property {property.Name}: {property.PropertyType}");
                    }
                }
            }
        }

        AssertNoViolations(
            violations,
            "Production assemblies must not depend directly on RealTfsClient or MockTfsClient outside the gateway boundary.");
    }

    [TestMethod]
    public void ProductionSource_DoesNotRegisterOrResolveRawTfsClientsDirectly()
    {
        var repoRoot = FindRepositoryRoot();
        var productionDirectories = new[]
        {
            Path.Combine(repoRoot, "PoTool.Api")
        };

        var violations = new List<string>();

        foreach (var directory in productionDirectories)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                                        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                var content = File.ReadAllText(file);
                foreach (var pattern in ForbiddenSourcePatterns)
                {
                    if (content.Contains(pattern, StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(repoRoot, file)} contains forbidden TFS boundary pattern `{pattern}`.");
                    }
                }

                var relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                foreach (var (pattern, allowedFiles) in RestrictedFactoryPatterns)
                {
                    if (content.Contains(pattern, StringComparison.Ordinal) &&
                        !allowedFiles.Contains(relativePath, StringComparer.Ordinal))
                    {
                        violations.Add($"{relativePath} contains restricted TFS boundary pattern `{pattern}` outside the approved gateway registration files.");
                    }
                }
            }
        }

        AssertNoViolations(
            violations,
            "Production source must not directly register or resolve raw TFS clients.");
    }

    private static IEnumerable<Assembly> LoadProductionAssemblies()
    {
        foreach (var assemblyName in ProductionAssemblyNames)
        {
            yield return Assembly.Load(assemblyName);
        }
    }

    private static bool ReferencesRawClient(Type type)
    {
        if (RawClientTypeNames.Contains(type.FullName ?? string.Empty))
        {
            return true;
        }

        if (type.IsArray || type.IsByRef || type.IsPointer)
        {
            return ReferencesRawClient(type.GetElementType()!);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ReferencesRawClient);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }

    private static void AssertNoViolations(IEnumerable<string> violations, string message)
    {
        var violationList = violations.Distinct().OrderBy(violation => violation).ToList();
        if (violationList.Count == 0)
        {
            return;
        }

        Assert.Fail($"{message}{Environment.NewLine}{string.Join(Environment.NewLine, violationList)}");
    }
}
