using Microsoft.Extensions.DependencyInjection;
using PoTool.Core.Contracts;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Creates the real TFS client without exposing the concrete implementation as an application-layer DI target.
/// </summary>
public static class RealTfsClientFactory
{
    public static ITfsClient Create(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<RealTfsClient>(serviceProvider);
    }
}
