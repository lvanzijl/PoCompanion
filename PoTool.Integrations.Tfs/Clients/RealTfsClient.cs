using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Models.Internal;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation with retry logic and enhanced error handling.
/// Supports Azure DevOps Server 2022.2 (API 7.0) and TFS 2019+ (API 5.1+).
/// This is the production implementation that connects to actual Azure DevOps/TFS servers.
/// </summary>
public partial class RealTfsClient : ITfsClient
{
}
