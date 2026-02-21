using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Configuration;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Configuration;

[TestClass]
public class RevisionPipelineWiringTests
{
    [TestMethod]
    public void AddPoToolApiServices_RegistersOnlyODataRevisionSource()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddPoToolApiServices(configuration, isDevelopment: true);

        var revisionSourceRegistrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkItemRevisionSource))
            .ToList();

        Assert.HasCount(1, revisionSourceRegistrations, "Exactly one revision source must be registered.");
        Assert.AreEqual(typeof(RealODataRevisionTfsClient), revisionSourceRegistrations[0].ImplementationType);

        var hasLegacyRegistration = services.Any(descriptor =>
            string.Equals(descriptor.ServiceType.Name, "IRevisionTfsClient", StringComparison.Ordinal) ||
            string.Equals(descriptor.ImplementationType?.Name, "RealRevisionTfsClient", StringComparison.Ordinal) ||
            string.Equals(descriptor.ImplementationType?.Name, "RestReportingRevisionSource", StringComparison.Ordinal));

        Assert.IsFalse(hasLegacyRegistration, "Legacy revision retrieval services must not be registered.");
    }

    [TestMethod]
    public void RuntimeSourceFiles_DoNotContainForbiddenLegacyRevisionReferences()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var runtimeRoots = new[]
        {
            Path.Combine(repositoryRoot, "PoTool.Api"),
            Path.Combine(repositoryRoot, "PoTool.Client"),
            Path.Combine(repositoryRoot, "PoTool.Core"),
            Path.Combine(repositoryRoot, "PoTool.Integrations.Tfs"),
            Path.Combine(repositoryRoot, "PoTool.Shared"),
            Path.Combine(repositoryRoot, "PoTool.Tools.TfsRetrievalValidator")
        };

        var forbiddenTokens = new[]
        {
            "/wit/reporting/workitemrevisions",
            "RealRevisionTfsClient",
            "RestReportingRevisionSource",
            "IRevisionTfsClient",
            "WorkItemRevisionSourceSelector",
            "RestReportingRevisions"
        };

        var offenders = new List<string>();
        foreach (var root in runtimeRoots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = File.ReadAllText(file, Encoding.UTF8);
                foreach (var forbidden in forbiddenTokens)
                {
                    if (text.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add($"{file}: {forbidden}");
                    }
                }
            }
        }

        if (offenders.Count > 0)
        {
            Assert.Fail("Forbidden legacy revision references found:\n" + string.Join("\n", offenders));
        }
    }
}
