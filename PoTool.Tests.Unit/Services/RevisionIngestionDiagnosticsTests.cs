using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.Configuration;
using PoTool.Integrations.Tfs.Diagnostics;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class RevisionIngestionDiagnosticsTests
{
    [TestMethod]
    public void StartRun_WhenDisabled_DoesNotSetRunContext()
    {
        var services = new ServiceCollection();
        services.AddOptions<RevisionIngestionDiagnosticsOptions>();
        services.AddSingleton<RevisionIngestionDiagnostics>();
        services.AddSingleton<ILogger<RevisionIngestionDiagnostics>>(NullLogger<RevisionIngestionDiagnostics>.Instance);

        using var provider = services.BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<RevisionIngestionDiagnostics>();

        using var scope = diagnostics.StartRun(
            productOwnerId: 1,
            isBackfill: false,
            startDateTime: null,
            startUtc: DateTimeOffset.UtcNow,
            readConcurrency: 2,
            writeConcurrency: 1,
            hydrationConcurrency: 4,
            out var runContext);

        var hasRunContext = diagnostics.TryGetCurrentRun(out _);

        Assert.IsFalse(hasRunContext);
        Assert.IsFalse(runContext.IsEnabled);
    }
}
